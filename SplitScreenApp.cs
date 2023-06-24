
using System.Runtime.InteropServices;
using System.Text;
using static MouseHook;
using WinSplit;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using static AppSettings;

public class SplitScreenApp
{
    // 共通で使う定数の定義
    private static double LeftScreenRatio => AppSettings.Instance.LeftScreenRatio;
    private static int MinWindowWidth => AppSettings.Instance.MinWindowWidth;
    private static double ExtendRatio => AppSettings.Instance.ExtendRatio;
    private static ButtonAction MiddleForwardButtonClickAction => AppSettings.Instance.MiddleForwardButtonClickAction;
    private static ButtonAction MiddleBackButtonClickAction => AppSettings.Instance.MiddleBackButtonClickAction;
    private static List<string> IgnoreWindowTitles => AppSettings.Instance.IgnoreWindowTitles;
    // 透過
    const int GWL_EXSTYLE = -20;
    const int WS_EX_LAYERED = 0x80000;
    const int LWA_ALPHA = 0x2;

    private HashSet<IntPtr> transparentedWindows = new HashSet<IntPtr>();

    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;
    private const int ULW_ALPHA = 0x00000002;
    private const int SWP_SHOWWINDOW = 0x0040;
    private const int SW_MINIMIZE = 6;
    private const int SW_MAXIMIZE = 3;

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

    private const int SNAP_WAIT_TIME = 100;


    // モニターのサイズ
    private Dictionary<int, Rectangle> monitorWorkingAreas = new Dictionary<int, Rectangle>();



    // 戻るボタンを押しているかどうか
    private bool isBackButtonPressed = false;
    // var (leftWindowRoot, rightWindowRoot) = GetLeftAndRightWindow(monitorIndex);
    IntPtr resizingLeftWindow = IntPtr.Zero;
    IntPtr resizingRightWindow = IntPtr.Zero;
    int resizingMonitorWidth = 0;
    int resizingExtendPixel = 0;

    public class ResizingContext
    {
        public IntPtr LeftWindow { get; set; }
        public IntPtr RightWindow { get; set; }
        public int ExtendPixel { get; set; }
        public int MonitorWidth { get; set; }
        public int MonitorIndex { get; set; }
    }
    private ResizingContext context;


    private void CompleteResize()
    {
        resizingLeftWindow = IntPtr.Zero;
        resizingRightWindow = IntPtr.Zero;
        resizingMonitorWidth = 0;
        resizingExtendPixel = 0;
    }

    // ウィンドウ情報を管理するための変数と構造体の定義
    private NotifyIcon _trayIcon;
    private WindowSelector _windowSelector;

    private List<WindowItem> _windows = new List<WindowItem>(); // 管理するウィンドウのリスト
    private int _targetWindowIndex = 0; // 現在のターゲットウィンドウのインデックス

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

