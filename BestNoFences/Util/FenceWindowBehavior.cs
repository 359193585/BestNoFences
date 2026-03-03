using Fenceless.Model;
using System;
using System.Drawing;
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
        /// <summary>
        /// handle windos message
        /// </summary>
        /// <returns>true: do not call base.WndProc; false: need call base.WnProc</returns>
        public bool ProcessMessage(ref Message m, FenceWindowBehaviorContent ctx)
        {
            if (ctx == null) return false;

            switch ((uint)m.Msg)
            {
                case WM_NCHITTEST:
                    return ProcessHitTest(ref m, ctx);

                case WM_SYSCOMMAND:
                    return ProcessSysCommand(ref m, ctx);

                case WM_SHOWWINDOW:
                    return ProcessShowWindow(ref m, ctx);

                case WM_SIZE:
                    return ProcessSize(ref m, ctx);

                case WM_WINDOWPOSCHANGING:
                    return ProcessWindowPosChanging(ref m, ctx);

                case WM_SETFOCUS:
                    return ProcessSetFocus(ref m, ctx);

                case WM_DISPLAYCHANGE:
                    return ProcessDisplayChange(ref m, ctx);

                case WM_DPICHANGED:
                    return ProcessDisplayChange(ref m, ctx);

                case WM_NCMOUSELEAVE:
                    return ProcessNcMouseLeave(ref m, ctx);

                default:
                    return false; // other mesg, let the base class handle
            }
        }
        #region  method of process message
        private bool ProcessHitTest(ref Message m, FenceWindowBehaviorContent ctx)
        {
            // Don't allow form dragging if we're dragging an item
            if (ctx.IsDraggingItem)
            {
                m.Result = (IntPtr)HTCLIENT;
                return false;
            }
            // Allow dragging and resizing
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

        private bool ProcessSysCommand(ref Message m, FenceWindowBehaviorContent ctx)
        {
            int command = m.WParam.ToInt32() & 0xFFF0;
            if (command == SC_MAXIMIZE || command == SC_MINIMIZE)
            {
                m.Result = IntPtr.Zero;
                return true;
            }
            return false; 
        }

        private bool ProcessShowWindow(ref Message m, FenceWindowBehaviorContent ctx)
        {
            if (m.WParam == IntPtr.Zero) //hide command
            {
                if (!ctx.IsAutoHidden && !ctx.IsDisposed)
                {
                    m.Result = IntPtr.Zero;
                    return true;
                }
            }
            return false;
        }
        private bool ProcessSize(ref Message m, FenceWindowBehaviorContent ctx)
        {
            if (m.WParam.ToInt32() == SIZE_MINIMIZED)
            {
                m.Result = IntPtr.Zero;
                return true;
            }
            return false;
        }
        private bool ProcessWindowPosChanging(ref Message m, FenceWindowBehaviorContent ctx)
        {
            var wp = Marshal.PtrToStructure<WINDOWPOS>(m.LParam);

            if ((wp.flags & HideWindowFlag) != 0)
            {
                if (!ctx.IsAutoHidden && !ctx.IsDisposed)
                {
                    wp.flags &= ~HideWindowFlag;
                    Marshal.StructureToPtr(wp, m.LParam, false);
                    return false; 
                }
            }
            return false;
        }
        private bool ProcessSetFocus(ref Message m, FenceWindowBehaviorContent ctx)
        {
            SetWindowPos(_targetForm.Handle, HWND_BOTTOM, 0, 0, 0, 0,
                SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
            return false;  // let the base class handle，windows can get focus
        }

        private bool ProcessDisplayChange(ref Message m, FenceWindowBehaviorContent ctx)
        {
            // new screen resolution or dpi changed
            FenceManager.Instance.SizeAllFenceCenter();
            return false; 
        }
        private bool ProcessNcMouseLeave(ref Message m, FenceWindowBehaviorContent ctx)
        {
            var myrect = new Rectangle(new Point(_fenceInfo.PosX, _fenceInfo.PosY),
                new Size(_fenceInfo.Width, _fenceInfo.Height));

            if (!myrect.IntersectsWith(new Rectangle(ctx.MousePos, new Size(1, 1))))
            {
                // Minify();
            }

            return false; // let the base class handle mouse leave

        }
        #endregion

    }
}
