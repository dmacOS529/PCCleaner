using System.IO;

namespace PCCleaner.Services;

public class EdgeCleaner : ChromiumBaseCleaner
{
    public override string BrowserName => "Microsoft Edge";

    public EdgeCleaner()
        : base(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Edge", "User Data"))
    {
    }
}
