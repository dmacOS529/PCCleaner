using System.IO;
using PCCleaner.Models;

namespace PCCleaner.Services;

public abstract class ChromiumBaseCleaner : IBrowserCleaner
{
    private readonly string _userDataPath;

    public abstract string BrowserName { get; }

    protected ChromiumBaseCleaner(string userDataPath)
    {
        _userDataPath = userDataPath;
    }

    public bool IsInstalled() => Directory.Exists(_userDataPath);

    public Task<ScanResult> ScanCookiesAsync() => Task.Run(() =>
    {
        var result = new ScanResult();
        foreach (var profile in GetProfiles())
        {
            var cookiePath = Path.Combine(profile, "Network", "Cookies");
            AddFileIfExists(cookiePath, result);
            AddFileIfExists(cookiePath + "-journal", result);
            AddFileIfExists(cookiePath + "-wal", result);
            AddFileIfExists(cookiePath + "-shm", result);
        }
        return result;
    });

    public Task<ScanResult> ScanHistoryAndTempAsync() => Task.Run(() =>
    {
        var result = new ScanResult();
        foreach (var profile in GetProfiles())
        {
            var historyPath = Path.Combine(profile, "History");
            AddFileIfExists(historyPath, result);
            AddFileIfExists(historyPath + "-journal", result);
            AddFileIfExists(historyPath + "-wal", result);
            AddFileIfExists(historyPath + "-shm", result);

            // Temp files grouped with history
            ScanDirectory(Path.Combine(profile, "Code Cache"), result);
            ScanDirectory(Path.Combine(profile, "GPUCache"), result);
        }
        return result;
    });

    public Task<ScanResult> ScanCacheAsync() => Task.Run(() =>
    {
        var result = new ScanResult();
        foreach (var profile in GetProfiles())
        {
            ScanDirectory(Path.Combine(profile, "Cache", "Cache_Data"), result);
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

    private List<string> GetProfiles()
    {
        var profiles = new List<string>();
        if (!Directory.Exists(_userDataPath))
            return profiles;

        var defaultProfile = Path.Combine(_userDataPath, "Default");
        if (Directory.Exists(defaultProfile))
            profiles.Add(defaultProfile);

        foreach (var dir in FileSystemHelper.EnumerateDirectoriesSafe(_userDataPath, "Profile *"))
        {
            profiles.Add(dir);
        }

        return profiles;
    }

    private static void AddFileIfExists(string path, ScanResult result)
    {
        if (File.Exists(path))
        {
            result.FilePaths.Add(path);
            result.FileCount++;
            result.TotalBytes += FileSystemHelper.GetFileSize(path);
        }
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
