
using System.Runtime.InteropServices;
using System.Text;
using static MouseHook;
using EasyWindowSnapper;
using static AppSettings;
using System.Collections.Concurrent;


public class SplitScreenApp
{
    // 共通で使う定数の定義
    private static double LeftScreenRatio => AppSettings.Instance.LeftScreenRatio;
    private static double ExtendRatio => AppSettings.Instance.ExtendRatio;
    private static ButtonAction MiddleForwardButtonClickAction => AppSettings.Instance.MiddleForwardButtonClickAction;
    private static ButtonAction MiddleBackButtonClickAction => AppSettings.Instance.MiddleBackButtonClickAction;
    // ウィンドウの一覧表示で無視するウィンドウのタイトル
    private static List<string> IgnoreWindowTitles => AppSettings.Instance.IgnoreWindowTitles;
    // ウィンドウのリサイズ時に毎回サイズを変更しないウィンドウのクラス名
    private static List<string> NonImmediateResizeWindowClasses => AppSettings.Instance.NonImmediateResizeWindowClasses;
    // 透過
    const int GWL_EXSTYLE = -20;
    const int WS_EX_LAYERED = 0x80000;
    const int LWA_ALPHA = 0x2;

    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;
    private const int ULW_ALPHA = 0x00000002;
    private const int SWP_SHOWWINDOW = 0x0040;
    private const int SW_MINIMIZE = 6;
    private const int SW_MAXIMIZE = 3;

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    // イベント
    const UInt32 WM_CLOSE = 0x0010;
    private const int SW_RESTORE = 9;
    const uint GA_ROOTOWNER = 3;
    private const int MaxLastActivePopupIterations = 50;
    const uint GA_ROOT = 2;
    const int HWND_TOP = 0;

    const int HWND_TOPMOST = -1;
    const int HWND_NOTOPMOST = -2;

    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOSIZE = 0x0001;

    const int SW_HIDE = 0;
    const int SW_SHOW = 5;


    private const int SNAP_WAIT_TIME = 100;


