namespace WinSplit
{
    public class WindowItem
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; }

        public WindowItemType? type { get; set; }

    }


    public enum WindowItemType
    {
        LEFT,
        RIGHT,
    }


    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }


}
