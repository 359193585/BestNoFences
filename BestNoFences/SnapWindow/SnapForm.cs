using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class SnapForm : Form
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        SnapManager.Instance.Register(this);
        SnapManager.Instance.VerticalCompensation = 0;
        SnapManager.Instance.HorizontalCompensation = 0;
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SnapManager.Instance.Unregister(this);
        base.OnFormClosed(e);
    }
    protected override void WndProc(ref Message m)
    {
        HandleMethod(ref m);
        base.WndProc(ref m);
    }
    private void HandleMethod(ref Message m)
    {
        if (m.Msg == SnapManager.WM_MOVING)
        {
            var rectStruct = Marshal.PtrToStructure<SnapManager.RECT>(m.LParam);
            SnapManager.Instance.HandleSnapOrResize(this, null, ref rectStruct);
            Marshal.StructureToPtr(rectStruct, m.LParam, true);
        }
        else if (m.Msg == SnapManager.WM_SIZING)
        {
            var rectStruct = Marshal.PtrToStructure<SnapManager.RECT>(m.LParam);
            SnapManager.Instance.HandleSnapOrResize(this, (int)m.WParam, ref rectStruct);
            Marshal.StructureToPtr(rectStruct, m.LParam, true);
        }

    }
}