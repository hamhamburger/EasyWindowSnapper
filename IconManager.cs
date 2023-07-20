using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.ComponentModel;

using System.Runtime.InteropServices;

public class IconManager
{

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private static IconManager instance;
    private Dictionary<IntPtr, uint> handleProcessIdCache = new Dictionary<IntPtr, uint>();
    private Dictionary<uint, Icon> processIdIconCache = new Dictionary<uint, Icon>();

    private Icon transparentIcon;


    private IconManager()
    {
        Bitmap bitmap = new Bitmap(1, 1);
        bitmap.SetPixel(0, 0, Color.Transparent);

        handleProcessIdCache = new Dictionary<IntPtr, uint>();
        processIdIconCache = new Dictionary<uint, Icon>();
        transparentIcon = Icon.FromHandle(bitmap.GetHicon());
    }



    public static IconManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new IconManager();
            }
            return instance;
        }
    }

    public Icon ExtractIconFromWindowHandle(IntPtr windowHandle)
    {
        uint processId = 0;
        if (!handleProcessIdCache.TryGetValue(windowHandle, out processId))
        {
            GetWindowThreadProcessId(windowHandle, out processId); // This is a Windows API function
            handleProcessIdCache[windowHandle] = processId;
        }
        if (processId == 0)
        {
            throw new ArgumentException("Could not find process associated with window handle");
        }
        Icon icon = null;
        if (!processIdIconCache.TryGetValue(processId, out icon))
        {
            try
            {
                Process process = Process.GetProcessById((int)processId);
                string executablePath = process.MainModule.FileName;
                icon = Icon.ExtractAssociatedIcon(executablePath);
                processIdIconCache[processId] = icon;
            }
            catch (Win32Exception e) when (e.NativeErrorCode == 5) // Access Denied error
            {
                icon = transparentIcon; // Use pre-generated transparent icon
                processIdIconCache[processId] = icon;
            }
        }
        return icon;
    }
}
