using System.Windows;
using PCCleaner.ViewModels;
using PCCleaner.Views;

namespace PCCleaner;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel()
        };
        mainWindow.Show();
    }
}
