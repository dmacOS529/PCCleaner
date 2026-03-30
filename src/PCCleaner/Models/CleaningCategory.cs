using System.Collections.ObjectModel;

namespace PCCleaner.Models;

public class CleaningCategory
{
    public string Name { get; }
    public string IconGlyph { get; }
    public ObservableCollection<CleaningOption> Options { get; } = new();

    public CleaningCategory(string name, string iconGlyph)
    {
        Name = name;
        IconGlyph = iconGlyph;
    }
}
