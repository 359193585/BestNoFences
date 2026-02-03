using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fenceless.Util
{
    internal class FenceWindowBehaviorContent
    {
        public bool IsDraggingItem { get; set; }
        public string DraggingItem { get; set; }
        public bool IsAutoHidden { get; set; }
        public bool IsDisposed { get; set; }
        public Point  MousePos { get; set; }
    }
}
