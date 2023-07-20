using System.Drawing;
using System.Windows.Forms;

public class DummyWindow : Form
{
    private PictureBox iconPictureBox;


    public DummyWindow(int x, int y, int width, int height)
    {
        this.Enabled = false;
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ShowInTaskbar = false;
        this.StartPosition = FormStartPosition.Manual;
        this.SetBounds(x, y, width, height);
        this.Padding = new Padding(5);

            this.Opacity = 0.9;

        this.iconPictureBox = new PictureBox();
        this.iconPictureBox.Size = new Size(100, 100);
        this.iconPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        this.Controls.Add(this.iconPictureBox);
    }

    public void DisplayIcon(IntPtr windowHandle)
    {
        Icon icon = IconManager.Instance.ExtractIconFromWindowHandle(windowHandle);

        if (icon != null)
        {
            this.iconPictureBox.Image = icon.ToBitmap();
        }
    }


    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        int borderWidth = 5;
        Color borderColor = Color.Black; // Change this to the color you want for the border
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
                                borderColor, borderWidth, ButtonBorderStyle.Solid,
                                borderColor, borderWidth, ButtonBorderStyle.Solid,
                                borderColor, borderWidth, ButtonBorderStyle.Solid,
                                borderColor, borderWidth, ButtonBorderStyle.Solid);
    }


    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (this.iconPictureBox != null) // check if iconPictureBox is not null
        {
            this.iconPictureBox.Location = new Point((this.ClientSize.Width - iconPictureBox.Width) / 2,
                                                      (this.ClientSize.Height - iconPictureBox.Height) / 2); // Update the position when the form is resized
        }
        this.Invalidate();
    }
    public IntPtr WindowHandle
    {
        get { return this.Handle; }
    }
}
