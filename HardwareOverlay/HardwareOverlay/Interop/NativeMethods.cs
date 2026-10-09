using System.Runtime.InteropServices;

namespace HardwareOverlay.Interop;

internal static class NativeMethods
{
    private const int GWL_EXSTYLE = -20;

    private const long WS_EX_TRANSPARENT = 0x00000020;
    private const long WS_EX_TOOLWINDOW  = 0x00000080;
    private const long WS_EX_LAYERED     = 0x00080000;
    private const long WS_EX_NOACTIVATE  = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    public static void MakeOverlayWindow(IntPtr hwnd)
    {
        var style = GetExStyle(hwnd) | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetExStyle(hwnd, style);
    }

    private static long GetExStyle(IntPtr hwnd) =>
        IntPtr.Size == 8
            ? GetWindowLongPtr64(hwnd, GWL_EXSTYLE).ToInt64()
            : GetWindowLong32(hwnd, GWL_EXSTYLE);

    private static void SetExStyle(IntPtr hwnd, long style)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(hwnd, GWL_EXSTYLE, new IntPtr(style));
        else
            SetWindowLong32(hwnd, GWL_EXSTYLE, unchecked((int)style));
    }
}
