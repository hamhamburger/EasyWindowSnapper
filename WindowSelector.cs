using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using WinSplit;

public partial class WindowSelector : Form
{
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
    // private Icon splitLeftIcon;
    // private Icon splitRightIcon;


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

    // private PreviewForm previewForm;
    private const int MaxDisplayItems = 15;
    private List<WindowItem>? _windows;
    private int _targetIndex;
    private ListView listView;
    // private PictureBox previewBox;
    public WindowSelector(List<WindowItem>? windows)
    {
        _windows = windows ?? new List<WindowItem>();
        System.Diagnostics.Debug.WriteLine($"Number of windows: {_windows.Count}");

        _targetIndex = 0;
        this.Size = new Size(800, 800); // Adjust as needed
        this.FormBorderStyle = FormBorderStyle.None; // Hide title bar
        this.Enabled = false; // Disable mouse and keyboard interactions
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ShowInTaskbar = false;
        



        dgvWindows = new DataGridView
        {
            Size = new Size(800, 800),
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            RowHeadersVisible = false,
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
             Font = new Font("Microsoft Sans Serif", 18.0f, FontStyle.Regular, GraphicsUnit.Pixel)
        };
        dgvWindows.ColumnHeadersVisible = false;
        dgvWindows.Rows.Clear();

        // Create and add icon column
        DataGridViewImageColumn iconColumn = new DataGridViewImageColumn
        {
            Name = "icon",
            HeaderText = "Icon",
            ImageLayout = DataGridViewImageCellLayout.Normal, // Keep the icon as it is
            Width = 60  // Set a fixed width for the icon column
        };
        dgvWindows.Columns.Add(iconColumn);

        // Create and add type column
        DataGridViewImageColumn typeColumn = new DataGridViewImageColumn
        {
            Name = "type",
            HeaderText = "Type",
            ImageLayout = DataGridViewImageCellLayout.Normal, // Keep the icon as it is
            Width = 60  // Set a fixed width for the type column
        };
        dgvWindows.Columns.Add(typeColumn);

        // Create and add title column
        DataGridViewTextBoxColumn titleColumn = new DataGridViewTextBoxColumn
        {
            Name = "title",
            HeaderText = "Title",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill // Set the title column to fill the remaining space
        };
        dgvWindows.Columns.Add(titleColumn);

        string leftIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons/split_left.ico");
        string rightIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons/split_right.ico");

        Icon splitLeftIcon = new Icon(leftIconPath);
        Icon splitRightIcon = new Icon(rightIconPath);

        // splitLeftIconBitmap = splitLeftIcon.ToBitmap();
        // splitRightIconBitmap = splitRightIcon.ToBitmap();
        splitLeftIconBitmap = ResizeIconBitmap(splitLeftIcon, 60, 60);
        splitRightIconBitmap = ResizeIconBitmap(splitRightIcon, 60, 60);

        transparentIconBitmap = new Bitmap(1, 1);
        transparentIconBitmap.MakeTransparent();


        this.Controls.Add(dgvWindows);

        this.TopMost = true;
    }

    public void UpdateWindows(List<WindowItem>? windows)
    {
        _windows = windows ?? new List<WindowItem>();
        System.Diagnostics.Debug.WriteLine($"Number of windows: {_windows.Count}");

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
        Size standardIconSize = new Size(48, 48);

        dgvWindows.Rows.Clear();
        foreach (var window in _windows)
        {
            // TODO パフォーマンス向上
            IntPtr hIcon = GetWindowIconCached(window.Handle);
            Icon appIcon = Icon.FromHandle(hIcon);
            Bitmap bitmap = appIcon.ToBitmap();

            // Resize the icon to the standard icon size
            Bitmap resizedIcon = new Bitmap(bitmap, standardIconSize);

            // Create cells
            var appImage = new DataGridViewImageCell() { Value = resizedIcon };
            var textCell = new DataGridViewTextBoxCell() { Value = window.Title };

            Bitmap typeIcon = transparentIconBitmap;

            if (window.type == WindowItemType.LEFT)
            {
                typeIcon = splitLeftIconBitmap;
            }
            else if (window.type == WindowItemType.RIGHT)
            {
                typeIcon = splitRightIconBitmap;
            }

            var typeImageCell = new DataGridViewImageCell() { Value = typeIcon };

            dgvWindows.Rows.Add(new object[] { appImage.Value, typeImageCell.Value, textCell.Value });
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

        // Clear the current selection in the DataGridView
        dgvWindows.ClearSelection();

        // Ensure _targetIndex is within the valid range
        if (_targetIndex >= 0 && _targetIndex < dgvWindows.Rows.Count)
        {
            // Highlight the new cell
            dgvWindows.CurrentCell = dgvWindows.Rows[_targetIndex].Cells[2]; // 2 is the title column
        }
        else
        {
            // Reset _targetIndex to 0 if it's out of range
            _targetIndex = 0;

            // Highlight the first cell
            if (dgvWindows.Rows.Count > 0)
            {
                dgvWindows.CurrentCell = dgvWindows.Rows[_targetIndex].Cells[2]; // 2 is the title column
            }
        }

        // Ensure the selected row is visible
        if (_targetIndex >= 0 && _targetIndex < dgvWindows.Rows.Count)
        {
            dgvWindows.FirstDisplayedScrollingRowIndex = _targetIndex;
        }
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

    // The remaining methods and classes are not shown here for brevity.
}

