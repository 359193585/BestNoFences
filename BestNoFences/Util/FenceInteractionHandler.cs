using Fenceless.Model;
using Fenceless.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using static Fenceless.Win32.WindowUtil;

namespace Fenceless.Util
{
    public enum ClickActionResult { DragItem, DragForm, ItemRemoved, None }

    public class FenceInteractionHandler
    {
        private readonly FenceInfo _fenceInfo;
        private readonly Logger _logger;
        private const int DragThreshold = 5; // Minimum pixels to start dragging

        // Internal drag state fields 
        public bool IsDraggingItem { get; private set; } = false;
        public string DraggingItemPath { get; private set; }
        public Point DragStartPoint { get; private set; }
        public Point DragCurrentPoint { get; private set; } = Point.Empty;
        public int DragTargetIndex { get; private set; } = -1;

        public FenceInteractionHandler(FenceInfo info, Logger logger)
        {
            _fenceInfo = info;
            _logger = logger;
        }
        #region drag item inter form
        public void StartItemDrag(string path, Point currentMousePos)
        {
            IsDraggingItem = true;
            DraggingItemPath = path;
            DragCurrentPoint = currentMousePos;
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

            var divisor = actualW + spacing;
            if (divisor <= 0) divisor = 1; 

            var itemsPerRow = Math.Max(1, (_fenceInfo.Width - spacing) / divisor);

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
            ResetDragState();
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
            DragTargetIndex = GetGridPositionIndex(currentPos, scrollOffset, titleHeight, itemWidth, textHeight, windowWidth);
        }
        public void ResetDragState()
        {
            IsDraggingItem = false;
            DraggingItemPath = null;
            DragTargetIndex = -1;
            DragCurrentPoint = Point.Empty;
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
        #endregion

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
        public void HandleExternalDrop(string[] dropedFiles)
        {
            try
            {
                var addedFiles = 0;
                _logger.Debug($"Processing {dropedFiles.Length} dropped files", "FenceWindow");

                foreach (var file in dropedFiles)
                {
                    if (_fenceInfo.Files.Contains(file) || !ItemExists(file))
                    {
                        _logger.Debug($"Skipped file (already exists or invalid): {file}", "FenceWindow");
                        continue;
                    }
                    string newFile = file;

                    if (Path.GetExtension(file).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        #region ask user if to delete source link file
                        string _isDeletePromptString = "";
                        if (AppSettings.Instance.isDeleteSourceLinkFile == -1 || AppSettings.Instance.isAskDeleteSourceLinkFile) //init value ,ask user
                        {
                            DialogResult dialogResult = CustomMessageBox.Show(
                                 $"Do you want keep source link [{Path.GetFileNameWithoutExtension(file)}]?",
                                 "Fenceless | Message",
                                 MessageBoxButtons.YesNo,
                                 MessageBoxIcon.Question);
                            if (dialogResult == DialogResult.Yes)
                            {
                                AppSettings.Instance.isDeleteSourceLinkFile = 0;
                                _isDeletePromptString = "Keep source link file";
                            }
                            else if (dialogResult == DialogResult.No)
                            {
                                AppSettings.Instance.isDeleteSourceLinkFile = 1;
                                _isDeletePromptString = "Delete source link file";
                            }
                        }
                        if (AppSettings.Instance.isAskDeleteSourceLinkFile)
                        {
                            DialogResult dialogResult2 = CustomMessageBox.Show(
                                $"Do you want always {_isDeletePromptString} ? \r\n If you select no, it will ask you again!\r\n\r\n(setting not saved persistently and message will again after program restarts.)",
                                "Fenceless | Message",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);
                            if (dialogResult2 == DialogResult.Yes)
                            {
                                AppSettings.Instance.isAskDeleteSourceLinkFile = false;
                            }

                        }
                        #endregion
                    }
                    else  // not a .lnk file, create new link file in  user appdata
                    {

                        var lnk = new LnkFileManager
                        {
                            TargetFilePath = file,
                            ShortcutFilePath = AppSettings.Instance.appDataPath + "\\shortcutbak",
                            ShortcutName = Path.GetFileName(file),
                            Description ="shortcut create by bestnofences",
                            WorkingDirectory=Path.GetDirectoryName(file)
                        };
                        newFile = new LnkHelper().CreateShortcutDynamic(lnk);
                    }

                    if (AppSettings.Instance.isDeleteSourceLinkFile == 1)
                    {
                        newFile = DeleteSourceShortcutFile(file);
                        _logger.Debug($"Deleted source link file for: {file}", "FenceWindow");
                    }

                    _fenceInfo.Files.Add(newFile);
                    addedFiles++;
                    _logger.Debug($"Added file to fence: {newFile}", "FenceWindow");


                    if (addedFiles > 0)
                    {
                        _logger.Info($"Added {addedFiles} files to fence '{_fenceInfo.Name}'", "FenceWindow");
                        Save();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to process dropped files for fence '{_fenceInfo.Name}'", "FenceWindow", ex);
            }
        }

        #region Ask user whether to keep the original shortcuts
        private string DeleteSourceShortcutFile(string filePath)
        {
            string newfilepath = filePath; // make a clone
            try
            {
                //only move .lnk files
                if (!Path.GetExtension(filePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) 
                {
                    _logger.Debug($"Not a shortcut file: {filePath}", "DeleteShortcutFile");
                    return newfilepath;
                }
                if (File.Exists(filePath))
                {
                    string shortCutLinkBakPath = AppSettings.Instance.appDataPathShortcutBak;
                    if (!Directory.Exists(shortCutLinkBakPath))
                    {
                        Directory.CreateDirectory(shortCutLinkBakPath);
                    }
                    newfilepath = Path.Combine(shortCutLinkBakPath, Path.GetFileName(filePath));
                    File.Copy(filePath, newfilepath, true);
                    if (File.Exists(newfilepath))
                    {
                        _logger.Debug($"Backed up shortcut to: {newfilepath}", "DeleteShortcutFile");
                        FileAttributes attributes = File.GetAttributes(filePath);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                            File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
                        File.Delete(filePath);
                        _logger.Debug($"Deleted shortcut: {filePath}", "DeleteShortcutFile");
                        if (IsDesktopFile(filePath)) NotifyDesktopChanged();
                    }
                    else
                    {
                        _logger.Warning($"Failed to back up shortcut to: {newfilepath}", "DeleteShortcutFile");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Error($"Access denied when deleting shortcut: {filePath}", "DeleteShortcutFile");
            }
            catch (IOException ex)
            {
                _logger.Error($"IO error when deleting shortcut: {filePath}", "DeleteShortcutFile");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error deleting shortcut: {filePath}", "DeleteShortcutFile");
            }
            return newfilepath;
        }
        private void NotifyDesktopChanged()
        {
            try
            {
                // send WM_SETTINGCHANGE notify FLUSH desktop 
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error notifying desktop change: {ex.Message}", "FenceWindow");
            }
        }
        #endregion

        private bool ItemExists(string path)
        {
            try
            {
                var exists = File.Exists(path) || Directory.Exists(path);
                if (!exists)
                {
                    _logger.Warning($"Item does not exist: {path}", "FenceWindow");
                }
                return exists;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error checking if item exists: {path}", "FenceWindow", ex);
                return false;
            }
        }
        private bool IsDesktopFile(string filePath)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fileDirectory = Path.GetDirectoryName(filePath);
                return string.Equals(fileDirectory, desktopPath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
       
        public List<string> ValidateAndCleanupItems()
        {
            var itemsToRemove = new List<string>();
            try
            {
                foreach (var file in _fenceInfo.Files)
                {
                    if (!ItemExists(file))itemsToRemove.Add(file);
                }

                if (itemsToRemove.Count > 0)
                {
                    foreach (var item in itemsToRemove)
                    {
                        _fenceInfo.Files.Remove(item);
                        _logger.Info($"Removed invalid {itemsToRemove.Count} item from fence '{_fenceInfo.Name}': {item}", "FenceWindow");
                    }
                    return itemsToRemove;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error validating items in fence '{_fenceInfo.Name}'", "FenceWindow", ex);
            }
            return itemsToRemove;
        }
        private void Save() => FenceManager.Instance.UpdateFence(_fenceInfo);
        
    }
}