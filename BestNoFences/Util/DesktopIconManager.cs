using Fenceless.Util;
using Fenceless.Win32;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class DesktopIconManager
{
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
    public static Size GetDesktopIconSize()
    {
        IntPtr shellView = GetDesktopListViewHandle();
        int width = WindowUtil.GetSystemMetrics(WindowUtil.SM_CXICON);  // SM_CXICON
        int height = WindowUtil.GetSystemMetrics(WindowUtil.SM_CYICON); // SM_CYICON
        return new System.Drawing.Size(width, height);
    }
    public static Size GetDesktopIconSpacing()
    {
        IntPtr desktopWindow = WindowUtil.GetDesktopWindow();
        IntPtr listViewHandle = WindowUtil.FindWindowEx(desktopWindow, IntPtr.Zero, "SysListView32", null);

        if (listViewHandle != IntPtr.Zero)
        {
            IntPtr result = WindowUtil.SendMessage(listViewHandle, WindowUtil.LVM_GETITEMSPACING, new IntPtr(1), IntPtr.Zero);
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
            WindowUtil.SendMessage(hDesktopListView, WindowUtil.LVM_ARRANGE, (IntPtr)WindowUtil.LVA_ALIGNLEFT, IntPtr.Zero);
        }
        DesktopRegistryManager.SetAutoArrange(true);
        DesktopRegistryManager.SetAlignToGrid(true);
        DesktopRegistryManager.RefreshDesktop();
    }
    public static void ArrangeIconsToRight()
    {
        IntPtr hDesktopListView = GetDesktopListViewHandle();
        if (hDesktopListView != IntPtr.Zero)
        {
            // send LVM_ARRANGE, set all icon align right win10/11 can not run
            WindowUtil.SendMessage(hDesktopListView, WindowUtil.LVM_ARRANGE, (IntPtr)WindowUtil.LVA_ALIGNRIGHT, IntPtr.Zero);
        }
        DesktopRegistryManager.SetAutoArrange(true);
        DesktopRegistryManager.SetAlignToGrid(true);
        DesktopRegistryManager.RefreshDesktop();
    }
    
    public  Rectangle EstimateIconsArea(out Size iconSize, out Size iconSpaceSize)
    {
        ArrangeIconsToLeft();
        iconSize = GetDesktopIconSize();
        iconSpaceSize = GetDesktopIconSpacing();

        IntPtr hDesktopListView = GetDesktopListViewHandle();
        if (hDesktopListView == IntPtr.Zero)
            return Rectangle.Empty;

        int iconCount = (int)WindowUtil.SendMessage(hDesktopListView, WindowUtil.LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
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
        IntPtr hProgman = WindowUtil.FindWindow("Progman", "Program Manager");
        IntPtr hShellView = WindowUtil.FindWindowEx(hProgman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (hShellView == IntPtr.Zero)
        {
            hShellView = WindowUtil.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "SHELLDLL_DefView", null);
        }
        return WindowUtil.FindWindowEx(hShellView, IntPtr.Zero, "SysListView32", "FolderView");
    }
}

public static class DesktopRegistryManager
{
    private const string DESKTOP_REGISTRY_PATH = @"Software\Microsoft\Windows\Shell\Bags\1\Desktop";

    public static bool SetAutoArrange(bool enable)
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(DESKTOP_REGISTRY_PATH, true))
            {
                if (key != null)
                {
                    // FFNESI: auto arrange  (1=true, 0=false)
                    key.SetValue("FFNESI", enable ? 1 : 0, Microsoft.Win32.RegistryValueKind.DWord);
                    RefreshDesktop();
                    return true;
                }
            }
        }
        catch
        {
        }
        return false;
    }

    public static bool SetAlignToGrid(bool enable)
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(DESKTOP_REGISTRY_PATH, true))
            {
                if (key != null)
                {
                    // IconSpacingX and  IconSpacingY eq  -1 , align to grid
                    int spacingValue = enable ? -1 : 112; 

                    key.SetValue("IconSpacing", spacingValue, Microsoft.Win32.RegistryValueKind.DWord);
                    key.SetValue("IconSpacingHorizontal", spacingValue, Microsoft.Win32.RegistryValueKind.DWord);
                    key.SetValue("IconSpacingVertical", spacingValue, Microsoft.Win32.RegistryValueKind.DWord);
                    RefreshDesktop();
                    return true;
                }
            }
        }
        catch
        {
        }
        return false;
    }

    public static bool GetAutoArrangeStatus()
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(DESKTOP_REGISTRY_PATH, false))
            {
                if (key != null)
                {
                    var value = key.GetValue("FFNESI");
                    if (value != null && value is int intValue)
                    {
                        return intValue == 1;
                    }
                }
            }
        }
        catch
        {
        }
        return false;
    }

    public static void RefreshDesktop()
    {
        try
        {
            IntPtr hDesktop = WindowUtil.FindWindow("Progman", "Program Manager");
            if (hDesktop != IntPtr.Zero)
            {
                RedrawWindow(hDesktop, IntPtr.Zero, IntPtr.Zero,
                    RDW_INVALIDATE | RDW_ERASE | RDW_FRAME | RDW_ALLCHILDREN | RDW_UPDATENOW | RDW_ERASENOW);
            }
        }
        catch
        {
        }
    }
    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);
    private const uint WM_SYSCOMMAND = 0x0112;
    private const uint SC_MONITORPOWER = 0xF170;
    private const uint WM_SETTINGCHANGE = 0x001A;

    // repaint 
    private const uint RDW_INVALIDATE = 0x0001;
    private const uint RDW_ERASE = 0x0004;
    private const uint RDW_FRAME = 0x0400;
    private const uint RDW_ALLCHILDREN = 0x0080;
    private const uint RDW_UPDATENOW = 0x0100;
    private const uint RDW_ERASENOW = 0x0200;

}