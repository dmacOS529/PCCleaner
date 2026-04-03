using System.Runtime.InteropServices;

namespace PCCleaner.Services;

public static class RecycleBinHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO
    {
        public uint cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    public static (long itemCount, long totalBytes) QueryRecycleBin()
    {
        var info = new SHQUERYRBINFO
        {
            cbSize = (uint)Marshal.SizeOf<SHQUERYRBINFO>()
        };

        int hr = SHQueryRecycleBin(null, ref info);
        if (hr == 0)
            return (info.i64NumItems, info.i64Size);

        return (0, 0);
    }

    public static bool EmptyRecycleBin()
    {
        int hr = SHEmptyRecycleBin(IntPtr.Zero, null,
            SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
        return hr == 0;
    }
}
