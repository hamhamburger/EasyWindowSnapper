using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using WinSplit;
using System.Drawing.Imaging;

public partial class WindowSelector : Form
{

    [DllImport("user32.dll", SetLastError = false)]
    static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetClassLong")]
    public static extern uint GetClassLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]



    public static extern IntPtr GetClassLong64(IntPtr hWnd, int nIndex);

    public static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 4)
            return new IntPtr(GetClassLong32(hWnd, nIndex));
        else
            return GetClassLong64(hWnd, nIndex);
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr LoadIcon(IntPtr hInstance, string lpIconName);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    private DataGridView dgvWindows;


    private Bitmap splitLeftIconBitmap;
    private Bitmap splitRightIconBitmap;
    private Bitmap transparentIconBitmap;
    private Dictionary<IntPtr, IntPtr> iconCache = new Dictionary<IntPtr, IntPtr>();

    public const int WM_GETICON = 0x007f;
    public const int ICON_SMALL2 = 2;
    public const int ICON_BIG = 1;
    public const int ICON_SMALL = 0;

    public static string IDI_APPLICATION = "#32512";
    public const int GCL_HICONSM = -34;
    public const int GCL_HICON = -14;

    private static int RowHeight => AppSettings.Instance.RowHeight;
    private static int MaxDisplayRows => AppSettings.Instance.MaxDisplayRows;

    private static bool IsDarkMode => AppSettings.Instance.IsDarkMode;


    private Bitmap ResizeIconBitmap(Icon icon, int width, int height)
    {
        Bitmap originalBitmap = icon.ToBitmap();
        Bitmap resizedBitmap = new Bitmap(originalBitmap, new Size(width, height));
        return resizedBitmap;
    }



    public IntPtr GetWindowIcon(IntPtr hWnd)
    {
        IntPtr hIcon = default(IntPtr);

        hIcon = SendMessage(hWnd, WM_GETICON, new IntPtr(ICON_SMALL2), IntPtr.Zero);
        if (hIcon == IntPtr.Zero)
        {
            hIcon = SendMessage(hWnd, WM_GETICON, new IntPtr(ICON_BIG), IntPtr.Zero);
        }
        if (hIcon == IntPtr.Zero)
        {
            hIcon = SendMessage(hWnd, WM_GETICON, new IntPtr(ICON_SMALL), IntPtr.Zero);
        }
        if (hIcon == IntPtr.Zero)
        {
            hIcon = GetClassLongPtr(hWnd, GCL_HICONSM);
        }
        if (hIcon == IntPtr.Zero)
        {
            hIcon = GetClassLongPtr(hWnd, GCL_HICON);
        }
        if (hIcon == IntPtr.Zero)
        {
            hIcon = LoadIcon(IntPtr.Zero, IDI_APPLICATION);
        }
        return hIcon;
    }


    public IntPtr GetWindowIconCached(IntPtr hWnd)
    {
        if (iconCache.ContainsKey(hWnd))
        {
            return iconCache[hWnd];
        }
        else
        {
            IntPtr icon = GetWindowIcon(hWnd);
            iconCache[hWnd] = icon;
            return icon;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }


    private List<WindowItem>? _windows;
    private int _targetIndex;
    public WindowSelector(List<WindowItem>? windows) : base()
    {
        // ウィンドウのリストを設定する。リストがnullの場合は新たに作成する。
        _windows = windows ?? new List<WindowItem>();
        _targetIndex = 0;

        // デバッグモードでダークモードのステータスを表示する
        System.Diagnostics.Debug.WriteLine(IsDarkMode);

        // フォームの基本設定を行う
        ConfigureForm();

        // DataGridView (dgvWindows)の設定を行う
        ConfigureDataGridView();

        // DataGridViewの行、セル、列の設定を行う
        ConfigureDataGridViewRowsAndCells();

        // DataGridViewにアイコンとタイトルの列を追加する
        AddColumnsToDataGridView();

        // アイコンの設定を行う
        SetupIcons();

        // ウィンドウコントロールを追加する
        this.Controls.Add(dgvWindows);

        // このウィンドウを最前面に保つ
        this.TopMost = true;

        // コントロールのスタイルを設定する
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    private void ConfigureForm()
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.Enabled = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ShowInTaskbar = false;
    }

    private void ConfigureDataGridView()
    {
        dgvWindows = new DataGridView
        {
            ColumnHeadersVisible = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            RowTemplate = { Height = RowHeight },
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            Font = new Font("Microsoft Sans Serif", 18.0f, FontStyle.Regular, GraphicsUnit.Pixel),
            ScrollBars = ScrollBars.None,
            BackgroundColor = IsDarkMode ? Color.Black : Color.White,
            ForeColor = IsDarkMode ? Color.White : Color.Black,
            CellBorderStyle = DataGridViewCellBorderStyle.None
        };

        int dgvPaddingAndMargin = dgvWindows.Margin.Top + dgvWindows.Margin.Bottom + dgvWindows.Padding.Top + dgvWindows.Padding.Bottom;
        dgvWindows.Size = new Size(800, RowHeight * MaxDisplayRows + dgvPaddingAndMargin);
        this.Size = new Size(800, RowHeight * MaxDisplayRows + dgvPaddingAndMargin + this.Padding.Top + this.Padding.Bottom);
        dgvWindows.Rows.Clear();
    }

    private void ConfigureDataGridViewRowsAndCells()
    {
        // セルの色設定
        dgvWindows.RowsDefaultCellStyle.BackColor = IsDarkMode ? Color.Black : Color.White;
        dgvWindows.RowsDefaultCellStyle.ForeColor = IsDarkMode ? Color.White : Color.Black;
        dgvWindows.CellBorderStyle = DataGridViewCellBorderStyle.None;
        dgvWindows.GridColor = IsDarkMode ? Color.White : Color.Black;

        // 選択時のハイライト色
        dgvWindows.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#ece1e1");
        dgvWindows.DefaultCellStyle.SelectionForeColor = dgvWindows.DefaultCellStyle.ForeColor;
    }

    private void AddColumnsToDataGridView()
    {
        // アイコン、タイプ、タイトルの列をDataGridViewに追加
        DataGridViewImageColumn iconColumn = new DataGridViewImageColumn
        {
            Name = "icon",
            HeaderText = "Icon",
            ImageLayout = DataGridViewImageCellLayout.Normal,
            Width = RowHeight,
        };
        dgvWindows.Columns.Add(iconColumn);


        DataGridViewTextBoxColumn titleColumn = new DataGridViewTextBoxColumn
        {
            Name = "title",
            HeaderText = "Title",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };
        dgvWindows.Columns.Add(titleColumn);
    }

    private void SetupIcons()
    {
        // アイコンパスを設定し、アイコンを読み込む
        string leftIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons/split_left.ico");
        string rightIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons/split_right.ico");

        Icon splitLeftIcon = new Icon(leftIconPath);
        Icon splitRightIcon = new Icon(rightIconPath);

        splitLeftIconBitmap = ResizeIconBitmap(splitLeftIcon, RowHeight, RowHeight);
        splitRightIconBitmap = ResizeIconBitmap(splitRightIcon, RowHeight, RowHeight);

        transparentIconBitmap = new Bitmap(1, 1);
        transparentIconBitmap.MakeTransparent();
    }
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        IntPtr desktopHandle = GetDesktopWindow();
        SetForegroundWindow(desktopHandle);
    }

    public void UpdateWindows(List<WindowItem>? windows)
    {
        _windows = windows ?? new List<WindowItem>();

        UpdateDataGridView();
    }
    public void deleteWindow(IntPtr handle)
    {
        for (int i = 0; i < _windows.Count; i++)
        {
            if (_windows[i].Handle == (IntPtr)handle)
            {
                _windows.RemoveAt(i);
                break;
            }
        }
        UpdateDataGridView();
    }
