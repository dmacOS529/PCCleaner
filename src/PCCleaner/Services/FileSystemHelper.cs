using System.IO;
using System.Security;

namespace PCCleaner.Services;

public static class FileSystemHelper
{
#if DEBUG
    private static readonly string LogPath = Path.Combine(
        AppContext.BaseDirectory, "logs", "PCCleaner_debug.log");

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }
#else
    public static void Log(string message) { }
#endif

    public static bool TrySafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return true;
            }
            if (Directory.Exists(path))
            {
                ClearReadOnlyRecursive(path);
                Directory.Delete(path, recursive: true);
                return true;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (SecurityException) { }
        return false;
    }

    private static void ClearReadOnlyRecursive(string directory)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); }
                catch { }
            }
        }
        catch { }
    }

    public static long GetFileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    public static long GetDirectorySize(string path)
    {
        long size = 0;
        foreach (var file in EnumerateFilesSafe(path, "*", SearchOption.AllDirectories))
        {
            size += GetFileSize(file);
        }
        return size;
    }

    public static IEnumerable<string> EnumerateFilesSafe(string path, string pattern, SearchOption option = SearchOption.TopDirectoryOnly)
    {
        if (!Directory.Exists(path))
            yield break;

        IEnumerator<string> enumerator;
        try
        {
            enumerator = Directory.EnumerateFiles(path, pattern, new EnumerationOptions
            {
                RecurseSubdirectories = option == SearchOption.AllDirectories,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System
            }).GetEnumerator();
        }
        catch
        {
            yield break;
        }

        while (true)
        {
            try
            {
                if (!enumerator.MoveNext())
                    break;
            }
            catch
            {
                continue;
            }
            yield return enumerator.Current;
        }

        enumerator.Dispose();
    }

    public static IEnumerable<string> EnumerateDirectoriesSafe(string path, string pattern = "*")
    {
        if (!Directory.Exists(path))
            yield break;

        IEnumerator<string> enumerator;
        try
        {
            enumerator = Directory.EnumerateDirectories(path, pattern, new EnumerationOptions
            {
                IgnoreInaccessible = true
            }).GetEnumerator();
        }
        catch
        {
            yield break;
        }

        while (true)
        {
            try
            {
                if (!enumerator.MoveNext())
                    break;
            }
            catch
            {
                continue;
            }
            yield return enumerator.Current;
        }

        enumerator.Dispose();
    }
}
