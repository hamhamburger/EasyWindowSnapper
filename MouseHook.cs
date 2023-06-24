using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;


// マウスのフック処理を行うクラス
public static class MouseHook
{

    // マウス操作イベントの定義
    public static event EventHandler<ExtendedMouseEventArgs> MouseAction = delegate { };
    public class ExtendedMouseEventArgs : EventArgs
{
    // プロパティの定義
    public MouseButtons? PressedButton { get; private set; }
    public MouseButtons? ReleasedButton { get; private set; } // 追加

    public int Delta { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public bool BackButtonDown { get; private set; }
    public bool ForwardButtonDown { get; private set; }

    // コンストラクタ
    public ExtendedMouseEventArgs(MouseButtons? pressedButton, MouseButtons? releasedButton, int delta, int x, int y, bool backButtonDown, bool forwardButtonDown)
    {
        PressedButton = pressedButton;
        ReleasedButton = releasedButton; // 追加
        Delta = delta;
        X = x;
        Y = y;
        BackButtonDown = backButtonDown;
        ForwardButtonDown = forwardButtonDown;
    }
}

    // 定数の定義
    private const int WH_MOUSE_LL = 14;

    public const uint MOUSEEVENTF_XDOWN = 0x0080;
    public const uint MOUSEEVENTF_XUP = 0x0100;
    private static uint[] XBUTTON1 = new uint[2] { 0x0001, 65536 };
    private static uint[] XBUTTON2 = new uint[2] { 0x0002, 131072 };
    private static bool otherButtonPressedWhileSideButtonPressed = false;

    public static event EventHandler BackButtonReleased = delegate { };

    public static bool IgnoreMouseAction { get; set; } = false;
    // Backボタンのプロパティ
    private static bool backButtonDown = false;
    public static bool BackButtonDown
    {
        get { return backButtonDown; }
        private set { backButtonDown = value; }
    }

    // Forwardボタンのプロパティ
    private static bool forwardButtonDown = false;
    public static bool ForwardButtonDown
    {
        get { return forwardButtonDown; }
        private set { forwardButtonDown = value; }
    }

    // プライベート変数
    private static LowLevelMouseProc _proc = HookCallback;
    private static IntPtr _hookID = IntPtr.Zero;

    // フックの開始
    public static void Start()
    {
        _hookID = SetHook(_proc);
    }

    // フックの停止
    public static void Stop()
    {
        UnhookWindowsHookEx(_hookID);
    }

