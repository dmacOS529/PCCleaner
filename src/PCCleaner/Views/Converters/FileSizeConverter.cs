using System.Globalization;
using System.Windows.Data;
using PCCleaner.ViewModels;

namespace PCCleaner.Views.Converters;

public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long bytes)
            return MainViewModel.FormatSize(bytes);
        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
