using System.Drawing;

namespace Fenceless.Util
{
    // Render context object, containing the current interaction state of the form
    public class FencePaintContext
    {
        // Drawing object and basic layout parameters
        public Graphics Graphics { get; set; }
        public Rectangle ClientRectangle { get; set; }
        public string WindowText { get; set; }
        public int TitleHeight { get; set; }
        public int TitleOffset { get; set; }
        public int ScrollOffset { get; set; }
        public int ItemWidth { get; set; }
        public int TextHeight { get; set; }

        // Interaction state: mouse position and scroll height feedback
        public Point MousePos { get; set; }

        // Drag related:
        public bool IsDragging { get; set; }
        public string DraggingItemPath { get; set; } 
        public int DragTargetIndex { get; set; }
        public Point DragCurrentPoint { get; set; }

        // Selection and hover state feedback
        public string SelectedItem { get; set; }
        public string HoveringItem { get; set; }
        public bool ShouldUpdateSelection { get; set; }
        public bool ShouldRunDoubleClick { get; set; }
        public bool HasSelectionUpdated { get; set; }
        public bool HasHoverUpdated { get; set; }
        public string NewSelectedItem { get; set; }
        public string NewHoveringItem { get; set; }
        public int NewScrollHeight { get; set; } 

    }
}