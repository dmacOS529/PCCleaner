using System.IO;

namespace PCCleaner.Services;

public class ChromeCleaner : ChromiumBaseCleaner
{
    public override string BrowserName => "Google Chrome";

    public ChromeCleaner()
        : base(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google", "Chrome", "User Data"))
    {
    }
}
