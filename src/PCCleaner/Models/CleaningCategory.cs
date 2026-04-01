using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PCCleaner.Models;

public partial class CleaningCategory : ObservableObject
{
    public string Name { get; }
    public string IconGlyph { get; }
    public ObservableCollection<CleaningOption> Options { get; } = new();

    [ObservableProperty]
    private bool _allSelected = true;

    public CleaningCategory(string name, string iconGlyph)
    {
        Name = name;
        IconGlyph = iconGlyph;
    }

    [RelayCommand]
    private void ToggleAll()
    {
        AllSelected = !AllSelected;
        foreach (var option in Options)
            option.IsSelected = AllSelected;
    }
}
