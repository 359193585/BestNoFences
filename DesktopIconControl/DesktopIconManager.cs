using System;
using System.Runtime.InteropServices;

namespace DesktopIconControl
{
    public static class DesktopIconManager
    {
        #region Win32

        private const int LVM_FIRST = 0x1000;
        private const int LVM_SETEXTENDEDLISTVIEWSTYLE = LVM_FIRST + 54;
        private const int LVM_ARRANGE = LVM_FIRST + 22;
        private const int LVS_AUTOARRANGE = 0x0100;
        private const int LVS_EX_SNAPTOGRID = 0x00080000;

        private const int GWL_STYLE = -16;

        private const int WM_COMMAND = 0x111;
        private const int ID_VIEW_AUTOARRANGE = 0x7011;
        private const int ID_VIEW_ALIGNTOGRID = 0x7012;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(
            IntPtr parentHandle,
            IntPtr childAfter,
            string lpszClass,
            string lpszWindow);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        #endregion

      
        private static IntPtr GetDesktopListView()
        {
            IntPtr progman = FindWindow("Progman", null);
            IntPtr shellViewWin = IntPtr.Zero;

            IntPtr desktopWnd = IntPtr.Zero;
            while ((desktopWnd = FindWindowEx(IntPtr.Zero, desktopWnd, "WorkerW", null)) != IntPtr.Zero)
            {
                shellViewWin = FindWindowEx(desktopWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellViewWin != IntPtr.Zero)
                    break;
            }

            if (shellViewWin == IntPtr.Zero)
            {
                shellViewWin = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            }

            if (shellViewWin == IntPtr.Zero)
                return IntPtr.Zero;

            return FindWindowEx(shellViewWin, IntPtr.Zero, "SysListView32", "FolderView");
        }

        
        public static void EnableAutoArrange()
        {
            IntPtr listView = GetDesktopListView();
            if (listView == IntPtr.Zero)
                return;

            int style = GetWindowLong(listView, GWL_STYLE);
            style |= LVS_AUTOARRANGE;
            SetWindowLong(listView, GWL_STYLE, style);
        }

        public static void EnableSnapToGrid()
        {
            IntPtr listView = GetDesktopListView();
            if (listView == IntPtr.Zero)
                return;

            SendMessage(
                listView,
                LVM_SETEXTENDEDLISTVIEWSTYLE,
                (IntPtr)LVS_EX_SNAPTOGRID,
                (IntPtr)LVS_EX_SNAPTOGRID);
        }

       
        public static void ArrangeIconsLeft()
        {
            EnableAutoArrange();
            EnableSnapToGrid();
        }
        public static void EnableAutoArrangeReal()
        {
            IntPtr listView = GetDesktopListView();
            if (listView == IntPtr.Zero)
                return;

            SendMessage(listView, WM_COMMAND,
                (IntPtr)ID_VIEW_AUTOARRANGE,
                IntPtr.Zero);
        }

        public static void EnableSnapToGridReal()
        {
            IntPtr listView = GetDesktopListView();
            if (listView == IntPtr.Zero)
                return;

            SendMessage(listView, WM_COMMAND,
                (IntPtr)ID_VIEW_ALIGNTOGRID,
                IntPtr.Zero);
        }

        public static void ArrangeToLeft()
        {
            IntPtr listView = GetDesktopListView();
            if (listView == IntPtr.Zero) return;

            int style = GetWindowLong(listView, GWL_STYLE);
            style |= LVS_AUTOARRANGE;
            SetWindowLong(listView, GWL_STYLE, style);

            SendMessage(listView,
                LVM_SETEXTENDEDLISTVIEWSTYLE,
                (IntPtr)LVS_EX_SNAPTOGRID,
                (IntPtr)LVS_EX_SNAPTOGRID);

            SendMessage(listView,
                LVM_ARRANGE,
                IntPtr.Zero,
                IntPtr.Zero);
        }
    }
}