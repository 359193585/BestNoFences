using Fenceless.Model;
using System;
using System.Drawing;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static Fenceless.Win32.WindowUtil;

namespace Fenceless.Util
{
    internal class FenceWindowBehavior
    {
        private readonly Form _targetForm;
        private readonly FenceInfo _fenceInfo;
        private bool _isDebugMode = false;

        public FenceWindowBehavior(Form form,FenceInfo fenceInfo)
        {
            _targetForm = form;
            _fenceInfo = fenceInfo;
        }
        public void ApplyStyles(bool isDebugMode)
        {
            _isDebugMode = isDebugMode;
            if (isDebugMode)
            {
                _targetForm.Opacity = 1.0;
                return;
            }

        }
        public bool ProcessMessage( ref Message m, FenceWindowBehaviorContent ctx)
        {
            if (ctx == null ) return false;

            // Then, allow dragging and resizing
            // If you comment out this section of code, it is easy to cause the form - flickering problem.
            if (m.Msg == WM_NCHITTEST)
            {

                // Don't allow form dragging if we're dragging an item
                if (ctx.IsDraggingItem)
                {
                    m.Result = (IntPtr)HTCLIENT;
                    return true;
                }
                var pt = _targetForm.PointToClient(new Point(m.LParam.ToInt32()));
                int borderSize = 10;

                if (pt.X < borderSize && pt.Y < borderSize)
                    m.Result = new IntPtr(HTTOPLEFT);
                else if (pt.X > (_targetForm.Width - borderSize) && pt.Y < borderSize)
                    m.Result = new IntPtr(HTTOPRIGHT);
                else if (pt.X < borderSize && pt.Y > (_targetForm.Height - borderSize))
                    m.Result = new IntPtr(HTBOTTOMLEFT);
                else if (pt.X > (_targetForm.Width - borderSize) && pt.Y > (_targetForm.Height - borderSize))
                    m.Result = new IntPtr(HTBOTTOMRIGHT);
                else if (pt.Y > (_targetForm.Height - borderSize))
                    m.Result = new IntPtr(HTBOTTOM);
                else if (pt.X < borderSize)
                    m.Result = new IntPtr(HTLEFT);
                else if (pt.X > (_targetForm.Width - borderSize))
                    m.Result = new IntPtr(HTRIGHT);
                return true;
            }
            // new screen resolution
            if (m.Msg == WM_DISPLAYCHANGE)
            {
                int newWidth = (int)m.LParam & 0xFFFF;  // lParam low 16 bits is width
                int newHeight = (int)m.LParam >> 16;    // lParam high 16 bits is height
                int colorDepth = (int)m.WParam;         // wParam means color depth
                FenceManager.Instance.SizeAllFence();
                //return true;
            }

            // 处理鼠标按下消息 - 不拦截，让基类处理
            if (m.Msg == WM_NCLBUTTONDOWN || m.Msg == WM_LBUTTONDOWN)
            {
                return false;  // 让基类处理
            }

            // 处理鼠标释放消息 - 不拦截，让基类处理
            if (m.Msg == WM_NCLBUTTONUP || m.Msg == WM_LBUTTONUP)
            {
                return false;  // 让基类处理
            }

            // 处理鼠标移动消息 - 不拦截，让基类处理
            if (m.Msg == WM_NCMOUSEMOVE || m.Msg == WM_MOUSEMOVE)
            {
                return false;  // 让基类处理
            }
            // Mouse leave
            var myrect = new Rectangle(new Point(_fenceInfo.PosX, _fenceInfo.PosY), new Size(_fenceInfo.Width,_fenceInfo.Height));
            if (m.Msg == 0x02a2 && !myrect.IntersectsWith(
                new Rectangle(ctx.MousePos,new Size(1, 1))))
            {
               // Minify();
            }

            // Prevent maximize/minimize
            if (m.Msg == WM_SYSCOMMAND)
            {
                var command = m.WParam.ToInt32() & 0xFFF0;
                if (command == SC_MAXIMIZE || command == SC_MINIMIZE)
                {
                    m.Result = IntPtr.Zero;
                    return true;
                }
                return false;
            }
            // 处理键盘消息 - 不拦截
            if (m.Msg == WM_KEYDOWN || m.Msg == WM_KEYUP)
            {
                return false;  // 让基类处理
            }
            // Prevent window from being hidden (Show Desktop)
            if (m.Msg == WM_SHOWWINDOW && m.WParam == IntPtr.Zero)
            {
                // Ignore hide commands unless we're auto-hiding or user is closing
                if (!ctx.IsAutoHidden && !ctx.IsDisposed)
                {
                    m.Result = IntPtr.Zero;
                    //return false;
                }
            }

            // Prevent window position changes that would hide the window (Show Desktop button)
            if (m.Msg == WM_WINDOWPOSCHANGING)
            {
                var wp = Marshal.PtrToStructure<WINDOWPOS>(m.LParam);

                // Check if the window is being moved off-screen or hidden
                if ((wp.flags & HideWindowFlag) != 0)
                {
                    // Remove the hide flag unless we're auto-hiding
                    if (!ctx.IsAutoHidden && !ctx.IsDisposed)
                    {
                        wp.flags &= ~HideWindowFlag;
                        Marshal.StructureToPtr(wp, m.LParam, false);
                        //return false;
                    }
                }
            }
            // By setting m.Result = IntPtr.Zero and returning, prevent the system from performing the default minimization operation.
            if (m.Msg == WM_SIZE && m.WParam.ToInt32() == SIZE_MINIMIZED)
            {
                //EnsureFenceVisible();
                m.Result = IntPtr.Zero;
                //return true;
            }

            if (m.Msg == WM_WINDOWPOSCHANGED)
            {
                //var wp = Marshal.PtrToStructure<WINDOWPOS>(m.LParam);
                //if ((wp.flags & HideWindowFlag) != 0 && !isAutoHidden && !IsDisposed)
                //{
                //    EnsureFenceVisible();
                //    m.Result = IntPtr.Zero;
                //    return;
                //}
            }

            if (m.Msg == WM_COMMAND)
            {
                //int commandId = m.WParam.ToInt32() & 0xFFFF;
                //if ((commandId == MIN_ALL || commandId == MIN_ALL_UNDO) && !isAutoHidden)
                //{
                //    EnsureFenceVisible();
                //    m.Result = IntPtr.Zero;
                //    return;
                //}
            }

            // Prevent foreground
            if (m.Msg == WM_SETFOCUS)
            {
                SendToDesktopBack();
                //return true;
            }

          
            return false;
        }
        private void SendToDesktopBack()
        {
            SetWindowPos(_targetForm.Handle, HWND_BOTTOM, 0, 0, 0, 0,
                SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        }
    }
}
