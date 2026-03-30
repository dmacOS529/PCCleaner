using PCCleaner.Models;

namespace PCCleaner.Services;

public interface IBrowserCleaner
{
    string BrowserName { get; }
    bool IsInstalled();
    Task<ScanResult> ScanCookiesAsync();
    Task<ScanResult> ScanHistoryAndTempAsync();
    Task<ScanResult> ScanCacheAsync();
    Task<CleaningResult> CleanFilesAsync(IEnumerable<string> filePaths, IProgress<int>? progress = null);
}
