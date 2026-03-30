using CommunityToolkit.Mvvm.ComponentModel;

namespace PCCleaner.Models;

public partial class CleaningOption : ObservableObject
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }

    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private ScanResult? _scanResult;

    public CleaningOption(string id, string displayName, string description)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
    }
}
