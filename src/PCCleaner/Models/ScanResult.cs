namespace PCCleaner.Models;

public class ScanResult
{
    public long FileCount { get; set; }
    public long TotalBytes { get; set; }
    public List<string> FilePaths { get; set; } = new();
}