    // デスクトップやウィンドウ操作に関する外部関数のインポート

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

    }

    // Settings menu item click handler
    private void OpenSettings(object sender, EventArgs e)
    {
        // Open the settings form
        var settingsForm = new SettingsForm();
        settingsForm.Show();
    }

    public void Run()
    {
        MouseHook.MouseAction += async (sender, e) => await OnMouseActionAsync(sender, e);
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

    private void MoveForeGroundWindow(IntPtr hwnd){
        if(false){
            SetForegroundWindow(hwnd);
        }
        else{
             SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
               SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
    
        }

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

        width = Math.Max(width, MinWindowWidth);
        width = Math.Min(width, workingArea.Width - MinWindowWidth);
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
        }
        else
        {
            x = (int)(workingArea.X + (workingArea.Width - width));
        }

        SetWindowPos(hWnd, (IntPtr)HWND_TOP, x, y, width, height, 0x0040);
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

        // ウィンドウの元の比率を計算
        double originalLeftWindowRatio = (double)leftWindowRect.Width / (leftWindowRect.Width + rightWindowRect.Width);
        double originalRightWindowRatio = 1 - originalLeftWindowRatio;



        // ウィンドウを交換して元の比率を維持            


        MoveWindowByRatio(leftWindowRoot, originalRightWindowRatio, false);
        MoveWindowByRatio(rightWindowRoot, originalLeftWindowRatio, true);
    }


    private (IntPtr leftWindowRoot, IntPtr rightWindowRoot) GetLeftAndRightWindow(int monitorIndex)
    {
        if (monitorIndex < 0 || monitorIndex >= Screen.AllScreens.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(monitorIndex), "Invalid monitor index.");
        }


        Rectangle workingArea = GetMonitorWorkingArea(monitorIndex);


        int acceptableWidth = workingArea.Width - MinWindowWidth;

        int leftWindowX = workingArea.X + 30;
        int rightWindowX = workingArea.X + workingArea.Width - 120;
        int y = (workingArea.Y + workingArea.Height) / 2;

        IntPtr leftWindowHandle = WindowFromPoint(new Point(leftWindowX, y));
        IntPtr rightWindowHandle = WindowFromPoint(new Point(rightWindowX, y));
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
            if (IsSmall(windowWidth, rcNormalPosition.Bottom - rcNormalPosition.Top) || windowWidth > acceptableWidth)
            {
                leftWindowHandle = IntPtr.Zero;
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
            if (IsSmall(windowWidth, rcNormalPosition.Bottom - rcNormalPosition.Top) || windowWidth > acceptableWidth)
            {
                rightWindowRoot = IntPtr.Zero;
            }
        }

        System.Diagnostics.Debug.WriteLine("left");
        System.Diagnostics.Debug.WriteLine(leftWindowRoot);
        System.Diagnostics.Debug.WriteLine("right");
        System.Diagnostics.Debug.WriteLine(rightWindowRoot);

        return (leftWindowRoot, rightWindowRoot);
    }



    public async Task<List<WindowItem>> GetAllWindowsOnCurrentDesktopAsync(int monitorIndex)
    {
        return await Task.Run(() =>
        {
            if (monitorIndex < 0 || monitorIndex >= Screen.AllScreens.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(monitorIndex), "Invalid monitor index.");
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
                System.Diagnostics.Debug.WriteLine(title.ToString());

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


    private void ResizeWindows(IntPtr leftWindowRoot, IntPtr rightWindowRoot, int newLeftWindowWidth, int newRightWindowWidth, int monitorIndex)
    {
        Rectangle workingArea = GetMonitorWorkingArea(monitorIndex);
        newLeftWindowWidth = Math.Min(newLeftWindowWidth, workingArea.Width);
        newRightWindowWidth = Math.Min(newRightWindowWidth, workingArea.Width);

        SetWindowPos(leftWindowRoot, (IntPtr)HWND_TOP, workingArea.X, workingArea.Y, newLeftWindowWidth, workingArea.Height, 0x0040);
        SetWindowPos(rightWindowRoot, (IntPtr)HWND_TOP, workingArea.X + newLeftWindowWidth, workingArea.Y, newRightWindowWidth, workingArea.Height, 0x0040);
    }

    // マウス操作時のイベント5
    private async Task OnMouseActionAsync(object sender, ExtendedMouseEventArgs e)
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
            _targetWindowIndex = 0;
            _windowSelector.Hide();

            // transParentedWindowsをループする
            // 透過を解除する
            foreach (var handle in transparentedWindows)
            {


                // SetWindowTransparency(handle, 255);

            }
            transparentedWindows.Clear();


        }
        if (e.ReleasedButton == MouseButtons.XButton1)
        {
            isBackButtonPressed = false;
            CompleteResize();
        }
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

    private void HandleBackButtonDown(ExtendedMouseEventArgs e, IntPtr hwnd, int monitorIndex)
    {
        if (hwnd == IntPtr.Zero) return;

        switch (e.PressedButton)
        {
            case MouseButtons.Left:
                SnapWindow(hwnd, monitorIndex, true);
                break;
            case MouseButtons.Right:
                SnapWindow(hwnd, monitorIndex, false);
                break;
            case MouseButtons.Middle:
                switch (MiddleBackButtonClickAction)
                {
                    case
                        ButtonAction.SWAP_WINDOWS:
                        SwapWindows(monitorIndex);
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


        if (e.Delta != 0)
        {
            if (isBackButtonPressed == false)
            {
                isBackButtonPressed = true;
                var (leftWindowRoot, rightWindowRoot) = GetLeftAndRightWindow(monitorIndex);
                MoveForeGroundWindow(leftWindowRoot);
                // MoveForeGroundWindow(rightWindowRoot);
                context = new ResizingContext
                {
                    LeftWindow = leftWindowRoot,
                    RightWindow = rightWindowRoot,
                    ExtendPixel = GetExtendPixel(monitorIndex),
                    MonitorWidth = GetMonitorWorkingArea(monitorIndex).Width,
                    MonitorIndex = monitorIndex
                };
               MoveForeGroundWindow(rightWindowRoot);
            }
            // TODO
            
           
            // MoveForeGroundWindow(context.RightWindow);

            ResizeWindowBasedOnDelta(e, context);
        }
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

            // SetWindowTransparency(leftWindowRoot, 200);
            // SetWindowTransparency(rightWindowRoot, 200);

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
                break;
            case MouseButtons.Right:
                SnapWindow(hwnd, monitorIndex, false);
                break;
            case MouseButtons.Middle:

                switch (MiddleForwardButtonClickAction)
                {
                    case ButtonAction.MINIMIZE_WINDOW:
                        MinimizeWindow(hwnd);


                        break;

                    case ButtonAction.CLOSE_WINDOW:
                        CloseWindow(hwnd);
                        System.Threading.Thread.Sleep(120);
                        if (!IsWindow(hwnd))
                        {
                            _windowSelector.deleteWindow(hwnd);

                        }
                        break;


                    default:
                        break;
                }


                break;


        }
    }



    public static void SetWindowTransparency(IntPtr hwnd, byte transparency)
    {
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED);
        SetLayeredWindowAttributes(hwnd, 0, transparency, LWA_ALPHA);
    }

    private void FlashWindow(IntPtr hWnd)
    {
        FLASHWINFO fi = new FLASHWINFO();
        fi.cbSize = Convert.ToUInt32(Marshal.SizeOf(fi));
        fi.hwnd = hWnd;
        fi.dwFlags = 3; // Flash both the window caption and taskbar button
        fi.uCount = uint.MaxValue; // Keep flashing until the window comes to the foreground
        fi.dwTimeout = 0;

        if (!FlashWindowEx(ref fi))
        {
        }
    }
    // TODO削除
    public static string GetWindowTitle(IntPtr hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        StringBuilder sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
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

    private void ResizeWindowBasedOnDelta(ExtendedMouseEventArgs e, ResizingContext context)
    {
        System.Diagnostics.Debug.WriteLine("delta:" + e.Delta);
        if (e.Delta > 0)
        {
            // 右のウィンドウを拡大
            if (context.LeftWindow == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("LeftWindowHandle is null");
                return;
            }
            int newRightWindowWidth = GetNewWindowWidth(context.RightWindow, context.ExtendPixel);
            UpdateWindowWidthsAndResize(e, context, newRightWindowWidth);
        }
        else
        {
            // 左のウィンドウを拡大
            if (context.RightWindow == IntPtr.Zero)
            {
                return;
            }
            int newLeftWindowWidth = GetNewWindowWidth(context.LeftWindow, context.ExtendPixel);
            UpdateWindowWidthsAndResize(e, context, newLeftWindowWidth);
        }
    }


    private int GetNewWindowWidth(IntPtr windowHandle, int extendPixel)
    {
        GetWindowRect(windowHandle, out RECT windowRect);
        return (int)(windowRect.Width + extendPixel);
    }


    private void UpdateWindowWidthsAndResize(ExtendedMouseEventArgs e, ResizingContext context, int newWindowWidth)
    {
        int newOtherWindowWidth = context.MonitorWidth - newWindowWidth;
        if (newOtherWindowWidth < MinWindowWidth)
        {
            System.Diagnostics.Debug.WriteLine("newOtherWindowWidth is too small");
            return;
        }

        if (e.Delta > 0)
        {
            ResizeWindows(context.LeftWindow, context.RightWindow, newOtherWindowWidth, newWindowWidth, context.MonitorIndex);
        }
        else
        {
            ResizeWindows(context.LeftWindow, context.RightWindow, newWindowWidth, newOtherWindowWidth, context.MonitorIndex);
        }
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
        // width とheightの積が 10000 以下なら小さいと判定する
        // return width * height <= 1000;
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

