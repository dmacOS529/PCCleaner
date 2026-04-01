using System.Globalization;
using System.Windows.Data;

namespace PCCleaner.Views.Converters;

public class SelectAllTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool allSelected = value is bool b && b;
        return allSelected ? "Deselect All" : "Select All";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
