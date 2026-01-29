using Fenceless.Model;
using System;
using System.Drawing;
using System.IO;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Header;

namespace Fenceless.Util
{
    public enum ClickActionResult { DragItem, DragForm, ItemRemoved, None }

    public class FenceInteractionHandler
    {
        private readonly FenceInfo _fenceInfo;
        private readonly Logger _logger;
        private const int DragThreshold = 5; // Minimum pixels to start dragging

        // Internal drag state fields 
        public bool IsDraggingItem { get; private set; }
        public string DraggingItemPath { get; private set; }
        public Point DragStartPoint { get; private set; }
        public Point DragCurrentPoint { get; private set; }
        public int DragTargetIndex { get; private set; } = -1;
        public Rectangle DraggingTargetRect { get; private set; }

        public FenceInteractionHandler(FenceInfo info, Logger logger)
        {
            _fenceInfo = info;
            _logger = logger;
        }
        // Handle business logic determination on MouseDown (integrates lock state and file validity check)
        public ClickActionResult HandleMouseDown(Point pos, bool isLocked, int scrollOffset, int titleHeight, int itemWidth, int textHeight, int windowWidth, out string targetPath)
        {
            targetPath = null;
            string hitPath = GetItemAtPosition(pos, scrollOffset, titleHeight, itemWidth, textHeight, windowWidth);

            // If not locked and an icon is clicked
            if (!isLocked && hitPath != null)
            {
                if (File.Exists(hitPath) || Directory.Exists(hitPath))
                {
                    targetPath = hitPath;
                    return ClickActionResult.DragItem;
                }
                else
                {
                    // File is missing, automatically remove it from the list and save
                    _logger.Warning($"Item no longer exists, removing from fence: {hitPath}", "InteractionHandler");
                    _fenceInfo.Files.Remove(hitPath);
                    Save();
                    return ClickActionResult.ItemRemoved;
                }
            }

            // If locked and no icon is clicked, or unlocked but clicked on empty space, allow dragging the form
            if (hitPath == null)
            {
                return ClickActionResult.DragForm;
            }

            return ClickActionResult.None;
        }
        // Get item at position: determines if an icon exists at the specified coordinates
        public string GetItemAtPosition(Point pos, int scrollOffset, int titleHeight, int itemWidth, int textHeight, int windowWidth)
        {
            int spacing = _fenceInfo.ItemSpacing;
            int actualW = Math.Max(_fenceInfo.IconSize + 10, itemWidth);
            int actualH = _fenceInfo.IconSize + textHeight + 10;
            int x = spacing;
            int y = spacing;

            foreach (var file in _fenceInfo.Files)
            {
                var itemRect = new Rectangle(x, y + titleHeight - scrollOffset, actualW, actualH);
                if (itemRect.Contains(pos)) return file;

                x += actualW + spacing;
                if (x + actualW > windowWidth) { x = spacing; y += actualH + spacing; }
            }
            return null;
        }
        // Get grid position index: converts physical coordinates into file list index
        public int GetGridPositionIndex(Point pos, int scrollOffset, int titleHeight, int itemWidth, int textHeight, int windowWidth)
        {
            int spacing = _fenceInfo.ItemSpacing;
            int actualW = Math.Max(_fenceInfo.IconSize + 10, itemWidth);
            int actualH = _fenceInfo.IconSize + textHeight + 10;
            int itemsPerRow = Math.Max(1, (windowWidth - spacing) / (actualW + spacing));

            // Convert coordinates to content area
            int relX = pos.X - spacing;
            int relY = pos.Y - titleHeight + scrollOffset - spacing;

            int col = Math.Max(0, Math.Min(relX / (actualW + spacing), itemsPerRow - 1));
            int row = Math.Max(0, relY / (actualH + spacing));

            int index = row * itemsPerRow + col;
            // Clamp index within legal range (Files.Count - 1 is key)
            return Math.Max(0, Math.Min(index, _fenceInfo.Files.Count - 1));
        }
        // Execute position swap logic
        public bool CompleteDragReorder(string draggingItem, Point dropLocation, int scrollOffset, int titleHeight, int itemWidth, int textHeight, int windowWidth)
        {
            if (string.IsNullOrEmpty(draggingItem)) return false;

            int sourceIdx = _fenceInfo.Files.IndexOf(draggingItem);
            int targetIdx = GetGridPositionIndex(dropLocation, scrollOffset, titleHeight, itemWidth, textHeight, windowWidth);

            if (sourceIdx != -1 && sourceIdx != targetIdx)
            {
                var item = _fenceInfo.Files[sourceIdx];
                _fenceInfo.Files.RemoveAt(sourceIdx);

                if (targetIdx >= _fenceInfo.Files.Count)
                    _fenceInfo.Files.Add(item);
                else
                    _fenceInfo.Files.Insert(targetIdx, item);

                Save();
                _logger.Info($"Successfully reordered '{Path.GetFileName(draggingItem)}' to index {targetIdx}", "InteractionHandler");
                return true;
            }
            return false;
        }
        // Handle core business logic for double-click events
        public bool HandleDoubleClick(Point mousePos, string currentSelectedItem, int scrollOffset, int titleHeight, int itemWidth, int textHeight, int windowWidth, out string updatedSelectedItem)
        {
            updatedSelectedItem = currentSelectedItem;
            string hitPath = GetItemAtPosition(mousePos, scrollOffset, titleHeight, itemWidth, textHeight, windowWidth);
            if (hitPath != null && hitPath == currentSelectedItem)
            {
                if (File.Exists(hitPath) || Directory.Exists(hitPath))
                {
                    var entry = FenceEntry.FromPath(hitPath);
                    if (entry != null)
                    {
                        _logger.Info($"Double-clicked item '{Path.GetFileName(hitPath)}' in fence '{_fenceInfo.Name}'", "InteractionHandler");
                        entry.Open();
                    }
                    return false; 
                }
                else
                {
                    // File doesn't exist: execute cleanup 
                    _logger.Warning($"Double-clicked item no longer exists, removing: {hitPath}", "InteractionHandler");
                    _fenceInfo.Files.Remove(hitPath);
                    updatedSelectedItem = null; // Reset selected item
                    Save();
                    return true; // Tell the form a refresh is needed
                }
            }

            return false;
        }
        public void HandleCancelDrag(string draggingItemPath)
        {
            if (string.IsNullOrEmpty(draggingItemPath)) return;
            _logger.Debug($"Cancelled drag operation for item '{Path.GetFileName(draggingItemPath)}' in fence '{_fenceInfo.Name}'", "InteractionHandler");
        }
        public bool ShouldStartDragging(Point startPoint, Point currentPoint)
        {
            double distance = Math.Sqrt(
                Math.Pow(startPoint.X - currentPoint.X, 2) +
                Math.Pow(startPoint.Y - currentPoint.Y, 2));
            return distance > DragThreshold;
        }
        // Initialize drag starting point (but don't start dragging immediately)
        public void PrepareDrag(string itemPath, Point startLocation)
        {
            DraggingItemPath = itemPath;
            DragStartPoint = startLocation;
            IsDraggingItem = false; // Initially false to avoid interfering with double-click
        }
        // Handle target index updates during mouse movement
        public void ProcessMouseMove(Point currentPos, int scrollOffset, int titleHeight, int itemWidth, int textHeight, int windowWidth)
        {
            if (!IsDraggingItem) return;
            DragCurrentPoint = currentPos;
            if (IsDraggingItem)
            {
                DragTargetIndex = GetGridPositionIndex(currentPos, scrollOffset, titleHeight, itemWidth, textHeight, windowWidth);


                //int columns = Math.Max(1, windowWidth / itemWidth);
                //int absX = currentPos.X;
                //int absY = currentPos.Y + scrollOffset - titleHeight;

                //int row = absY / textHeight;
                //int col = absX / itemWidth;
                //DragTargetIndex = (row * columns) + col;
                //DragTargetIndex = Math.Max(0, Math.Min(DragTargetIndex, _fenceInfo.Files.Count - 1));

                //int itemHeight = _fenceInfo.IconSize + textHeight + 10;
                //int targetX = (DragTargetIndex % columns) * itemWidth;
                //int targetY = (DragTargetIndex / columns) * itemHeight + titleHeight - scrollOffset;

                //DraggingTargetRect = new Rectangle(targetX, targetY, itemWidth, itemHeight);
            }

        }
        public void ResetDragState()
        {
            IsDraggingItem = false;
            DraggingItemPath = null;
            DragTargetIndex = -1;
        }
        // Determine if displacement requirements for starting a drag are met
        public bool ShouldStartItemDrag(Point currentLocation)
        {
            if (DraggingItemPath == null) return false;

            double distance = Math.Sqrt(Math.Pow(currentLocation.X - DragStartPoint.X, 2) + Math.Pow(currentLocation.Y - DragStartPoint.Y, 2));
            if (distance >= DragThreshold)
            {
                IsDraggingItem = true; // Formally enter dragging state
                return true;
            }
            return false;
        }
        private void Save() => FenceManager.Instance.UpdateFence(_fenceInfo);
        
    }
}