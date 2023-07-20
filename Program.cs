
using System;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    static Mutex mutex = new Mutex(true, "{9D3407F8-B49C-4A2C-983F-D1C965CDC956}");

    [STAThread]
    static void Main()
    {
        if (mutex.WaitOne(TimeSpan.Zero, true))
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            var trayApp = new SplitScreenApp();
            trayApp.Run();

            Application.Run();
            mutex.ReleaseMutex();
        }
        else
        {
            MessageBox.Show("Application is already running.");
        }
    }
}