    // フックの設定
    private static IntPtr SetHook(LowLevelMouseProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    // デリゲートの定義
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    // フックコールバック
    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        MouseMessages message = (MouseMessages)wParam;
        MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

        if (nCode >= 0)
        {
            // マウスメッセージを取得

            // Xボタンの押下判定
            if (message == MouseMessages.WM_XBUTTONDOWN)
            {

                if (Array.IndexOf(XBUTTON1, hookStruct.mouseData) >= 0)
                {
                    BackButtonDown = true;

                    return (IntPtr)1;
                }
                else if (Array.IndexOf(XBUTTON2, hookStruct.mouseData) >= 0)
                {



                    // Forward button down
                    ForwardButtonDown = true;

                    return (IntPtr)1;

                }
            }




            if (forwardButtonDown || backButtonDown)
            {

                MouseButtons? pressedButton = null;
                MouseButtons? releasedButton = null;
                int delta = 0;

                // 他のボタンの状態をチェック
                switch (message)
                {
                    case MouseMessages.WM_LBUTTONDOWN:
                        pressedButton = MouseButtons.Left;
                        otherButtonPressedWhileSideButtonPressed = true;
                        break;
                    case MouseMessages.WM_RBUTTONDOWN:
                        pressedButton = MouseButtons.Right;
                        otherButtonPressedWhileSideButtonPressed = true;
                        break;
                    case MouseMessages.WM_MBUTTONDOWN:
                        pressedButton = MouseButtons.Middle;
                        otherButtonPressedWhileSideButtonPressed = true;
                        break;
                    case MouseMessages.WM_MOUSEWHEEL:
                        delta = (short)((hookStruct.mouseData >> 16) & 0xffff);
                        otherButtonPressedWhileSideButtonPressed = true;
                        break;
                    case MouseMessages.WM_MBUTTONUP:
                        otherButtonPressedWhileSideButtonPressed = true;
                        break;
                    case MouseMessages.WM_XBUTTONUP:
                        if (Array.IndexOf(XBUTTON1, hookStruct.mouseData) >= 0)
                        {
                            BackButtonDown = false;
                            releasedButton = MouseButtons.XButton1;
                        }
                        if (Array.IndexOf(XBUTTON2, hookStruct.mouseData) >= 0)
                        {
                            ForwardButtonDown = false;
                            releasedButton = MouseButtons.XButton2;
                        }
                        if (otherButtonPressedWhileSideButtonPressed)
                        {
                            
                            otherButtonPressedWhileSideButtonPressed = false;
                            MouseAction?.Invoke(null, new ExtendedMouseEventArgs(null, releasedButton, delta, hookStruct.pt.x, hookStruct.pt.y, backButtonDown, forwardButtonDown));
                            return (IntPtr)1;
                        }
                        otherButtonPressedWhileSideButtonPressed = false;
                        break;
                }

                // マウスアクションイベントを発火
                if (pressedButton.HasValue || delta != 0)
                {
                    MouseAction?.Invoke(null, new ExtendedMouseEventArgs(pressedButton,null, delta, hookStruct.pt.x, hookStruct.pt.y, backButtonDown, forwardButtonDown));

                }


                // Suppress other mouse events while side button is pressed.
                if (wParam.ToInt32() == (int)MouseMessages.WM_MOUSEWHEEL ||
           wParam.ToInt32() == (int)MouseMessages.WM_LBUTTONDOWN ||
           wParam.ToInt32() == (int)MouseMessages.WM_RBUTTONDOWN ||
           wParam.ToInt32() == (int)MouseMessages.WM_MBUTTONDOWN ||
           wParam.ToInt32() == (int)MouseMessages.WM_MBUTTONUP ||
           wParam.ToInt32() == (int)MouseMessages.WM_LBUTTONUP ||
           wParam.ToInt32() == (int)MouseMessages.WM_RBUTTONUP
           )

                {
                    return (IntPtr)1;
                }
            }
        }
        if (message == MouseMessages.WM_XBUTTONUP)
        {
            if (Array.IndexOf(XBUTTON1, hookStruct.mouseData) >= 0)
            {
                // Check if no other button was pressed while the back button was down
                if (!otherButtonPressedWhileSideButtonPressed)
                {

                    KeyboardInput.PressBrowserBack();
                    KeyboardInput.ReleaseBrowserBack();

                    return (IntPtr)1;

                }

            }
                if (Array.IndexOf(XBUTTON2, hookStruct.mouseData) >= 0)
            {
                // Check if no other button was pressed while the back button was down
                if (!otherButtonPressedWhileSideButtonPressed)
                {


                    KeyboardInput.PressBrowserForward();
                    KeyboardInput.ReleaseBrowserForward();

                    return (IntPtr)1;

                }

            }

        }



        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }






// キーボード




    // その他の定数、構造体、およびメソッド定義は省略 // マウスメッセージの列挙型
    private enum MouseMessages

    {
        WM_LBUTTONDOWN = 0x0201,
        WM_LBUTTONUP = 0x0202,
        WM_MOUSEMOVE = 0x0200,
        WM_MOUSEWHEEL = 0x020A,
        WM_RBUTTONDOWN = 0x0204,
        WM_RBUTTONUP = 0x0205,
        WM_MBUTTONDOWN = 0x0207,
        WM_MBUTTONUP = 0x0208,
        WM_XBUTTONDOWN = 0x020B,
        WM_XBUTTONUP = 0x020C
    }

    // POINT構造体の定義
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);


    [StructLayout(LayoutKind.Explicit)]
    struct MOUSEKEYBDHARDWAREINPUT
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    const uint INPUT_MOUSE = 0;


}