using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class SnapManager
{
    private SnapManager() { }
    public static SnapManager Instance { get; } = new SnapManager();

    private readonly List<Form> _forms = new();

    public int SnapDistance { get; set; } = 10;
    public int HorizontalCompensation { get; set; } = 7;
    public int VerticalCompensation { get; set; } = 7;

    #region Win32

    public const int WM_MOVING = 0x0216;
    public const int WM_SIZING = 0x0214;

    public const int WMSZ_LEFT = 1;
    public const int WMSZ_RIGHT = 2;
    public const int WMSZ_TOP = 3;
    public const int WMSZ_BOTTOM = 6;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom, Height, Width;

        public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
        public static RECT FromRectangle(Rectangle r) => new RECT
        {
            Left = r.Left,
            Top = r.Top,
            Right = r.Right,
            Bottom = r.Bottom,
            Height = r.Bottom - r.Top,
            Width = r.Right - r.Left,
        };
        public override string ToString()
        {
            return $"L:{Left} T:{Top} R:{Right} B:{Bottom} W:{Width} H:{Height}";
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    private const int SM_CXSIZEFRAME = 32;
    private const int SM_CYSIZEFRAME = 33;
    private const int SM_CXPADDEDBORDER = 92;

    #endregion

    #region 注册

    public void Register(Form form)
    {
        if (!_forms.Contains(form))
            _forms.Add(form);
    }

    public void Unregister(Form form)
    {
        _forms.Remove(form);
    }

    #endregion

    #region 主入口

    public void HandleSnapOrResize(Form movingForm, int? edge, ref RECT rect)
    {
        Rectangle moving = rect.ToRectangle();

        foreach (var target in _forms)
        {
            if (target == movingForm) continue;

            Rectangle targetRect = GetDwmVisibleRect(target);

            if (edge == null)
                SnapMove(ref moving, targetRect);
            else
                SnapResize(ref moving, targetRect, edge.Value);
        }

        rect = RECT.FromRectangle(moving);
    }

    #endregion

    #region Snap Logic

    private void SnapMove(ref Rectangle moving, Rectangle target)
    {
        // snap to left/right
        if (VerticalOverlap(moving, target))
        {
            if (Near(moving.Left, target.Right))
            {
                moving.X = target.Right - HorizontalCompensation;
                //than top/bottom align
                if (Near(moving.Top, target.Top))
                {
                    moving.Y = target.Top;
                }
                if (Near(moving.Bottom, target.Bottom))
                {
                    moving.Y = moving.Bottom - moving.Height;
                }
            }

            else if (Near(moving.Right, target.Left))
            {
                moving.X = target.Left - moving.Width + HorizontalCompensation - 1;
                //than top/bottom align
                if (Near(moving.Top, target.Top))
                {
                    moving.Y = target.Top;
                }
                if (Near(moving.Bottom, target.Bottom))
                {
                    moving.Y = moving.Bottom - moving.Height;
                }
            }

        }

        // snap to top/bottom
        if (HorizontalOverlap(moving, target))
        {
            if (Near(moving.Top, target.Bottom))
            {
                moving.Y = target.Bottom - (VerticalCompensation - 7);

                //than try left/right align
                if (Near(moving.Left, target.Left))
                {
                    moving.X = target.X - HorizontalCompensation;
                }
                if (Near(moving.Right, target.Right))
                {
                    moving.X = target.Right - moving.Width + HorizontalCompensation;
                }
            }

            else if (Near(moving.Bottom, target.Top))
            {
                moving.Y = target.Top - moving.Height + VerticalCompensation;
            }
        }
    }

    private void SnapResize(ref Rectangle moving, Rectangle target, int edge)
    {
        switch (edge)
        {
            case WMSZ_TOP:
                if (HorizontalOverlap(moving, target) && Near(moving.Top, target.Bottom))
                {
                    int newTop = target.Bottom - VerticalCompensation + 6;
                    moving.Height = moving.Bottom - newTop;
                    moving.Y = newTop;
                }
                break;

            case WMSZ_BOTTOM:
                if (HorizontalOverlap(moving, target) && Near(moving.Bottom, target.Top))
                {
                    moving.Height = target.Top - moving.Top + VerticalCompensation + 1;
                }
                break;

            case WMSZ_LEFT:
                if (VerticalOverlap(moving, target) && Near(moving.Left, target.Right))
                {
                    int newLeft = target.Right - HorizontalCompensation;
                    moving.Width = moving.Right - newLeft;
                    moving.X = newLeft;
                }
                break;

            case WMSZ_RIGHT:
                if (VerticalOverlap(moving, target) && Near(moving.Right, target.Left))
                {
                    moving.Width = target.Left - moving.Left + HorizontalCompensation;
                }
                break;
        }
    }

    #endregion

    #region Utils

    private bool Near(int a, int b) => Math.Abs(a - b) <= SnapDistance;

    private bool HorizontalOverlap(Rectangle a, Rectangle b)
    {
        int tolerance = 20;
        return a.Right > b.Left - tolerance && a.Left < b.Right + tolerance;
    }
    private bool VerticalOverlap(Rectangle a, Rectangle b)
    {
        int tolerance = 20;
        return a.Bottom > b.Top - tolerance && a.Top < b.Bottom + tolerance;
    }

    private Rectangle GetDwmVisibleRect(Form form)
    {
        DwmGetWindowAttribute(form.Handle, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT r, Marshal.SizeOf<RECT>());
        return r.ToRectangle();
    }

    #endregion
}