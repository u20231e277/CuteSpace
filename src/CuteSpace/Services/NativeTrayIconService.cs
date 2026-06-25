using System.Runtime.InteropServices;

namespace CuteSpace.Services;

public sealed class NativeTrayIconService : IDisposable
{
    private const int NifMessage = 0x00000001;
    private const int NifIcon = 0x00000002;
    private const int NifTip = 0x00000004;
    private const int NimAdd = 0x00000000;
    private const int NimDelete = 0x00000002;
    private const int WmTrayIcon = 0x0400 + 44;
    private const int WmLButtonUp = 0x0202;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;
    private const int ImageIcon = 1;
    private const int LrLoadFromFile = 0x00000010;
    private const int LrDefaultSize = 0x00000040;

    private readonly WndProc _wndProc;
    private nint _hwnd;
    private nint _hIcon;
    private ushort _classAtom;
    private bool _visible;

    public event EventHandler? ShowRequested;

    public NativeTrayIconService()
    {
        _wndProc = WindowProc;
    }

    public void Show(string iconPath)
    {
        if (_visible)
        {
            return;
        }

        EnsureWindow();
        _hIcon = LoadImage(0, iconPath, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize);

        var data = CreateData();
        data.uFlags = NifMessage | NifTip | NifIcon;
        data.hIcon = _hIcon;
        data.szTip = "CuteSpace";
        Shell_NotifyIcon(NimAdd, ref data);
        _visible = true;
    }

    public void Hide()
    {
        if (!_visible)
        {
            return;
        }

        var data = CreateData();
        Shell_NotifyIcon(NimDelete, ref data);
        _visible = false;
    }

    public void Dispose()
    {
        Hide();
        if (_hIcon != 0)
        {
            DestroyIcon(_hIcon);
            _hIcon = 0;
        }

        if (_hwnd != 0)
        {
            DestroyWindow(_hwnd);
            _hwnd = 0;
        }

        if (_classAtom != 0)
        {
            UnregisterClass("CuteSpaceTrayWindow", GetModuleHandle(null));
            _classAtom = 0;
        }
    }

    private NotifyIconData CreateData()
    {
        return new NotifyIconData
        {
            cbSize = Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hwnd,
            uID = 1,
            uCallbackMessage = WmTrayIcon
        };
    }

    private void EnsureWindow()
    {
        if (_hwnd != 0)
        {
            return;
        }

        var wc = new WindowClass
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = "CuteSpaceTrayWindow"
        };

        _classAtom = RegisterClass(ref wc);
        _hwnd = CreateWindowEx(0, "CuteSpaceTrayWindow", "CuteSpaceTrayWindow", 0, 0, 0, 0, 0, 0, 0, wc.hInstance, 0);
    }

    private nint WindowProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WmTrayIcon && (lParam == WmLButtonUp || lParam == WmLButtonDblClk || lParam == WmRButtonUp))
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClass(ref WindowClass lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string lpClassName, nint hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadImage(nint hInst, string name, int type, int cx, int cy, int fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint hIcon);
}
