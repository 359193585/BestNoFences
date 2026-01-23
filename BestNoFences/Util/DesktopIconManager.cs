using Fenceless.Util;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class DesktopIconManager
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    private const int SM_CXICON = 11;
    private const int SM_CYICON = 12;
    private const int LVM_FIRST = 0x1000;
    private const int LVM_GETITEMSPACING = LVM_FIRST + 51;
    private const uint LVM_ARRANGE = LVM_FIRST + 22;
    private const uint LVM_GETITEMCOUNT = LVM_FIRST + 4;
    private const uint LVA_ALIGNLEFT = 0x0001; // left alignment

    private readonly Logger logger;

    [Guid("1af3a467-213b-42c5-83e0-47844020a173"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFolderView
    {
        [PreserveSig] int GetSpacing(ref POINT pPt); 
        [PreserveSig] int GetViewMode(out uint puViewMode);
    }
    [StructLayout(LayoutKind.Sequential)]
  
    public struct POINT { public int x; public int y; }
    public DesktopIconManager()
    {
        logger = Logger.Instance;

    }
    public static System.Drawing.Size GetDesktopIconSize()
    {
        IntPtr shellView = GetDesktopListViewHandle();
        int width = GetSystemMetrics(SM_CXICON);  // SM_CXICON
        int height = GetSystemMetrics(SM_CYICON); // SM_CYICON
        return new System.Drawing.Size(width, height);
    }
    public static Size GetDesktopIconSpacing()
    {
        IntPtr desktopWindow = GetDesktopWindow();
        IntPtr listViewHandle = FindWindowEx(desktopWindow, IntPtr.Zero, "SysListView32", null);

        if (listViewHandle != IntPtr.Zero)
        {
            IntPtr result = SendMessage(listViewHandle, LVM_GETITEMSPACING, new IntPtr(1), IntPtr.Zero);
            int spacing = result.ToInt32();
            int width = spacing & 0xFFFF;       
            int height = (spacing >> 16) & 0xFFFF; 

            return new Size(width, height);
        }
        else
        {
            //  if retrieval fails, return a reasonable default value (e.g., 96x68)
            return new Size(96, 68);
        }
    }

    

    public static void ArrangeIconsToLeft()
    {
        IntPtr hDesktopListView = GetDesktopListViewHandle();
        if (hDesktopListView != IntPtr.Zero)
        {
            // send LVM_ARRANGE, set all icon align left win10/11 can not run
            SendMessage(hDesktopListView, LVM_ARRANGE, (IntPtr)LVA_ALIGNLEFT, IntPtr.Zero);
        }
    }

    public  Rectangle EstimateIconsArea(out Size iconSize, out Size iconSpaceSize)
    {
        ArrangeIconsToLeft();
        iconSize = GetDesktopIconSize();
        iconSpaceSize = GetDesktopIconSpacing();

        IntPtr hDesktopListView = GetDesktopListViewHandle();
        if (hDesktopListView == IntPtr.Zero)
            return Rectangle.Empty;

        int iconCount = (int)SendMessage(hDesktopListView, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
        if (iconCount == 0)
            return Rectangle.Empty;

        Rectangle screenArea = Screen.PrimaryScreen.WorkingArea;
        int iconsPerColumn = screenArea.Height / (iconSpaceSize.Height);
        //logger.Info($"get iconSize:{iconSize.Width},{iconSize.Height}  ", "DesktopIconManager");
        //logger.Info($"get iconSpaceSize: {iconSpaceSize.Width},{iconSpaceSize.Height}  ", "DesktopIconManager");

        int needCol = (iconCount + iconsPerColumn - 1) / iconsPerColumn;
        needCol += 1;
        int areaWidth = (iconSpaceSize.Width) * needCol; 

        return new Rectangle(screenArea.Left, screenArea.Top, areaWidth, screenArea.Height);
    }

    public  Rectangle GetUsableScreenArea()
    {
        Rectangle screenArea = Screen.PrimaryScreen.WorkingArea;
        Size iconSize;
        Size iconSpaceSize;
        Rectangle iconsArea = EstimateIconsArea(out  iconSize, out iconSpaceSize);
        //logger.Info($"get icons area in desktop {iconsArea.X},{iconsArea.Y},{iconsArea.Width},{iconsArea.Height}  ", "DesktopIconManager");
        if (iconsArea.IsEmpty)
            return screenArea;

        return new Rectangle(
            x: iconsArea.Right,
            y: screenArea.Top,
            width: screenArea.Width - iconsArea.Width - iconSpaceSize.Width,
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