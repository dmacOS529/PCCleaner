namespace PCCleaner.Models;

public class CleaningResult
{
    public long DeletedCount { get; set; }
    public long FreedBytes { get; set; }
    public long FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
