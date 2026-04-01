using System.Diagnostics;
using System.IO;
using PCCleaner.Models;

namespace PCCleaner.Services;

public class DiskCleaner
{
    public Task<ScanResult> ScanRecycleBinAsync() => Task.Run(() =>
    {
        var (itemCount, totalBytes) = RecycleBinHelper.QueryRecycleBin();
        return new ScanResult
        {
            FileCount = itemCount,
            TotalBytes = totalBytes
        };
    });

    public Task<CleaningResult> CleanRecycleBinAsync() => Task.Run(() =>
    {
        var (_, totalBytes) = RecycleBinHelper.QueryRecycleBin();
        bool success = RecycleBinHelper.EmptyRecycleBin();
        return new CleaningResult
        {
            DeletedCount = success ? 1 : 0,
            FreedBytes = success ? totalBytes : 0,
            FailedCount = success ? 0 : 1
        };
    });

    public Task<ScanResult> ScanTempFilesAsync() => Task.Run(() =>
    {
        var result = new ScanResult();

        // User temp folder
        var userTemp = Path.GetTempPath();
        ScanDirectory(userTemp, result);

        // Windows temp folder
        var winTemp = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
        ScanDirectory(winTemp, result);

        return result;
    });

    public Task<ScanResult> ScanCrashDumpsAsync() => Task.Run(() =>
    {
        var result = new ScanResult();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        ScanDirectory(Path.Combine(localAppData, "CrashDumps"), result);

        return result;
    });

    public Task<ScanResult> ScanStoreAppCacheAsync() => Task.Run(() =>
    {
        var result = new ScanResult();
        var packagesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");

        if (!Directory.Exists(packagesPath))
            return result;

        foreach (var packageDir in FileSystemHelper.EnumerateDirectoriesSafe(packagesPath))
        {
            ScanDirectory(Path.Combine(packageDir, "LocalCache"), result);
            ScanDirectory(Path.Combine(packageDir, "TempState"), result);
        }

        return result;
    });

    public Task<ScanResult> ScanUpdateCacheAsync() => Task.Run(() =>
    {
        var result = new ScanResult();
        var downloadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SoftwareDistribution", "Download");
        ScanDirectory(downloadPath, result);
        return result;
    });

    public Task<CleaningResult> CleanUpdateCacheAsync(IEnumerable<string> filePaths)
    {
        return CleanFilesAsync(filePaths);
    }

    public Task<ScanResult> ScanComponentCleanupAsync() => Task.Run(() =>
    {
        var result = new ScanResult();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "DISM.exe",
                Arguments = "/Online /Cleanup-Image /AnalyzeComponentStore",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return result;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(60_000);

            // DISM reports "Component Store Cleanup Recommended : Yes"
            if (output.Contains("Yes", StringComparison.OrdinalIgnoreCase))
            {
                // Try to parse the reclaimable size from "Reclaimable Packages : X"
                // The actual size of the component store is reported but exact reclaimable
                // bytes aren't always available, so we flag it as 1 item to indicate cleanup is available
                result.FileCount = 1;

                // Try to find "Size" line for component store size
                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains("Reclaimable", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains("GB", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            var sizeStr = parts[1].Trim().Replace("GB", "").Trim();
                            if (double.TryParse(sizeStr, out var gb))
                                result.TotalBytes = (long)(gb * 1024 * 1024 * 1024);
                        }
                    }
                }

                // Fallback — if we couldn't parse the size, estimate conservatively
                if (result.TotalBytes == 0)
                    result.TotalBytes = -1; // Signal "unknown size" to UI
            }
        }
        catch (Exception ex)
        {
            FileSystemHelper.Log($"DISM AnalyzeComponentStore failed: {ex.Message}");
        }
        return result;
    });

    public Task<CleaningResult> CleanComponentStoreAsync() => Task.Run(() =>
    {
        var result = new CleaningResult();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "DISM.exe",
                Arguments = "/Online /Cleanup-Image /StartComponentCleanup",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null)
            {
                result.FailedCount = 1;
                return result;
            }

            process.WaitForExit(300_000); // up to 5 minutes

            if (process.ExitCode == 0)
                result.DeletedCount = 1;
            else
                result.FailedCount = 1;
        }
        catch (Exception ex)
        {
            FileSystemHelper.Log($"DISM StartComponentCleanup failed: {ex.Message}");
            result.FailedCount = 1;
        }
        return result;
    });

    public Task<CleaningResult> CleanFilesAsync(IEnumerable<string> filePaths, IProgress<int>? progress = null)
    {
        return Task.Run(() =>
        {
            var result = new CleaningResult();
            var files = filePaths.ToList();
            int processed = 0;

            foreach (var path in files)
            {
                var size = FileSystemHelper.GetFileSize(path);
                if (FileSystemHelper.TrySafeDelete(path))
                {
                    result.DeletedCount++;
                    result.FreedBytes += size;
                }
                else
                {
                    result.FailedCount++;
                    result.Errors.Add(path);
                }

                processed++;
                progress?.Report((int)((double)processed / files.Count * 100));
            }
            return result;
        });
    }

    private static void ScanDirectory(string path, ScanResult result)
    {
        foreach (var file in FileSystemHelper.EnumerateFilesSafe(path, "*", SearchOption.AllDirectories))
        {
            result.FilePaths.Add(file);
            result.FileCount++;
            result.TotalBytes += FileSystemHelper.GetFileSize(file);
        }
    }
}
