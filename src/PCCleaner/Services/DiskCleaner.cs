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