public void UpdateDataGridView()
{
    Size standardIconSize = new Size(RowHeight, RowHeight);

    dgvWindows.Rows.Clear();
    foreach (var window in new List<WindowItem>(_windows))
    {
        Bitmap resizedIcon;
        try
        {
            IntPtr hIcon = GetWindowIconCached(window.Handle);
            Icon appIcon = Icon.FromHandle(hIcon);
            Bitmap bitmap = appIcon.ToBitmap();

            // Resize the icon to the standard icon size
            resizedIcon = new Bitmap(bitmap, standardIconSize);
        }
        catch (ArgumentException)
        {
            resizedIcon = new Bitmap(transparentIconBitmap, standardIconSize);
        }

        Bitmap typeIcon = transparentIconBitmap;

        if (window.type == WindowItemType.LEFT)
        {
            typeIcon = splitLeftIconBitmap;
        }
        else if (window.type == WindowItemType.RIGHT)
        {
            typeIcon = splitRightIconBitmap;
        }

        // Scale typeIcon to fit resizedIcon
        typeIcon = new Bitmap(typeIcon, standardIconSize);

        float transparency = 0.8f;
        Bitmap transparentTypeIcon = new Bitmap(typeIcon.Width, typeIcon.Height);
        using (Graphics g = Graphics.FromImage(transparentTypeIcon))
        {
            ColorMatrix colorMatrix = new ColorMatrix();
            colorMatrix.Matrix33 = transparency;
            ImageAttributes attributes = new ImageAttributes();
            attributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

            g.DrawImage(typeIcon, new Rectangle(0, 0, typeIcon.Width, typeIcon.Height),
                0, 0, typeIcon.Width, typeIcon.Height, GraphicsUnit.Pixel, attributes);
        }

        // Combine icons
        using (Graphics g = Graphics.FromImage(resizedIcon))
        {
            g.DrawImage(transparentTypeIcon, new Point(0, 0));
        }

        // Create cells
        var appImage = new DataGridViewImageCell() { Value = resizedIcon };
        var textCell = new DataGridViewTextBoxCell() { Value = window.Title };

        dgvWindows.Rows.Add(new object[] { appImage.Value, textCell.Value });
    }
    dgvWindows.ClearSelection();
    UpdateHighlight();
}
    private void UpdateHighlight()
    {

        // Ensure that there are windows to select from
        if (_windows == null || _windows.Count == 0)
        {
            return;
        }

        // Ensure _targetIndex is within the valid range
        if (_targetIndex >= 0 && _targetIndex < dgvWindows.Rows.Count)
        {
            // Highlight the new row
            System.Diagnostics.Debug.WriteLine("OK");
            dgvWindows.ClearSelection();  // Clear any previous selection
            dgvWindows.Rows[_targetIndex].Selected = true;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("UpdateHighlight _targetIndex:" + _targetIndex + " is out of range");
            _targetIndex = 0;

            // Highlight the first row
            if (dgvWindows.Rows.Count > 0)
            {
                dgvWindows.ClearSelection();  // Clear any previous selection
                dgvWindows.Rows[_targetIndex].Selected = true;
            }
        }
    }
    public void ResetIndex()
    {
        _targetIndex = 0;
        UpdateHighlight();
    }

    public void SetFormToMonitor(int monitorIndex)
    {
        if (monitorIndex >= 0 && monitorIndex < Screen.AllScreens.Length)
        {
            var targetScreen = Screen.AllScreens[monitorIndex];
            this.Location = new Point(
                targetScreen.WorkingArea.Left + targetScreen.WorkingArea.Width / 2 - this.Width / 2,
                targetScreen.WorkingArea.Top + targetScreen.WorkingArea.Height / 2 - this.Height / 2
            );
        }
        else // 指定したインデックスのモニターが存在しない場合、主モニターにフォームを表示
        {
            var targetScreen = Screen.PrimaryScreen;
            this.Location = new Point(
                targetScreen.WorkingArea.Left + targetScreen.WorkingArea.Width / 2 - this.Width / 2,
                targetScreen.WorkingArea.Top + targetScreen.WorkingArea.Height / 2 - this.Height / 2
            );
        }

    }

    public WindowItem SelectNextWindow()
    {
        if (_windows.Count == 0)
        {
            return new WindowItem();
        }

        _targetIndex = (_targetIndex + 1) % _windows.Count;
        UpdateHighlight();
        System.Diagnostics.Debug.WriteLine("SelectNextWindow _targetIndex:" + _targetIndex);
        return _windows[_targetIndex];
    }


    public WindowItem SelectPreviousWindow()
    {
        if (_windows.Count == 0)
        {
            return new WindowItem();
        }

        _targetIndex = (_targetIndex - 1 + _windows.Count) % _windows.Count;
        UpdateHighlight();

        return _windows[_targetIndex];
    }

    public WindowItem GetCurrentlySelectedWindow()
    {
        if (_windows.Count == 0)
        {
            return new WindowItem();
        }

        return _windows[_targetIndex];
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DropSHADOW = 0x00020000;
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= CS_DropSHADOW;
            return cp;
        }
    }

}

