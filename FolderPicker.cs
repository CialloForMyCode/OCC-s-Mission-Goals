using System.Runtime.InteropServices;

namespace OCCMissionGoals;

/// <summary>
/// 纯 WPF 文件夹选择器，通过 SHBrowseForFolder 实现。
/// </summary>
public static class FolderPicker
{
    /// <summary>
    /// 打开文件夹浏览对话框，返回所选路径或 null。
    /// </summary>
    public static string? Show(string? initialDirectory = null)
    {
        var dialog = new BrowseInfo();
        dialog.hwndOwner = IntPtr.Zero;
        dialog.pidlRoot = IntPtr.Zero;
        dialog.pszDisplayName = new string('\0', 260);
        dialog.lpszTitle = "选择文件夹";
        dialog.ulFlags = 0x00000040; // BIF_NEWDIALOGSTYLE

        // 回调用于设置初始目录
        NativeMethods.BrowseCallbackProc callback = (hwnd, msg, lParam, lpData) =>
        {
            if (msg == 1) // BFFM_INITIALIZED
            {
                var path = Marshal.PtrToStringAuto(lpData);
                if (!string.IsNullOrEmpty(path))
                    SendMessage(hwnd, 0x00000467u, IntPtr.Zero, lpData); // BFFM_SETSELECTIONW
            }
            return 0;
        };

        var pathPtr = IntPtr.Zero;
        try
        {
            if (!string.IsNullOrEmpty(initialDirectory))
                pathPtr = Marshal.StringToCoTaskMemAuto(initialDirectory);

            dialog.lpfn = Marshal.GetFunctionPointerForDelegate(callback);
            dialog.lParam = pathPtr;

            var pidl = NativeMethods.SHBrowseForFolder(ref dialog);
            if (pidl == IntPtr.Zero) return null;

            var path = new char[260];
            NativeMethods.SHGetPathFromIDList(pidl, path);
            Marshal.FreeCoTaskMem(pidl);

            var result = new string(path).TrimEnd('\0');
            return string.IsNullOrEmpty(result) ? null : result;
        }
        finally
        {
            if (pathPtr != IntPtr.Zero)
                Marshal.FreeCoTaskMem(pathPtr);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(
        IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct BrowseInfo
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public string pszDisplayName;
        public string lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    private static class NativeMethods
    {
        public delegate int BrowseCallbackProc(IntPtr hwnd, uint msg, IntPtr lParam, IntPtr lpData);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHBrowseForFolder(ref BrowseInfo lpbi);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SHGetPathFromIDList(IntPtr pidl, char[] pszPath);
    }
}
