using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class DesktopIconManager
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [Guid("1af3a467-213b-42c5-83e0-47844020a173"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFolderView
    {
        [PreserveSig] int GetSpacing(ref POINT pPt); // 获取图标间距（含图标本身）
        [PreserveSig] int GetViewMode(out uint puViewMode);
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x; public int y; }
    public static System.Drawing.Size GetDesktopIconSize()
    {
        IntPtr shellView = GetDesktopListViewHandle();
        int width = GetSystemMetrics(11);  // SM_CXICON
        int height = GetSystemMetrics(12); // SM_CYICON
        return new System.Drawing.Size(width, height);
    }

   
    private const uint LVM_FIRST = 0x1000;
    private const uint LVM_ARRANGE = LVM_FIRST + 22;
    private const uint LVM_GETITEMCOUNT = LVM_FIRST + 4;
    private const uint LVA_ALIGNLEFT = 0x0001; // left alignment

    public static void ArrangeIconsToLeft()
    {
        IntPtr hDesktopListView = GetDesktopListViewHandle();
        if (hDesktopListView != IntPtr.Zero)
        {
            // send LVM_ARRANGE, set all icon align left win10/11 can not run
            SendMessage(hDesktopListView, LVM_ARRANGE, (IntPtr)LVA_ALIGNLEFT, IntPtr.Zero);
        }
    }

    public static Rectangle EstimateIconsArea()
    {
        ArrangeIconsToLeft(); 

        IntPtr hDesktopListView = GetDesktopListViewHandle();
        if (hDesktopListView == IntPtr.Zero)
            return Rectangle.Empty;

        int iconCount = (int)SendMessage(hDesktopListView, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
        if (iconCount == 0)
            return Rectangle.Empty;

        Rectangle screenArea = Screen.PrimaryScreen.WorkingArea;

        int singleIconWidth = 80;   
        int singleIconHeight = 90;  
        int iconsPerColumn = iconCount; 

        int areaWidth = singleIconWidth + 20; 

        int areaHeight = iconsPerColumn * singleIconHeight;

        areaHeight = Math.Min(areaHeight, screenArea.Height);

        return new Rectangle(screenArea.Left, screenArea.Top, areaWidth, areaHeight);
    }

    public static Rectangle GetUsableScreenArea()
    {
        Rectangle screenArea = Screen.PrimaryScreen.WorkingArea;
        Rectangle iconsArea = EstimateIconsArea();

        if (iconsArea.IsEmpty)
            return screenArea;

        return new Rectangle(
            x: iconsArea.Right,
            y: screenArea.Top,
            width: screenArea.Width - iconsArea.Width,
            height: screenArea.Height
        );
    }

    private static IntPtr GetDesktopListViewHandle()
    {
        IntPtr hProgman = FindWindow("Progman", "Program Manager");
        IntPtr hShellView = FindWindowEx(hProgman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (hShellView == IntPtr.Zero)
        {
            hShellView = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "SHELLDLL_DefView", null);
        }
        return FindWindowEx(hShellView, IntPtr.Zero, "SysListView32", "FolderView");
    }
}