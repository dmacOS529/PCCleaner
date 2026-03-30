using System.IO;
using PCCleaner.Models;

namespace PCCleaner.Services;

public class FirefoxCleaner : IBrowserCleaner
{
    private readonly string _profilesPath;
    private readonly string _localProfilesPath;

    public string BrowserName => "Mozilla Firefox";

    public FirefoxCleaner()
    {
        _profilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mozilla", "Firefox", "Profiles");
        _localProfilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Mozilla", "Firefox", "Profiles");
    }

    public bool IsInstalled() => Directory.Exists(_profilesPath);

    public Task<ScanResult> ScanCookiesAsync() => Task.Run(() =>
    {
        var result = new ScanResult();
        foreach (var profile in GetProfiles())
        {
            AddFileIfExists(Path.Combine(profile, "cookies.sqlite"), result);
            AddFileIfExists(Path.Combine(profile, "cookies.sqlite-wal"), result);
            AddFileIfExists(Path.Combine(profile, "cookies.sqlite-shm"), result);
        }
        return result;
    });

    public Task<ScanResult> ScanHistoryAndTempAsync() => Task.Run(() =>
    {
        var result = new ScanResult();
        foreach (var profile in GetProfiles())
        {
            AddFileIfExists(Path.Combine(profile, "places.sqlite"), result);
            AddFileIfExists(Path.Combine(profile, "places.sqlite-wal"), result);
            AddFileIfExists(Path.Combine(profile, "places.sqlite-shm"), result);
            AddFileIfExists(Path.Combine(profile, "formhistory.sqlite"), result);

            // Temp files grouped with history
            ScanDirectory(Path.Combine(profile, "startupCache"), result);
        }
        return result;
    });

    public Task<ScanResult> ScanCacheAsync() => Task.Run(() =>
    {
        var result = new ScanResult();
        foreach (var profileDir in FileSystemHelper.EnumerateDirectoriesSafe(_localProfilesPath))
        {
            var cachePath = Path.Combine(profileDir, "cache2", "entries");
            foreach (var file in FileSystemHelper.EnumerateFilesSafe(cachePath, "*", SearchOption.AllDirectories))
            {
                result.FilePaths.Add(file);
                result.FileCount++;
                result.TotalBytes += FileSystemHelper.GetFileSize(file);
            }
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
        foreach (var dir in FileSystemHelper.EnumerateDirectoriesSafe(_profilesPath))
        {
            profiles.Add(dir);
        }
        return profiles;
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

    private static void AddFileIfExists(string path, ScanResult result)
    {
        if (File.Exists(path))
        {
            result.FilePaths.Add(path);
            result.FileCount++;
            result.TotalBytes += FileSystemHelper.GetFileSize(path);
        }
    }
}
