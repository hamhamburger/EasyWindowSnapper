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



    private Dictionary<IntPtr, IntPtr> iconCache = new Dictionary<IntPtr, IntPtr>();

    public const int WM_GETICON = 0x007f;
    public const int ICON_SMALL2 = 2;
    public const int ICON_BIG = 1;
    public const int ICON_SMALL = 0;

    public static string IDI_APPLICATION = "#32512";
    public const int GCL_HICONSM = -34;
    public const int GCL_HICON = -14;


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
        _targetIndex = 0;

        listView = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            HideSelection = true,
            SmallImageList = new ImageList() { ImageSize = new Size((int)(32 * 1.5), (int)(32 * 1.5)) }
        };
        listView.Columns.Add("Icon", (int)(100 * 1.5), HorizontalAlignment.Left);
        listView.Columns.Add("Title", (int)(300 * 1.5), HorizontalAlignment.Left);

        int itemHeight = (int)(17 * 3);
        int headerHeight = (int)(20 * 3);
        listView.Height = MaxDisplayItems * itemHeight + headerHeight;

        int totalColumnWidth = 0;
        foreach (ColumnHeader column in listView.Columns)
        {
            totalColumnWidth += column.Width;
        }
        listView.Width = totalColumnWidth;

        UpdateListView();

        this.ClientSize = listView.Size;
        this.FormBorderStyle = FormBorderStyle.None;
        this.ShowInTaskbar = false;
        this.TopMost = true;
        this.StartPosition = FormStartPosition.CenterScreen;

        this.Controls.Add(listView);


        this.ClientSize = new Size(listView.Width, listView.Height);

    }

    public void UpdateWindows(List<WindowItem>? windows)
    {
        _windows = windows ?? new List<WindowItem>();
        UpdateListView();
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
        UpdateListView();
    }


    private void UpdateListView()
    {
        listView.Items.Clear();
        listView.SmallImageList.Images.Clear();

        for (int i = 0; i < _windows.Count; i++)
        {
            var window = _windows[i];
            IntPtr hIcon = GetWindowIcon(window.Handle);
            if (hIcon == IntPtr.Zero)
            {
                hIcon = GetClassLongPtr(window.Handle, GCL_HICON);
            }
            if (hIcon == IntPtr.Zero)
            {
                hIcon = LoadIcon(IntPtr.Zero, IDI_APPLICATION);
            }
            listView.SmallImageList.Images.Add(Icon.FromHandle(hIcon));

            var lvi = new ListViewItem(new[] { "", window.Title })
            {
                ImageIndex = i
            };
            listView.Items.Add(lvi);
        }

        listView.SmallImageList.ColorDepth = ColorDepth.Depth32Bit;

        UpdateHighlight();
    }



    // パフォーマンス重視版
    // private void UpdateListView()
    // {
    //     for (int i = 0; i < _windows.Count; i++)
    //     {
    //         var window = _windows[i];
    //         if (i >= listView.Items.Count)
    //         {
    //             var lvi = new ListViewItem(new[] { "", window.Title })
    //             {
    //                 ImageIndex = i
    //             };
    //             listView.Items.Add(lvi);
    //         }
    //         IntPtr hIcon = GetWindowIconCached(window.Handle);
    //         listView.SmallImageList.Images.Add(Icon.FromHandle(hIcon));
    //     }
    //     listView.SmallImageList.ColorDepth = ColorDepth.Depth32Bit;
    //     UpdateHighlight();
    // }

    private void UpdateHighlight()
    {
        listView.SelectedItems.Clear();
        for (int i = 0; i < listView.Items.Count; i++)
        {
            ListViewItem item = listView.Items[i];
            if (i == _targetIndex)
            {
                item.BackColor = Color.LightBlue;

                // Get the rectangle of the listView item
                var itemRect = listView.Items[i].Bounds;

                // Calculate the new location and size for the preview
                // var previewLocation = new Point(itemRect.Right + 5, itemRect.Top);
                // var previewSize = new Size(itemRect.Width, itemRect.Height);

                // Show the window preview with the new location and size
                // this.previewForm.ShowWindowPreview(_windows[i].Handle, previewLocation, previewSize);

                item.EnsureVisible();
            }
            else
            {
                item.BackColor = listView.BackColor;
            }
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

