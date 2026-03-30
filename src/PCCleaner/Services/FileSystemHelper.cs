using System.IO;
using System.Security;

namespace PCCleaner.Services;

public static class FileSystemHelper
{
    public static bool TrySafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                return true;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (SecurityException) { }
        return false;
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
