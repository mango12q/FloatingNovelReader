using System;
using System.Runtime.InteropServices;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// Win32 API 封装。通过 P/Invoke 调用原生方法实现：
///   - 窗口置顶切换（SetWindowPos + HWND_TOPMOST）
///   - 鼠标穿透（GWL_EXSTYLE + WS_EX_TRANSPARENT）
///   - 屏幕尺寸（多显示器）
/// </summary>
public static class Win32Helper
{
    // GetWindowLong / SetWindowLong 索引
    public const int GWL_EXSTYLE = -20;
    public const int GWL_STYLE = -16;

    // 扩展窗口样式
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    // SetWindowPos 标志
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public static readonly IntPtr HWND_TOP = new IntPtr(0);

    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    public static void SetTopmost(IntPtr hWnd, bool topmost)
    {
        var insertAfter = topmost ? HWND_TOPMOST : HWND_NOTOPMOST;
        SetWindowPos(hWnd, insertAfter, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>
    /// 设置/取消鼠标穿透。开启后，鼠标点击直接穿透到下层窗口。
    /// </summary>
    public static void SetClickThrough(IntPtr hWnd, bool enabled)
    {
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        if (enabled)
        {
            // 开启 WS_EX_LAYERED + WS_EX_TRANSPARENT
            SetWindowLong(hWnd, GWL_EXSTYLE,
                exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }
        else
        {
            SetWindowLong(hWnd, GWL_EXSTYLE,
                exStyle & ~WS_EX_TRANSPARENT);
        }
    }

    public static bool IsClickThrough(IntPtr hWnd)
    {
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        return (exStyle & WS_EX_TRANSPARENT) != 0;
    }

    public static void SetToolWindow(IntPtr hWnd, bool tool)
    {
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        if (tool)
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        else
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle & ~WS_EX_TOOLWINDOW);
    }
}
