using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCCleaner.Models;
using PCCleaner.Services;

namespace PCCleaner.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly List<IBrowserCleaner> _browserCleaners = new();
    private readonly DiskCleaner _diskCleaner = new();

    public ObservableCollection<CleaningCategory> Categories { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isCleaning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
    private bool _hasScanResults;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string _statusMessage = "Select items and click Scan to analyze.";

    [ObservableProperty]
    private long _totalFilesFound;

    [ObservableProperty]
    private long _totalSizeFound;

    [ObservableProperty]
    private string _summaryText = "";

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    private string _lastScanTime = "";

    public bool IsBusy => IsScanning || IsCleaning;

    public MainViewModel()
    {
        InitializeBrowserCleaners();
        BuildCategories();
    }

    private void InitializeBrowserCleaners()
    {
        var cleaners = new IBrowserCleaner[]
        {
            new ChromeCleaner(),
            new EdgeCleaner(),
            new FirefoxCleaner()
        };

        foreach (var cleaner in cleaners)
        {
            if (cleaner.IsInstalled())
                _browserCleaners.Add(cleaner);
        }
    }

    private void BuildCategories()
    {
        if (_browserCleaners.Count > 0)
        {
            var browsers = string.Join(", ", _browserCleaners.Select(b => b.BrowserName));
            var browserCategory = new CleaningCategory("Web Browsers", "\uE774");
            browserCategory.Options.Add(new CleaningOption("browser.cookies", "Website Cookies",
                $"Login tokens and tracking cookies from {browsers}"));
            browserCategory.Options.Add(new CleaningOption("browser.history", "Browsing History & Temp Files",
                $"Visited URLs, form data, code cache, and GPU cache from {browsers}"));
            browserCategory.Options.Add(new CleaningOption("browser.cache", "Cache",
                $"Cached images, scripts, and stylesheets from {browsers}"));
            Categories.Add(browserCategory);
        }

        var diskCategory = new CleaningCategory("Disk Space", "\uE74E");
        diskCategory.Options.Add(new CleaningOption("disk.recyclebin", "Recycle Bin",
            "All deleted files sitting in the Windows Recycle Bin"));
        diskCategory.Options.Add(new CleaningOption("disk.tempfiles", "Temporary Files",
            "User temp and Windows temp folders (installers, logs, update leftovers)"));
        diskCategory.Options.Add(new CleaningOption("disk.crashdumps", "Crash Dumps",
            "Windows Error Reporting crash dump files"));
        diskCategory.Options.Add(new CleaningOption("disk.storeappcache", "Store App Cache",
            "Cached data and temp files from Microsoft Store apps (Spotify, etc.)"));
        Categories.Add(diskCategory);

        var updateCategory = new CleaningCategory("Windows Update", "\uE895");
        updateCategory.Options.Add(new CleaningOption("update.cache", "Update Download Cache",
            "Downloaded update packages in SoftwareDistribution\\Download"));
        updateCategory.Options.Add(new CleaningOption("update.components", "Windows Update Cleanup",
            "Superseded update components via DISM (may take a few minutes)"));
        Categories.Add(updateCategory);
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        IsScanning = true;
        IsComplete = false;
        HasScanResults = false;
        ProgressPercent = 0;
        TotalFilesFound = 0;
        TotalSizeFound = 0;
        SummaryText = "";

        try
        {
            var allOptions = Categories.SelectMany(c => c.Options).Where(o => o.IsSelected).ToList();
            int completed = 0;

            foreach (var option in allOptions)
            {
                option.ScanResult = null;
                StatusMessage = $"Scanning {option.DisplayName}...";

                var result = await ScanOptionAsync(option.Id);
                option.ScanResult = result;
                TotalFilesFound += result.FileCount;
                if (result.TotalBytes > 0)
                    TotalSizeFound += result.TotalBytes;

                completed++;
                ProgressPercent = (int)((double)completed / allOptions.Count * 100);
            }

            HasScanResults = true;
            LastScanTime = $"Last scan: {DateTime.Now:h:mm tt}";
            SummaryText = $"{TotalFilesFound:N0} files found ({FormatSize(TotalSizeFound)})";
            StatusMessage = "Scan complete. Click Clean to remove selected items.";
        }
        catch (Exception ex)
        {
            FileSystemHelper.Log($"!!! SCAN CRASHED: {ex}");
            StatusMessage = $"Error during scan: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private bool CanScan() => !IsScanning && !IsCleaning;

    [RelayCommand(CanExecute = nameof(CanClean))]
    private async Task CleanAsync()
    {
        IsCleaning = true;
        ProgressPercent = 0;

        try
        {
            var warnings = GetBrowserWarnings();
            if (!string.IsNullOrEmpty(warnings))
            {
                StatusMessage = warnings;
            }

            var optionsToClean = Categories
                .SelectMany(c => c.Options)
                .Where(o => o.IsSelected && o.ScanResult != null && (o.ScanResult.FileCount > 0 || o.Id == "disk.recyclebin" || o.Id == "update.components"))
                .ToList();

            FileSystemHelper.Log($"=== CLEAN START: {optionsToClean.Count} options ===");

            long totalFreed = 0;
            long totalDeleted = 0;
            long totalFailed = 0;
            int completed = 0;

            foreach (var option in optionsToClean)
            {
                StatusMessage = $"Cleaning {option.DisplayName}...";

                var result = await CleanOptionAsync(option);
                totalFreed += result.FreedBytes;
                totalDeleted += result.DeletedCount;
                totalFailed += result.FailedCount;

                option.ScanResult = null;

                completed++;
                ProgressPercent = (int)((double)completed / optionsToClean.Count * 100);
            }

            HasScanResults = false;
            IsComplete = true;
            TotalFilesFound = 0;
            TotalSizeFound = 0;
            SummaryText = $"Cleaned {totalDeleted:N0} items, freed {FormatSize(totalFreed)}";
            FileSystemHelper.Log($"=== CLEAN DONE: deleted={totalDeleted}, failed={totalFailed}, freed={FormatSize(totalFreed)} ===");

            if (totalFailed > 0)
                StatusMessage = $"Cleaned {totalDeleted:N0} files. {totalFailed:N0} skipped (in use by other apps).";
            else
                StatusMessage = "All done! Click Scan to run another check.";
        }
        catch (Exception ex)
        {
            FileSystemHelper.Log($"!!! CLEAN CRASHED: {ex}");
            StatusMessage = $"Error during clean: {ex.Message}";
        }
        finally
        {
            IsCleaning = false;
        }
    }

    private bool CanClean() => !IsScanning && !IsCleaning && HasScanResults;

    private async Task<ScanResult> ScanOptionAsync(string optionId)
    {
        if (optionId.StartsWith("disk."))
        {
            var type = optionId.Split('.', 2)[1];
            return type switch
            {
                "recyclebin" => await _diskCleaner.ScanRecycleBinAsync(),
                "tempfiles" => await _diskCleaner.ScanTempFilesAsync(),
                "crashdumps" => await _diskCleaner.ScanCrashDumpsAsync(),
                "storeappcache" => await _diskCleaner.ScanStoreAppCacheAsync(),
                _ => new ScanResult()
            };
        }

        if (optionId.StartsWith("update."))
        {
            var type = optionId.Split('.', 2)[1];
            return type switch
            {
                "cache" => await _diskCleaner.ScanUpdateCacheAsync(),
                "components" => await _diskCleaner.ScanComponentCleanupAsync(),
                _ => new ScanResult()
            };
        }

        if (optionId.StartsWith("browser."))
        {
            var type = optionId.Split('.', 2)[1];
            return await ScanAllBrowsersAsync(type);
        }

        return new ScanResult();
    }

    private async Task<ScanResult> ScanAllBrowsersAsync(string type)
    {
        var combined = new ScanResult();

        foreach (var cleaner in _browserCleaners)
        {
            var result = type switch
            {
                "cookies" => await cleaner.ScanCookiesAsync(),
                "history" => await cleaner.ScanHistoryAndTempAsync(),
                "cache" => await cleaner.ScanCacheAsync(),
                _ => new ScanResult()
            };

            combined.FileCount += result.FileCount;
            combined.TotalBytes += result.TotalBytes;
            combined.FilePaths.AddRange(result.FilePaths);
        }

        return combined;
    }

    private async Task<CleaningResult> CleanOptionAsync(CleaningOption option)
    {
        if (option.Id == "disk.recyclebin")
            return await _diskCleaner.CleanRecycleBinAsync();

        if (option.Id == "update.components")
            return await _diskCleaner.CleanComponentStoreAsync();

        if (option.Id == "update.cache")
            return await _diskCleaner.CleanFilesAsync(option.ScanResult!.FilePaths);

        if (option.Id.StartsWith("disk."))
            return await _diskCleaner.CleanFilesAsync(option.ScanResult!.FilePaths);

        // Browser options — all file paths are already aggregated in ScanResult
        if (option.Id.StartsWith("browser.") && option.ScanResult?.FilePaths.Count > 0)
        {
            // Use the first available cleaner's CleanFilesAsync (they all share the same logic)
            return await _browserCleaners[0].CleanFilesAsync(option.ScanResult.FilePaths);
        }

        return new CleaningResult();
    }

    private string GetBrowserWarnings()
    {
        var browserCategory = Categories.FirstOrDefault(c => c.Name == "Web Browsers");
        if (browserCategory == null || !browserCategory.Options.Any(o => o.IsSelected && o.ScanResult?.FileCount > 0))
            return "";

        var runningBrowsers = new List<string>();
        var browserProcesses = new Dictionary<string, string>
        {
            { "chrome", "Google Chrome" },
            { "msedge", "Microsoft Edge" },
            { "firefox", "Mozilla Firefox" }
        };

        foreach (var (processName, browserName) in browserProcesses)
        {
            try
            {
                if (Process.GetProcessesByName(processName).Length > 0)
                    runningBrowsers.Add(browserName);
            }
            catch { }
        }

        if (runningBrowsers.Count > 0)
            return $"Warning: {string.Join(", ", runningBrowsers)} running. Some files may be locked.";

        return "";
    }

    public static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