    // リサイズのキュー
    private const int MAX_PENDING_CALLS = 8;
    private ConcurrentQueue<Task> tasks = new ConcurrentQueue<Task>();
    private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);


    // モニターのサイズ
    private Dictionary<int, Rectangle> monitorWorkingAreas = new Dictionary<int, Rectangle>();



    // パフォーマンス改善用

    private bool resizeStarted = false;


    // ウィンドウの最小幅のキャッシュ
    // 将来的に必要に応じてクリアするようにしたい
    // 時間? または、ウィンドウの数?
    private Dictionary<IntPtr, int> handleMinWidthCache = new Dictionary<IntPtr, int>();
    private int GetMinWidthWithCache(IntPtr handle)
    {
        if (!handleMinWidthCache.ContainsKey(handle))
        {
            handleMinWidthCache[handle] = GetMinWidth(handle);
        }

        return handleMinWidthCache[handle];
    }

    public int GetMinWidth(IntPtr window)
    {
        // Get current window size
        GetWindowRect(window, out RECT rect);
        int originalWidth = rect.Right - rect.Left;
        int originalHeight = rect.Bottom - rect.Top;

        // Set width to minimum
        SetWindowPos(window, IntPtr.Zero, rect.Left, rect.Top, 1, originalHeight, SWP_NOZORDER | SWP_NOACTIVATE);
        Thread.Sleep(10);


        // Get new window size
        GetWindowRect(window, out rect);
        int minWidth = rect.Right - rect.Left;

        // Restore original size
        SetWindowPos(window, IntPtr.Zero, rect.Left, rect.Top, originalWidth, originalHeight, SWP_NOZORDER | SWP_NOACTIVATE);

        return minWidth;
    }




    public class ResizingContext
    {
        public IntPtr LeftWindow { get; set; }
        public IntPtr RightWindow { get; set; }
        public IntPtr ActualLeftWindow { get; set; }
        public IntPtr ActualRightWindow { get; set; }
        public int ExtendPixel { get; set; }
        public int MonitorWidth { get; set; }
        public int MonitorIndex { get; set; }
    }
    private ResizingContext context;


    private NotifyIcon _trayIcon;
    private WindowSelector _windowSelector;
    private DummyWindow _leftDummyWindow;
    private DummyWindow _rightDummyWindow;

    private List<WindowItem> _windows = new List<WindowItem>(); // 管理するウィンドウのリスト

    public struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public Point ptMinPosition;
        public Point ptMaxPosition;
        public RECT rcNormalPosition;
    }

    // 外部関数のインポート

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
    public interface IVirtualDesktopManager
    {
        bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow);
        Guid GetWindowDesktopId(IntPtr topLevelWindow);
        void MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }

    [ComImport]
    [Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
    public class VirtualDesktopManager
    {
    }


    [DllImport("user32.dll")]
    static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);


    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);


    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [DllImport("user32.dll")]
    public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = false)]
    static extern IntPtr GetDesktopWindow();


    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetLastActivePopup(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(Point p);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr BeginDeferWindowPos(int nNumWindows);

    [DllImport("user32.dll")]
    public static extern IntPtr DeferWindowPos(IntPtr hWinPosInfo, IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);


    public SplitScreenApp()
    {

        // コンテキストメニューストリップを作成し、"Exit"メニューアイテムで充填
        var contextMenuStrip = new ContextMenuStrip();

        // Exit menu item
        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += Exit;
        contextMenuStrip.Items.Add(exitMenuItem);

        // Settings menu item
        var settingsMenuItem = new ToolStripMenuItem("Settings");
        settingsMenuItem.Click += OpenSettings;
        contextMenuStrip.Items.Add(settingsMenuItem);

        // アイコンのパスを取得
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons/icon.ico");

        _trayIcon = new NotifyIcon()
        {
            Icon = new Icon(iconPath),
            ContextMenuStrip = contextMenuStrip,
            Visible = true
        };

        // フォームの初期化と非表示設定
        _windowSelector = new WindowSelector(new List<WindowItem>());
        _windowSelector.Visible = false;

        _leftDummyWindow = new DummyWindow(0, 0, 0, 0);
        _leftDummyWindow.Visible = false;
        _rightDummyWindow = new DummyWindow(0, 0, 0, 0);
        _rightDummyWindow.Visible = false;

        ClearContext();

    }

    private void OpenSettings(object sender, EventArgs e)
    {
        var settingsForm = new SettingsForm();
        settingsForm.Show();
    }

    public void Run()
    {
        MouseHook.MouseAction += OnMouseAction;
        MouseHook.Start();

    }
    // // ウィンドウを移動する関数
    private void MoveWindowByRatio(IntPtr hWnd, double screenRatio, bool moveToLeft)
    {

        // ウィンドウが最大化または最小化されている場合、ウィンドウを復元する
        if (IsZoomed(hWnd) || IsIconic(hWnd))
        {
            ShowWindow(hWnd, SW_RESTORE);
        }
        Rectangle workingArea = Screen.FromHandle(hWnd).WorkingArea;

        int width = (int)(workingArea.Width * screenRatio);
        int height = workingArea.Height;
        int x;
        int y = workingArea.Y;

        if (moveToLeft)
        {
            x = workingArea.X;
        }
        else
        {
            x = (int)(workingArea.X + (workingArea.Width * (1 - screenRatio)));
        }

        SetWindowPos(hWnd, (IntPtr)HWND_TOP, x, y, width, height, SWP_SHOWWINDOW);
        MoveForeGroundWindow(hWnd);
    }

    private void SnapWindow(IntPtr hwnd, int monitorIndex, bool moveToLeft)
    {

        MoveWindow(hwnd, monitorIndex, moveToLeft);
        // 右に移動した場合は左のウィンドウの左に移動する
        Thread.Sleep(100);

        if (moveToLeft)
        {

            var (leftWindowRoot, rightWindowRoot) = GetLeftAndRightWindow(monitorIndex);
            if (rightWindowRoot != IntPtr.Zero)
            {
                MoveWindow(rightWindowRoot, monitorIndex, false);
                MoveForeGroundWindow(rightWindowRoot);


            }

        }
        else
        {
            var (leftWindowRoot, rightWindowRoot) = GetLeftAndRightWindow(monitorIndex);
            if (leftWindowRoot != IntPtr.Zero)
            {
                MoveWindow(leftWindowRoot, monitorIndex, true);
                MoveForeGroundWindow(leftWindowRoot);

            }
        }
        MoveForeGroundWindow(hwnd);
    }

    private void MoveForeGroundWindow(IntPtr hwnd)
    {

        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        SetForegroundWindow(hwnd);


    }



    // ウィンドウを左右どちらかの空いているスペースに移動する関数
    private void MoveWindow(IntPtr hWnd, int monitorIndex, bool moveToLeft)
    {
        // MinimizeWindow(hWnd);
        var (leftWindowRoot, rightWindowRoot) = GetLeftAndRightWindow(monitorIndex);

        Rectangle workingArea = GetMonitorWorkingArea(monitorIndex);
        var monitorWidth = workingArea.Width;
        GetWindowRect(rightWindowRoot, out RECT rightWindowRect);
        GetWindowRect(leftWindowRoot, out RECT leftWindowRect);

        if (moveToLeft)
        {


            if (rightWindowRoot == IntPtr.Zero)
            {
                if (leftWindowRoot == IntPtr.Zero)
                {
                    MoveWindowByRatio(hWnd, LeftScreenRatio, true);
                    return;
                }
                else
                {
                    MoveWindowByWidth(hWnd, leftWindowRect.Width, true);
                    return;
                }
            }

            else
            {

                if (leftWindowRoot == rightWindowRoot)
                {
                    MoveWindowByRatio(hWnd, LeftScreenRatio, true);
                    return;
                }
                var remainingWidth = monitorWidth - rightWindowRect.Width;
                MoveWindowByWidth(hWnd, remainingWidth, true);
                return;
            }


        }

        else
        {

            if (leftWindowRoot == IntPtr.Zero)
            {
                if (rightWindowRoot == IntPtr.Zero)
                {
                    MoveWindowByRatio(hWnd, 1 - LeftScreenRatio, false);
                    return;
                }
                else
                {
                    MoveWindowByWidth(hWnd, rightWindowRect.Width, false);
                    return;
                }
            }
            else
            {
                if (leftWindowRoot == rightWindowRoot)
                {
                    MoveWindowByRatio(hWnd, 1 - LeftScreenRatio, false);
                    return;
                }
                var remainingWidth = monitorWidth - leftWindowRect.Width;
                MoveWindowByWidth(hWnd, remainingWidth, false);
                return;
            }
        }


    }


    // TODO　パフォーマンス改善
    private void MoveWindowByWidth(IntPtr hWnd, int width, bool moveToLeft)
    {
        Rectangle workingArea = Screen.FromHandle(hWnd).WorkingArea;
        // ウィンドウが最大化または最小化されている場合、ウィンドウを復元する
        if (IsZoomed(hWnd) || IsIconic(hWnd))
        {
            ShowWindow(hWnd, SW_RESTORE);
        }

        int height = workingArea.Height;
        int x;
        int y = workingArea.Y;

        if (moveToLeft)
        {
            x = workingArea.X;
            SetWindowPos(hWnd, (IntPtr)HWND_TOP, x, y, width, height, 0x0040);

        }
        else
        {
            x = (int)(workingArea.X + (workingArea.Width - width));
        }

        SetWindowPos(hWnd, (IntPtr)HWND_TOP, x, y, width, height, 0x0040);
        if (!moveToLeft)
        {

            GetWindowRect(hWnd, out RECT windowRect);
            if (width < windowRect.Width)
            {
                // 角がずれている場合があるので、再度移動する
                SetWindowPos(hWnd, (IntPtr)HWND_TOP, workingArea.Width - windowRect.Width + workingArea.X, y, width, height, 0x0040);
            }
        }
        MoveForeGroundWindow(hWnd);
    }

    // ウィンドウを交換する関数
    private void SwapWindows(int monitorIndex)
    {
        // 左側のウィンドウと右側のウィンドウを取得
        var (leftWindowRoot, rightWindowRoot) = GetLeftAndRightWindow(monitorIndex);
        if (leftWindowRoot == IntPtr.Zero || rightWindowRoot == IntPtr.Zero)
        {
            return;
        }




        // 両ウィンドウの現在のサイズを取得
        GetWindowRect(leftWindowRoot, out RECT leftWindowRect);
        GetWindowRect(rightWindowRoot, out RECT rightWindowRect);

        int currentRightWindowWidth = rightWindowRect.Width;
        int currentLeftWindowWidth = leftWindowRect.Width;
        int newRightWindowWidth;
        int newLeftWindowWidth;

        Rectangle workingArea = GetMonitorWorkingArea(monitorIndex);


        if (currentLeftWindowWidth <= currentRightWindowWidth)
        {
            newRightWindowWidth = Math.Max(currentLeftWindowWidth, GetMinWidth(rightWindowRoot));
            newLeftWindowWidth = workingArea.Width + workingArea.X - newRightWindowWidth;
        }
        else
        {
            newLeftWindowWidth = Math.Max(currentRightWindowWidth, GetMinWidth(leftWindowRoot));
            newRightWindowWidth = workingArea.Width + workingArea.X - newLeftWindowWidth;
        }


        MoveWindowByWidth(leftWindowRoot, newLeftWindowWidth, false);
        MoveWindowByWidth(rightWindowRoot, newRightWindowWidth, true);
    }


    private (IntPtr leftWindowRoot, IntPtr rightWindowRoot) GetLeftAndRightWindow(int monitorIndex)
    {
        if (monitorIndex < 0 || monitorIndex >= Screen.AllScreens.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(monitorIndex), "Invalid monitor index.");
        }


        Rectangle workingArea = GetMonitorWorkingArea(monitorIndex);

        int leftWindowX = workingArea.X + 30;
        int rightWindowX = workingArea.X + workingArea.Width - 30;
        int y1 = (workingArea.Y + workingArea.Height) / 5;
        int y2 = y1 * 4;

        IntPtr leftWindowHandle1 = WindowFromPoint(new Point(leftWindowX, y1));
        IntPtr leftWindowHandle2 = WindowFromPoint(new Point(leftWindowX, y2));


        IntPtr rightWindowHandle1 = WindowFromPoint(new Point(rightWindowX, y1));
        IntPtr rightWindowHandle2 = WindowFromPoint(new Point(rightWindowX, y2));


        IntPtr rightWindowHandle = rightWindowHandle1 == rightWindowHandle2 ? rightWindowHandle1 : IntPtr.Zero;
        IntPtr leftWindowHandle = leftWindowHandle1 == leftWindowHandle2 ? leftWindowHandle1 : IntPtr.Zero;
        IntPtr rightWindowRoot = GetAncestor(rightWindowHandle, GA_ROOT);
        IntPtr leftWindowRoot = GetAncestor(leftWindowHandle, GA_ROOT);

        StringBuilder title = new StringBuilder(256);
        GetWindowText(leftWindowRoot, title, title.Capacity);
        if (title.Length == 0)
        {
            leftWindowRoot = IntPtr.Zero;
        }
        else
        {
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            GetWindowPlacement(leftWindowRoot, ref placement);
            RECT rcNormalPosition = placement.rcNormalPosition;

            int windowWidth = rcNormalPosition.Right - rcNormalPosition.Left;
            if (IsSmall(windowWidth, rcNormalPosition.Bottom - rcNormalPosition.Top))
            {
                leftWindowHandle1 = IntPtr.Zero;
            }
        }

        title.Clear();
        GetWindowText(rightWindowRoot, title, title.Capacity);
        if (title.Length == 0)
        {
            rightWindowRoot = IntPtr.Zero;
        }
        else
        {
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            GetWindowPlacement(rightWindowRoot, ref placement);
            RECT rcNormalPosition = placement.rcNormalPosition;

            int windowWidth = rcNormalPosition.Right - rcNormalPosition.Left;
            if (IsSmall(windowWidth, rcNormalPosition.Bottom - rcNormalPosition.Top))
            {


                rightWindowRoot = IntPtr.Zero;
            }
        }



        return (leftWindowRoot, rightWindowRoot);
    }



    public async Task<List<WindowItem>> GetAllWindowsOnCurrentDesktopAsync(int monitorIndex)
    {
        return await Task.Run(() =>
        {
            if (monitorIndex < 0 || monitorIndex >= Screen.AllScreens.Length)
            {
                return new List<WindowItem>();
            }

            List<WindowItem> windows = new List<WindowItem>();
            IVirtualDesktopManager virtualDesktopManager = (IVirtualDesktopManager)new VirtualDesktopManager();
            Screen targetMonitor = Screen.AllScreens[monitorIndex];

            // targetMonitorの仮想デスクトップにあるウィンドウを列挙
            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                if (!virtualDesktopManager.IsWindowOnCurrentVirtualDesktop(hWnd))
                {
                    return true; // 現在の仮想デスクトップにないウィンドウは無視
                }

                if (!IsAltTabWindow(hWnd))
                {
                    return true;
                }

                StringBuilder title = new StringBuilder(256);
                GetWindowText(hWnd, title, title.Capacity);
                if (title.Length == 0)
                {
                    return true;
                }

                if (IgnoreWindowTitles.Contains(title.ToString()))
                {
                    return true;

                }

                WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
                placement.length = Marshal.SizeOf(placement);
                GetWindowPlacement(hWnd, ref placement);
                RECT rcNormalPosition = placement.rcNormalPosition;

                int centerX = rcNormalPosition.Left + (rcNormalPosition.Right - rcNormalPosition.Left) / 2;
                int centerY = rcNormalPosition.Top + (rcNormalPosition.Bottom - rcNormalPosition.Top) / 2;

                if (!targetMonitor.Bounds.Contains(centerX, centerY))
                {
                    return true; // ターゲットモニターに中心がないウィンドウは無視
                }

                if (IsSmall(rcNormalPosition.Right - rcNormalPosition.Left, rcNormalPosition.Bottom - rcNormalPosition.Top))
                {
                    return true;
                }
                windows.Add(new WindowItem
                {
                    Handle = hWnd,
                    Title = title.ToString(),
                });

                return true;
            }, IntPtr.Zero);

            return windows;
        });
    }



    // マウス操作時のイベントハンドラ
    private void OnMouseAction(object sender, ExtendedMouseEventArgs e)
    {
        Point cursorPosition = GetCursorPosition(e);
        IntPtr hWnd = WindowFromPoint(cursorPosition);
        IntPtr hWndRoot = GetAncestor(hWnd, GA_ROOT);
        int monitorIndex = GetMonitorIndex(cursorPosition);

        if (e.BackButtonDown)
        {
            HandleBackButtonDown(e, hWndRoot, monitorIndex);
        }

        if (e.ForwardButtonDown)
        {
            HandleForwardButtonDown(e, hWndRoot, monitorIndex);
        }
        if (e.ReleasedButton == MouseButtons.XButton2)
        {
            // _windowsをクリア
            _windows.Clear();
            _windowSelector.ResetIndex();
            _windowSelector.Hide();

        }
        if (e.ReleasedButton == MouseButtons.XButton1)
        {
            resizeStarted = false;
            _windows.Clear();
            _windowSelector.Hide();

            // ダミーのウィンドウを基に実際のウィンドウのサイズを設定し復元
            RestoreFromDummyWindow(context);

        }
    }

    private void RestoreFromDummyWindow(ResizingContext context)
    {
        if (context.ActualLeftWindow != IntPtr.Zero)
        {
            GetWindowRect(context.LeftWindow, out RECT leftWindowRect);
            int width = leftWindowRect.Right - leftWindowRect.Left;
            int height = leftWindowRect.Bottom - leftWindowRect.Top;

            SetWindowPos(context.ActualLeftWindow, IntPtr.Zero, leftWindowRect.Left, leftWindowRect.Top, width, height, 0);
            ShowWindow(context.ActualLeftWindow, SW_SHOW);

        }

        if (context.ActualRightWindow != IntPtr.Zero)
        {
            GetWindowRect(context.RightWindow, out RECT rightWindowRect);

            Rectangle workingArea = GetMonitorWorkingArea(context.MonitorIndex);
            int width = rightWindowRect.Right - rightWindowRect.Left;
            int height = rightWindowRect.Bottom - rightWindowRect.Top;
            SetWindowPos(context.ActualRightWindow, IntPtr.Zero, workingArea.Right - width, workingArea.Top, width, height, 0);
            ShowWindow(context.ActualRightWindow, SW_SHOW);
        }
        _leftDummyWindow.Hide();
        _rightDummyWindow.Hide();

        ClearContext();
    }


    private void MinimizeWindow(IntPtr hWnd)
    {
        ShowWindow(hWnd, SW_MINIMIZE);
    }

    private void MaximizeWindow(IntPtr hWnd)
    {
        ShowWindow(hWnd, SW_MAXIMIZE);
    }

    private void CloseWindow(IntPtr hWnd)
    {
        SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    private Point GetCursorPosition(ExtendedMouseEventArgs e)
    {
        return new Point(e.X, e.Y);
    }

    private int GetMonitorIndex(Point cursorPosition)
    {
        Screen currentScreen = Screen.FromPoint(cursorPosition);
        return Array.IndexOf(Screen.AllScreens, currentScreen);
    }

    private Rectangle GetMonitorWorkingArea(int monitorIndex)
    {
        if (!monitorWorkingAreas.ContainsKey(monitorIndex))
        {
            monitorWorkingAreas[monitorIndex] = Screen.AllScreens[monitorIndex].WorkingArea;
        }

        return monitorWorkingAreas[monitorIndex];
    }

    private async void HandleBackButtonDown(ExtendedMouseEventArgs e, IntPtr hwnd, int monitorIndex)
    {
        if (hwnd == IntPtr.Zero) return;


        // クリック時の処理
        switch (e.PressedButton)
        {
            case MouseButtons.Left:
                if (context.ActualLeftWindow != IntPtr.Zero || context.ActualRightWindow != IntPtr.Zero)
                {
                    return;

                }

                SnapWindow(hwnd, monitorIndex, true);
                SetContext(monitorIndex);
                break;
            case MouseButtons.Right:
                if (context.ActualLeftWindow != IntPtr.Zero || context.ActualRightWindow != IntPtr.Zero)
                {
                    return;
                }
                SnapWindow(hwnd, monitorIndex, false);
                SetContext(monitorIndex);
                break;
            case MouseButtons.Middle:
                if (context.ActualLeftWindow != IntPtr.Zero || context.ActualRightWindow != IntPtr.Zero)
                {
                    return;
                }
                switch (MiddleBackButtonClickAction)
                {
                    case
                        ButtonAction.SWAP_WINDOWS:
                        SwapWindows(monitorIndex);
                        SetContext(monitorIndex);
                        break;

                    case ButtonAction.MINIMIZE_WINDOW:
                        MinimizeWindow(hwnd);

                        break;

                    case ButtonAction.MAXIMIZE_WINDOW:
                        MaximizeWindow(hwnd);
                        break;

                    case ButtonAction.CLOSE_WINDOW:
                        CloseWindow(hwnd);


                        break;


                    default:
                        break;
                }
                break;
        }



        // ホイール時の処理
        if (e.Delta != 0)
        {

            // 初回のみコンテクストをセット
            if (resizeStarted == false)
            {
                resizeStarted = true;

                SetContextWithDummy(monitorIndex);
                MoveForeGroundWindow(context.LeftWindow);
                MoveForeGroundWindow(context.RightWindow);
            }


            // 連続して呼び出された場合はスキップ

            if (tasks.Count < MAX_PENDING_CALLS)
            {
                var task = ResizeWindowBasedOnDelta(e, context);
                tasks.Enqueue(task);

                await task;

                tasks.TryDequeue(out _);
            }



        }
    }



    // 現在のモニターの情報をセットする関数
    private void SetContext(int monitorIndex)
    {
        var (leftWindowRoot, rightWindowRoot) = GetLeftAndRightWindow(monitorIndex);
        context = new ResizingContext
        {
            LeftWindow = leftWindowRoot,
            RightWindow = rightWindowRoot,
            ExtendPixel = GetExtendPixel(monitorIndex),
            MonitorWidth = GetMonitorWorkingArea(monitorIndex).Width,
            MonitorIndex = monitorIndex
        };
    }

    private void ClearContext()
    {
        context = new ResizingContext();
    }

    private void SetContextWithDummy(int monitorIndex)
    {
        var (leftWindowRoot, rightWindowRoot) = GetLeftAndRightWindow(monitorIndex);
        var ActualLeftWindow = IntPtr.Zero;
        var ActualRightWindow = IntPtr.Zero;

        if (NonImmediateResizeWindowClasses.Contains(GetWindowClassName(leftWindowRoot)))
        {
            ActualLeftWindow = leftWindowRoot;
            leftWindowRoot = _leftDummyWindow.WindowHandle;

            _leftDummyWindow.DisplayIcon(ActualLeftWindow);
            GetWindowRect(ActualLeftWindow, out RECT leftWindowRect);
            SetWindowPos(leftWindowRoot, IntPtr.Zero, leftWindowRect.Left, leftWindowRect.Top, leftWindowRect.Right - leftWindowRect.Left, leftWindowRect.Bottom - leftWindowRect.Top, SWP_NOZORDER | SWP_NOACTIVATE);
            ShowWindow(ActualLeftWindow, SW_HIDE);
            _leftDummyWindow.Show();
        }

        if (NonImmediateResizeWindowClasses.Contains(GetWindowClassName(rightWindowRoot)))
        {
            ActualRightWindow = rightWindowRoot;
            rightWindowRoot = _rightDummyWindow.WindowHandle;

            _rightDummyWindow.DisplayIcon(ActualRightWindow);
            GetWindowRect(ActualRightWindow, out RECT rightWindowRect);
            SetWindowPos(rightWindowRoot, IntPtr.Zero, rightWindowRect.Left, rightWindowRect.Top, rightWindowRect.Right - rightWindowRect.Left, rightWindowRect.Bottom - rightWindowRect.Top, SWP_NOZORDER | SWP_NOACTIVATE);
            ShowWindow(ActualRightWindow, SW_HIDE);
            _rightDummyWindow.Show();
        }

        context = new ResizingContext
        {
            LeftWindow = leftWindowRoot,
            RightWindow = rightWindowRoot,
            ActualLeftWindow = ActualLeftWindow,
            ActualRightWindow = ActualRightWindow,
            ExtendPixel = GetExtendPixel(monitorIndex),
            MonitorWidth = GetMonitorWorkingArea(monitorIndex).Width,
            MonitorIndex = monitorIndex
        };
    }

    private async void HandleForwardButtonDown(ExtendedMouseEventArgs e, IntPtr hWndRoot, int monitorIndex)
    {
        if (_windows.Count == 0)
        {
            _windows = await GetAllWindowsOnCurrentDesktopAsync(monitorIndex);
            var (leftWindowRoot, rightWindowRoot) = GetLeftAndRightWindow(monitorIndex);
            // _windowsのleftWindowとrightWindowにtypeをセット
            foreach (var window in _windows)
            {
                if (window.Handle == leftWindowRoot)
                {
                    window.type = WindowItemType.LEFT;
                }
                else if (window.Handle == rightWindowRoot)
                {
                    window.type = WindowItemType.RIGHT;
                }

            }

            _windowSelector.SetFormToMonitor(monitorIndex);
            _windowSelector.Show();
            _windowSelector.UpdateWindows(_windows);


            return;

        }


        if (e.Delta != 0)
        {
            if (e.Delta > 0)
            {

                _windowSelector.SelectPreviousWindow();

            }
            else
            {
                _windowSelector.SelectNextWindow();
            }
        }
        var hwnd = _windowSelector.GetCurrentlySelectedWindow().Handle;
        switch (e.PressedButton)
        {


            case MouseButtons.Left:
                SnapWindow(hwnd, monitorIndex, true);
                IntPtr rightWindowHandle = IntPtr.Zero;

                foreach (var window in _windows)
                {
                    if (window.Handle == hwnd)
                    {

                        if (window.type == WindowItemType.RIGHT)
                        {
                            rightWindowHandle = window.Handle;
                        }
                        window.type = WindowItemType.LEFT;
                    }
                    else if (window.type == WindowItemType.LEFT)
                    {
                        window.type = null;
                    }

                }

                // 右のウィンドウを左に移動した場合
                // 現在右にあるウィンドウのtypeをRIGHTにする
                if (hwnd == rightWindowHandle)
                {
                    var (currentLeftWindow, currentRightWindow) = GetLeftAndRightWindow(monitorIndex);
                    foreach (var window in _windows)
                    {
                        if (window.Handle == currentRightWindow)
                        {
                            window.type = WindowItemType.RIGHT;
                            break;
                        }
                    }
                }
                _windowSelector.UpdateWindows(_windows);

                break;
            case MouseButtons.Right:
                SnapWindow(hwnd, monitorIndex, false);
                IntPtr leftWindowHandle = IntPtr.Zero;

                foreach (var window in _windows)
                {
                    if (window.Handle == hwnd)
                    {
                        if (window.type == WindowItemType.LEFT)
                        {
                            leftWindowHandle = window.Handle;
                        }
                        window.type = WindowItemType.RIGHT;
                    }
                    else if (window.type == WindowItemType.RIGHT)
                    {
                        window.type = null;
                    }
                }

                // 左のウィンドウを右に移動した場合
                // 現在左にあるウィンドウのtypeをLEFTにする
                if (hwnd == leftWindowHandle)
                {
                    var (currentLeftWindow, currentRightWindow) = GetLeftAndRightWindow(monitorIndex);
                    foreach (var window in _windows)
                    {
                        if (window.Handle == currentLeftWindow)
                        {
                            window.type = WindowItemType.LEFT;
                            break;
                        }
                    }
                }
                _windowSelector.UpdateWindows(_windows);
                break;
            case MouseButtons.Middle:

                switch (MiddleForwardButtonClickAction)
                {
                    case ButtonAction.MINIMIZE_WINDOW:
                        MinimizeWindow(hwnd);
                        break;

                    case ButtonAction.CLOSE_WINDOW:
                        CloseWindow(hwnd);
                        System.Threading.Thread.Sleep(200);
                        if (!IsWindow(hwnd))
                        {
                            _windowSelector.deleteWindow(hwnd);

                            var (currentLeftWindow, currentRightWindow) = GetLeftAndRightWindow(monitorIndex);
                            foreach (var window in _windows)
                            {
                                if (window.Handle == currentLeftWindow)
                                {
                                    window.type = WindowItemType.LEFT;

                                }
                                else if (window.Handle == currentRightWindow)
                                {
                                    window.type = WindowItemType.RIGHT;

                                }
                            }
                            _windowSelector.UpdateWindows(_windows);

                        }
                        break;


                    default:
                        break;
                }


                break;


        }
    }



    public static string GetWindowTitle(IntPtr hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        StringBuilder sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetWindowClassName(IntPtr hWnd)
    {
        StringBuilder className = new StringBuilder(256);
        GetClassName(hWnd, className, className.Capacity);
        return className.ToString();
    }


    private Dictionary<int, int> extendPixels = new Dictionary<int, int>();

    private int GetExtendPixel(int monitorIndex)
    {
        if (!extendPixels.ContainsKey(monitorIndex))
        {
            var monitorWidth = GetMonitorWorkingArea(monitorIndex).Width;
            extendPixels[monitorIndex] = (int)(ExtendRatio * monitorWidth);
        }

        return extendPixels[monitorIndex];
    }

    private async Task ResizeWindowBasedOnDelta(ExtendedMouseEventArgs e, ResizingContext context)
    {
        await semaphoreSlim.WaitAsync();
        try
        {
            Rectangle workingArea = GetMonitorWorkingArea(context.MonitorIndex);

            // ホイールを上に回した場合
            if (e.Delta > 0)
            {

                ShrinkLeftExpandRight(workingArea, context);
            }

            // ホイールを下に回した場合
            else
            {
                ShrinkRightExpandLeft(workingArea, context);
            }
            await Task.Delay(5);
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    private void ShrinkLeftExpandRight(Rectangle workingArea, ResizingContext context)
    {
        if (context.RightWindow == IntPtr.Zero)
        {
            ShrinkLeftWindow(workingArea, context);
        }
        else if (context.LeftWindow == IntPtr.Zero)
        {
            ExpandRightWindow(workingArea, context);
        }
        // 最大化されている場合
        else if (context.LeftWindow == context.RightWindow)
        {


            ShrinkLeftWindow(workingArea, context);
            SetContext(context.MonitorIndex);
        }

        else
        {
            ShrinkLeftExpandRightBothWindows(workingArea, context);
        }
    }

    private void ShrinkRightExpandLeft(Rectangle workingArea, ResizingContext context)
    {

        if (context.LeftWindow == IntPtr.Zero)
        {
            ShrinkRightWindow(workingArea, context);
        }
        else if (context.RightWindow == IntPtr.Zero)
        {
            ExpandLeftWindow(workingArea, context);
        }
        else if (context.LeftWindow == context.RightWindow)
        {

            ShrinkRightWindow(workingArea, context);
            SetContext(context.MonitorIndex);
        }
        else
        {
            ShrinkRightExpandLeftBothWindows(workingArea, context);
        }
    }

    private void ShrinkLeftWindow(Rectangle workingArea, ResizingContext context)
    {
        var shrinkedLeftWindowWidth = GetShrinkedWindowWidth(context.LeftWindow, context.ExtendPixel);
        shrinkedLeftWindowWidth = Math.Max(shrinkedLeftWindowWidth, GetMinWidthWithCache(context.ActualLeftWindow != IntPtr.Zero ? context.ActualLeftWindow : context.LeftWindow));
        if (IsZoomed(context.LeftWindow))
        {
            ShowWindow(context.LeftWindow, SW_RESTORE);
        }
        SetWindowPos(context.LeftWindow, (IntPtr)HWND_TOP, workingArea.X, workingArea.Y, shrinkedLeftWindowWidth, workingArea.Height, 0x0040);


    }

    private void ExpandRightWindow(Rectangle workingArea, ResizingContext context)
    {
        var extendedRightWindowWidth = GetExtendedWindowWidth(context.RightWindow, context.ExtendPixel);
        extendedRightWindowWidth = Math.Min(extendedRightWindowWidth, workingArea.Width);
        SetWindowPos(context.RightWindow, (IntPtr)HWND_TOP, workingArea.Width + workingArea.X - extendedRightWindowWidth, workingArea.Y, extendedRightWindowWidth, workingArea.Height, 0x0040);
    }

    private bool ShrinkLeftExpandRightBothWindows(Rectangle workingArea, ResizingContext context)
    {
        int newLeftWindowWidth = Math.Max(GetShrinkedWindowWidth(context.LeftWindow, context.ExtendPixel), GetMinWidthWithCache(context.ActualLeftWindow != IntPtr.Zero ? context.ActualLeftWindow : context.LeftWindow));

        int newRightWindowWidth = workingArea.Width - newLeftWindowWidth;


        IntPtr deferHandle = BeginDeferWindowPos(2);

        deferHandle = DeferWindowPos(deferHandle, context.LeftWindow, (IntPtr)HWND_TOP, workingArea.X, workingArea.Y, newLeftWindowWidth, workingArea.Height, 0x0040);
        if (deferHandle == IntPtr.Zero)
        {
            return false;
        }

        deferHandle = DeferWindowPos(deferHandle, context.RightWindow, (IntPtr)HWND_TOP, workingArea.X + newLeftWindowWidth, workingArea.Y, newRightWindowWidth, workingArea.Height, 0x0040);
        if (deferHandle == IntPtr.Zero)
        {
            return false;
        }

        if (!EndDeferWindowPos(deferHandle))
        {
            return false;
        }

        return true;
    }


    private void ShrinkRightWindow(Rectangle workingArea, ResizingContext context)
    {

        var shrinkedRightWindowWidth = GetShrinkedWindowWidth(context.RightWindow, context.ExtendPixel);
        shrinkedRightWindowWidth = Math.Max(shrinkedRightWindowWidth, GetMinWidthWithCache(context.ActualRightWindow != IntPtr.Zero ? context.ActualRightWindow : context.RightWindow));
        if (IsZoomed(context.RightWindow))
        {
            ShowWindow(context.RightWindow, SW_RESTORE);
        }

        SetWindowPos(context.RightWindow, (IntPtr)HWND_TOP, workingArea.X + workingArea.Width - shrinkedRightWindowWidth, workingArea.Y, shrinkedRightWindowWidth, workingArea.Height, 0x0040);
    }

    private void ExpandLeftWindow(Rectangle workingArea, ResizingContext context)
    {
        var extendedLeftWindowWidth = GetExtendedWindowWidth(context.LeftWindow, context.ExtendPixel);
        extendedLeftWindowWidth = Math.Min(extendedLeftWindowWidth, workingArea.Width);
        SetWindowPos(context.LeftWindow, (IntPtr)HWND_TOP, workingArea.X, workingArea.Y, extendedLeftWindowWidth, workingArea.Height, 0x0040);
    }

    private bool ShrinkRightExpandLeftBothWindows(Rectangle workingArea, ResizingContext context)
    {
        int newRightWindowWidth = Math.Max(GetShrinkedWindowWidth(context.RightWindow, context.ExtendPixel), GetMinWidthWithCache(context.ActualRightWindow != IntPtr.Zero ? context.ActualRightWindow : context.RightWindow));

        int newLeftWindowWidth = workingArea.Width - newRightWindowWidth;

        IntPtr deferHandle = BeginDeferWindowPos(2);

        deferHandle = DeferWindowPos(deferHandle, context.RightWindow, (IntPtr)HWND_TOP, workingArea.X + workingArea.Width - newRightWindowWidth, workingArea.Y, newRightWindowWidth, workingArea.Height, 0x0040);
        if (deferHandle == IntPtr.Zero)
        {
            return false;
        }

        deferHandle = DeferWindowPos(deferHandle, context.LeftWindow, (IntPtr)HWND_TOP, workingArea.X, workingArea.Y, newLeftWindowWidth, workingArea.Height, 0x0040);
        if (deferHandle == IntPtr.Zero)
        {
            return false;
        }

        if (!EndDeferWindowPos(deferHandle))
        {
            return false;
        }

        return true;
    }



    private int GetShrinkedWindowWidth(IntPtr windowHandle, int extendPixel)
    {
        GetWindowRect(windowHandle, out RECT windowRect);

        return (int)(windowRect.Width - extendPixel);
    }

    private int GetExtendedWindowWidth(IntPtr windowHandle, int extendPixel)
    {
        GetWindowRect(windowHandle, out RECT windowRect);
        return (int)(windowRect.Width + extendPixel);
    }


    private static IntPtr GetLastVisibleActivePopUpOfWindow(IntPtr window)
    {
        var level = MaxLastActivePopupIterations;
        var currentWindow = window;
        while (level-- > 0)
        {
            var lastPopUp = GetLastActivePopup(currentWindow);

            if (IsWindowVisible(lastPopUp))
                return lastPopUp;

            if (lastPopUp == currentWindow)
                return IntPtr.Zero;

            currentWindow = lastPopUp;
        }

        return IntPtr.Zero;
    }

    private static bool IsAltTabWindow(IntPtr hwnd)
    {
        if (hwnd == GetShellWindow())
            return false;

        var root = GetAncestor(hwnd, GA_ROOTOWNER);

        if (GetLastVisibleActivePopUpOfWindow(root) != hwnd)
            return false;

        // Rest of the code...
        return true;
    }

    private static bool IsSmall(IntPtr width, IntPtr height)
    {
        if (width < 100 || height < 100)
        {
            return true;
        }
        return false;
    }

    // 終了イベント
    private void Exit(object sender, EventArgs e)
    {
        MouseHook.Stop();
        _trayIcon.Visible = false;
        Application.Exit();
    }
}

