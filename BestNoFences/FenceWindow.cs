using Fenceless.Model;
using Fenceless.UI;
using Fenceless.Util;
using Fenceless.Win32;
using Peter;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static Fenceless.Win32.WindowUtil;
using FormsTimer = System.Windows.Forms.Timer;

namespace Fenceless
{
    public partial class FenceWindow : Form
    {
        #region private value
        private int logicalTitleHeight;
        private int titleHeight;
        private const int titleOffset = 3;
        private const int itemWidth = 75;
        private const int textHeight = 35;

        private Font titleFont;
        private Font iconFont;

        private string selectedItem;
        private string hoveringItem;
        private bool shouldUpdateSelection;
        private bool shouldRunDoubleClick;
        private bool isMinified;
        private int prevHeight;
        private int scrollHeight;
        private int scrollOffset;

        // New fields for transparency and autohide
        private bool isAutoHidden = false;
        private double normalOpacity = 1.0;
        private bool isMouseInside = false;
        private FormsTimer autoHideTimer;
        // Visibility monitor to prevent Show Desktop from hiding the window
        private System.Threading.Timer visibilityMonitor;

        // Internal drag and drop fields
        private bool isDraggingItem = false;
        private string draggingItem = null;
        private Point dragCurrentPoint;
        private int dragTargetIndex = -1;

        private bool _isDragReady = false;
        private bool _isFormDrag = false;
        private Point _formDragStartPoint = Point.Empty;
        private bool _isDraggingForm = false;

        private FormsTimer dragRefreshTimer;
        #endregion

        // Thread-safe icon cache with automatic memory management
        private readonly IconCache iconCache = new IconCache(50);
        private readonly ThrottledExecution throttledMove = new ThrottledExecution(TimeSpan.FromSeconds(4));
        private readonly ThrottledExecution throttledResize = new ThrottledExecution(TimeSpan.FromSeconds(4));
        private readonly ShellContextMenu shellContextMenu = new ShellContextMenu();
        private readonly ThumbnailProvider thumbnailProvider = new ThumbnailProvider();
        private readonly FenceInfo _fenceInfo;
        private readonly Logger logger;
        private FenceRenderer _fenceRenderer;  // Declare the renderer instance
        private FenceInteractionHandler _handler; 

        public FenceWindow(FenceInfo fenceInfo)
        {
            _fenceInfo = fenceInfo;

            logger = Logger.Instance;
            logger.Debug($"Creating fence window for '{fenceInfo.Name}'", "FenceWindow");

            _fenceRenderer = new FenceRenderer(_fenceInfo, logger);// rendering process
            _handler = new FenceInteractionHandler(_fenceInfo, logger);

            // Set form properties to hide from Alt+Tab before initialization
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.None;

            InitializeComponent();
            SetupEventHandlers();
            DropShadow.ApplyShadows(this);
            BlurUtil.EnableBlur(Handle);

            logicalTitleHeight = (fenceInfo.TitleHeight < 16 || fenceInfo.TitleHeight > 100) ? 35 : fenceInfo.TitleHeight;
            titleHeight = LogicalToDeviceUnits(logicalTitleHeight);

            MouseWheel += FenceWindow_MouseWheel;
            thumbnailProvider.IconThumbnailLoaded += ThumbnailProvider_IconThumbnailLoaded;

            ReloadFonts();

            AllowDrop = true;

            Text = fenceInfo.Name;
            Location = new Point(fenceInfo.PosX, fenceInfo.PosY);

            Width = fenceInfo.Width;
            Height = fenceInfo.Height;
            prevHeight = fenceInfo.Height;
            
            lockedToolStripMenuItem.Checked = fenceInfo.Locked;
            minifyToolStripMenuItem.Checked = fenceInfo.CanMinify;

            // Initialize transparency and autohide
            SetTransparency(fenceInfo.Transparency);
            InitializeAutoHide();

            Minify();

            logger.Info($"Fence window '{fenceInfo.Name}' created successfully at ({fenceInfo.PosX}, {fenceInfo.PosY})", "FenceWindow");
        }

        private void ReloadFonts()
        {
            var family = new FontFamily("Segoe UI");
            titleFont = new Font(family, (int)Math.Floor(logicalTitleHeight / 2.0));
            iconFont = new Font(family, 9);
        }

        private void SetupEventHandlers()
        {
            removeItemToolStripMenuItem.Click += (sender, e) =>
            {
                if (hoveringItem != null)
                {
                    try
                    {
                        DialogResult result = CustomMessageBox.Show(
                            $"Remove '{Path.GetFileName(hoveringItem)}' from this fence?\n\nThis will not delete the file, only remove it from the fence.",
                            "Remove Item",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (result == DialogResult.Yes)
                        {
                            _fenceInfo.Files.Remove(hoveringItem);
                            hoveringItem = null;
                            selectedItem = null;
                            Save();
                            Refresh();
                            logger.Info($"Removed item from fence '{_fenceInfo.Name}'", "FenceWindow");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Failed to remove item from fence '{_fenceInfo.Name}'", "FenceWindow", ex);
                    }
                }
            };

            moveItemUpToolStripMenuItem.Click += (sender, e) =>
            {
                if (hoveringItem != null)
                {
                    try
                    {
                        var currentIndex = _fenceInfo.Files.IndexOf(hoveringItem);
                        if (currentIndex > 0)
                        {
                            // Swap with previous item
                            _fenceInfo.Files[currentIndex] = _fenceInfo.Files[currentIndex - 1];
                            _fenceInfo.Files[currentIndex - 1] = hoveringItem;

                            Save();
                            Refresh();
                            logger.Debug($"Moved item up in fence '{_fenceInfo.Name}'", "FenceWindow");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Failed to move item up in fence '{_fenceInfo.Name}'", "FenceWindow", ex);
                    }
                }
            };

            moveItemDownToolStripMenuItem.Click += (sender, e) =>
            {
                if (hoveringItem != null)
                {
                    try
                    {
                        var currentIndex = _fenceInfo.Files.IndexOf(hoveringItem);
                        if (currentIndex >= 0 && currentIndex < _fenceInfo.Files.Count - 1)
                        {
                            // Swap with next item
                            _fenceInfo.Files[currentIndex] = _fenceInfo.Files[currentIndex + 1];
                            _fenceInfo.Files[currentIndex + 1] = hoveringItem;

                            Save();
                            Refresh();
                            logger.Debug($"Moved item down in fence '{_fenceInfo.Name}'", "FenceWindow");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Failed to move item down in fence '{_fenceInfo.Name}'", "FenceWindow", ex);
                    }
                }
            };
        }

        // Add validation for file operations
        private bool ItemExists(string path)
        {
            try
            {
                var exists = File.Exists(path) || Directory.Exists(path);
                if (!exists)
                {
                    logger.Warning($"Item does not exist: {path}", "FenceWindow");
                }
                return exists;
            }
            catch (Exception ex)
            {
                logger.Error($"Error checking if item exists: {path}", "FenceWindow", ex);
                return false;
            }
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FenceManager.Instance.ShowGlobalSettings();
        }

        // Add methods for external control from FenceManager
        public void UpdateAutoHideState()
        {
            if (_fenceInfo.AutoHide)
            {
                StartAutoHideTimer();
            }
            else
            {
                ShowFence();
                StopAutoHideTimer();
            }
        }

        public void ApplySettings()
        {
            // Apply transparency
            SetTransparency(_fenceInfo.Transparency);

            // Apply auto-hide settings
            autoHideTimer.Interval = _fenceInfo.AutoHideDelay;
            UpdateAutoHideState();

            // Apply other settings
            lockedToolStripMenuItem.Checked = _fenceInfo.Locked;
            minifyToolStripMenuItem.Checked = _fenceInfo.CanMinify;

            // Update title and size if changed
            Text = _fenceInfo.Name;
            Width = _fenceInfo.Width;
            Height = _fenceInfo.Height;

            // Update title height if changed
            logicalTitleHeight = _fenceInfo.TitleHeight;
            titleHeight = LogicalToDeviceUnits(logicalTitleHeight);
            ReloadFonts();

            // Clear icon cache if icon size changed
            if (iconCache.CacheCount > 0)
            {
                ClearIconCache();
            }

            // Adjust height if minified
            if (isMinified)
            {
                prevHeight = Height;
                Height = titleHeight;
            }

            Refresh();
            Save();
        }

        private void ClearIconCache()
        {
            try
            {
                logger.Debug($"Clearing icon cache ({iconCache.CacheCount} entries)", "FenceWindow");

                iconCache.ClearCache();
            }
            catch (Exception ex)
            {
                logger.Error("Error clearing icon cache", "FenceWindow", ex);
            }
        }

        private void InitializeAutoHide()
        {
            autoHideTimer = new FormsTimer();
            autoHideTimer.Interval = _fenceInfo.AutoHideDelay;
            autoHideTimer.Tick += AutoHideTimer_Tick;
        }

        private void SetTransparency(int transparencyPercent)
        {
            // Clamp transparency between 25 and 100
            transparencyPercent = Math.Max(25, Math.Min(100, transparencyPercent));
            _fenceInfo.Transparency = transparencyPercent;

            normalOpacity = transparencyPercent / 100.0;
            if (!isAutoHidden)
            {
                this.Opacity = normalOpacity;
            }

            Save();
        }

        private void AutoHideTimer_Tick(object sender, EventArgs e)
        {
            if (_fenceInfo.AutoHide && !isMouseInside && !isMinified)
            {
                HideFence();
            }
            autoHideTimer.Stop();
        }

        private void HideFence()
        {
            if (!isAutoHidden)
            {
                isAutoHidden = true;
                this.Opacity = 0.1; // Nearly invisible but still responsive to mouse
            }
        }

        private void ShowFence()
        {
            if (isAutoHidden)
            {
                isAutoHidden = false;
                this.Opacity = normalOpacity;
            }
        }

        private void StartAutoHideTimer()
        {
            if (_fenceInfo.AutoHide && !isAutoHidden)
            {
                autoHideTimer.Stop();
                autoHideTimer.Start();
            }
        }

        private void StopAutoHideTimer()
        {
            autoHideTimer.Stop();
        }

        private void InitializeVisibilityMonitor()
        {
            visibilityMonitor = new System.Threading.Timer(_ => EnsureFenceVisible(true), null,
                TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
        }

        private void EnsureFenceVisible(bool triggeredByMonitor = false)
        {
            if (IsDisposed || !IsHandleCreated || isAutoHidden)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => EnsureFenceVisible(triggeredByMonitor)));
                }
                catch (ObjectDisposedException)
                {
                    // Window disposed while invoke was pending
                }
                return;
            }

            bool isHidden = !IsWindowVisible(Handle) ||
                            IsIconic(Handle) ||
                            !Visible ||
                            WindowState == FormWindowState.Minimized;

            if (isHidden)
            {
                Visible = true;
                if (WindowState == FormWindowState.Minimized)
                {
                    WindowState = FormWindowState.Normal;
                }

                ShowWindow(Handle, SW_SHOWNOACTIVATE);
                SendToDesktopBack();

                if (!triggeredByMonitor)
                {
                    logger?.Debug($"Fence window '{_fenceInfo?.Name ?? "Unknown"}' restored after Show Desktop", "FenceWindow");
                }
            }
        }

        private void SendToDesktopBack()
        {
            SetWindowPos(Handle, HWND_BOTTOM, 0, 0, 0, 0,
                SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        }

      
        private void HandleDisplayChangeGrid(int newScreenWidth, int newScreenHeight)
        {
            // Resize all fences
            FenceManager.Instance.SizeAllFence();
            // sure position is in screen bounds
            EnsureFenceVisible();
        }
     

        private void RemoveSelectedItem()
        {
            if (selectedItem != null)
            {
                try
                {
                    _fenceInfo.Files.Remove(selectedItem);
                    selectedItem = null;
                    hoveringItem = null;
                    Save();
                    Refresh();
                    logger.Info($"Removed selected item from fence '{_fenceInfo.Name}' via keyboard", "FenceWindow");
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to remove selected item from fence '{_fenceInfo.Name}'", "FenceWindow", ex);
                }
            }
        }

        private void MoveSelectedItemUp()
        {
            if (selectedItem != null)
            {
                try
                {
                    var currentIndex = _fenceInfo.Files.IndexOf(selectedItem);
                    if (currentIndex > 0)
                    {
                        _fenceInfo.Files[currentIndex] = _fenceInfo.Files[currentIndex - 1];
                        _fenceInfo.Files[currentIndex - 1] = selectedItem;
                        Save();
                        Refresh();
                        logger.Debug($"Moved selected item up in fence '{_fenceInfo.Name}' via keyboard", "FenceWindow");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to move selected item up in fence '{_fenceInfo.Name}'", "FenceWindow", ex);
                }
            }
        }

        private void MoveSelectedItemDown()
        {
            if (selectedItem != null)
            {
                try
                {
                    var currentIndex = _fenceInfo.Files.IndexOf(selectedItem);
                    if (currentIndex >= 0 && currentIndex < _fenceInfo.Files.Count - 1)
                    {
                        _fenceInfo.Files[currentIndex] = _fenceInfo.Files[currentIndex + 1];
                        _fenceInfo.Files[currentIndex + 1] = selectedItem;
                        Save();
                        Refresh();
                        logger.Debug($"Moved selected item down in fence '{_fenceInfo.Name}' via keyboard", "FenceWindow");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to move selected item down in fence '{_fenceInfo.Name}'", "FenceWindow", ex);
                }
            }
        }

        private void ToggleTransparency()
        {
            // Cycle through transparency levels: 100 -> 75 -> 50 -> 25 -> 100
            int newTransparency;
            switch (_fenceInfo.Transparency)
            {
                case 100:
                    newTransparency = 75;
                    break;
                case 75:
                    newTransparency = 50;
                    break;
                case 50:
                    newTransparency = 25;
                    break;
                default:
                    newTransparency = 100;
                    break;
            }
            SetTransparency(newTransparency);
        }

        private void ShowAllFences()
        {
            // This will be implemented in FenceManager
            FenceManager.Instance.ShowAllFences();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string fenceName = _fenceInfo.Name;
            if (CustomMessageBox.Show( $"Really remove this fence? \r\n fence name = {_fenceInfo.Name}", "Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                FenceManager.Instance.RemoveFence(_fenceInfo);
                Close();
            }
        }

        private void deleteItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _fenceInfo.Files.Remove(hoveringItem);
            hoveringItem = null;
            Save();
            Refresh();
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var hasHoveringItem = hoveringItem != null;
            var itemIndex = hasHoveringItem ? _fenceInfo.Files.IndexOf(hoveringItem) : -1;

            // Item-specific actions
            deleteItemToolStripMenuItem.Visible = hasHoveringItem;
            removeItemToolStripMenuItem.Visible = hasHoveringItem;
            moveItemUpToolStripMenuItem.Visible = hasHoveringItem && itemIndex > 0;
            moveItemDownToolStripMenuItem.Visible = hasHoveringItem && itemIndex < _fenceInfo.Files.Count - 1;
            toolStripSeparator3.Visible = hasHoveringItem;
        }

        private void FenceWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // This handles the KeyDown event for the form
            // ProcessCmdKey already handles our shortcuts, but this can be used for other keys
        }

        private void FenceWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && !lockedToolStripMenuItem.Checked)
                e.Effect = DragDropEffects.Move;
        }

        private void FenceWindow_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
                var addedFiles = 0;

                logger.Debug($"Processing {dropped.Length} dropped files", "FenceWindow");

                foreach (var file in dropped)
                {
                    if (!_fenceInfo.Files.Contains(file) && ItemExists(file))
                    {
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
                        string newFile = file;
                        if (AppSettings.Instance.isDeleteSourceLinkFile == 1)
                        {
                            newFile = DeleteShortcutFile(file);
                            logger.Debug($"Deleted source link file for: {file}", "FenceWindow");
                        }

                        _fenceInfo.Files.Add(newFile);
                        addedFiles++;
                        logger.Debug($"Added file to fence: {newFile}", "FenceWindow");
                    }
                    else
                    {
                        logger.Debug($"Skipped file (already exists or invalid): {file}", "FenceWindow");
                    }
                }

                if (addedFiles > 0)
                {
                    logger.Info($"Added {addedFiles} files to fence '{_fenceInfo.Name}'", "FenceWindow");
                    Save();
                    Refresh();
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to process dropped files for fence '{_fenceInfo.Name}'", "FenceWindow", ex);
            }
        }

        private void FenceWindow_Resize(object sender, EventArgs e)
        {
            throttledResize.Run(() =>
            {
                _fenceInfo.Width = Width;
                _fenceInfo.Height = isMinified ? prevHeight : Height;
                Save();
            });

            Refresh();
        }

        private void FenceWindow_MouseMove(object sender, MouseEventArgs e)
        {
#if nouse
            // first handle form dragging
            if (_isFormDrag && e.Button == MouseButtons.Left && !_isDraggingForm)
            {
                // calculate movement distance
                int moveX = Math.Abs(e.X - _formDragStartPoint.X);
                int moveY = Math.Abs(e.Y - _formDragStartPoint.Y);

                // if movement exceeds threshold, start form drag
                if (moveX > SystemInformation.DragSize.Width / 2 ||
                    moveY > SystemInformation.DragSize.Height / 2)
                {
                    StartFormDrag();
                    return; 
                }
            }
            // Handle internal item dragging
            if (isDraggingItem && !lockedToolStripMenuItem.Checked)
            {
                dragCurrentPoint = e.Location;

                // Update target position for drop indicator
                UpdateDragTarget(e.Location);

                // Use throttled refresh during drag to prevent excessive repainting
                if (dragRefreshTimer == null)
                {
                    dragRefreshTimer = new FormsTimer();
                    dragRefreshTimer.Interval = 16; // ~60 FPS max
                    dragRefreshTimer.Tick += (s, args) =>
                    {
                        if (isDraggingItem)
                        {
                            Invalidate();
                        }
                        else
                        {
                            dragRefreshTimer.Stop();
                            dragRefreshTimer.Dispose();
                            dragRefreshTimer = null;
                        }
                    };
                    dragRefreshTimer.Start();
                }
                return;
            }

            // Check if we should start dragging
            if (e.Button == MouseButtons.Left && !isDraggingItem && selectedItem != null && !lockedToolStripMenuItem.Checked)
            {
                // Only start drag if the item still exists
                if (ItemExists(selectedItem))
                {
                    var dragDistance = Math.Sqrt(Math.Pow(e.X - dragStartPoint.X, 2) + Math.Pow(e.Y - dragStartPoint.Y, 2));
                    if (dragDistance >= DragThreshold)
                    {
                        StartItemDrag(selectedItem, e.Location);
                        return;
                    }
                }
                else
                {
                    // Item no longer exists, clear selection
                    logger.Warning($"Selected item no longer exists: {selectedItem}", "FenceWindow");
                    _fenceInfo.Files.Remove(selectedItem);
                    selectedItem = null;
                    Save();
                    Refresh();
                }
            }

            // Only refresh if not dragging to avoid excessive repaints
            if (!isDraggingItem)
            {
                Refresh();
            }
#endif

            if (_isFormDrag && e.Button == MouseButtons.Left && !_isDraggingForm)
            {
                if (Math.Abs(e.X - _formDragStartPoint.X) > SystemInformation.DragSize.Width / 2 ||
                    Math.Abs(e.Y - _formDragStartPoint.Y) > SystemInformation.DragSize.Height / 2)
                {
                    StartFormDrag();
                    return;
                }
            }
            if (_isDragReady && !_handler.IsDraggingItem)
            {
                if (_handler.ShouldStartItemDrag(e.Location))
                {
                    this.Cursor = Cursors.Hand;
                    this.Text = $"{_fenceInfo.Name} - Dragging {Path.GetFileName(_handler.DraggingItemPath)}";
                    StartDragTimer(); //  Start throttled refresh timer
                }
            }
            _handler.ProcessMouseMove(e.Location, scrollOffset, titleHeight, itemWidth, textHeight, Width);

            if (!_handler.IsDraggingItem) Refresh();
        }
        // Unified frame-rate limited timer handling (maintain 60FPS rendering)
        private void StartDragTimer()
        {
            if (dragRefreshTimer == null)
            {
                dragRefreshTimer = new FormsTimer { Interval = 16 };
                dragRefreshTimer.Tick += (s, a) => {
                    if (_handler.IsDraggingItem) Invalidate();
                    else StopDragTimer(); 
                };
                dragRefreshTimer.Start();
            }
        }
        private void StopDragTimer()
        {
            dragRefreshTimer?.Stop();
        }

        private void FenceWindow_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var action = _handler.HandleMouseDown(e.Location, lockedToolStripMenuItem.Checked,
                    scrollOffset, titleHeight, itemWidth, textHeight, Width, out string targetPath);

                if (action == ClickActionResult.DragItem)
                {
                    _isDragReady = true;
                    _isFormDrag = false;
                    _handler.PrepareDrag(targetPath, e.Location); // 准备数据，但不开启 IsDraggingItem
                    draggingItem = targetPath;
                    selectedItem = targetPath;
                    this.Cursor = Cursors.Hand;
                }
                else if (action == ClickActionResult.DragForm)
                {
                    _isFormDrag = true;
                    _formDragStartPoint = e.Location;
                }
                else if (action == ClickActionResult.ItemRemoved)
                    this.Refresh(); 
            }
          
        }

        private void FenceWindow_MouseUp(object sender, MouseEventArgs e)
        {
            _isDragReady = false;
            if (_handler.IsDraggingItem)
            {
                bool changed = _handler.CompleteDragReorder(draggingItem, e.Location,
                    scrollOffset, titleHeight, itemWidth, textHeight, Width);
                ResetDragUI();
                if (changed) Refresh();
            }
            _isFormDrag = false;
            _isDraggingForm = false;
        }
        private void ResetDragUI()
        {
            isDraggingItem = false;
            draggingItem = null;
            dragTargetIndex = -1;
            this.Cursor = Cursors.Default;
            this.Text = _fenceInfo.Name;
            
            // Stop drag refresh timer
            if (dragRefreshTimer != null)
            {
                dragRefreshTimer.Stop();
                dragRefreshTimer.Dispose();
                dragRefreshTimer = null;
            }
            Invalidate();
            Refresh();
        }
        private void SaveFormPosition()
        {
            try
            {
                if (this.Location.X < 0) this.Location = new Point(0, this.Location.Y);
                if (this.Location.Y < 0) this.Location = new Point(this.Location.X, 0);

                Screen screen = Screen.FromControl(this);
                if (this.Right > screen.WorkingArea.Right)
                    this.Location = new Point(screen.WorkingArea.Right - this.Width, this.Location.Y);
                if (this.Bottom > screen.WorkingArea.Bottom)
                    this.Location = new Point(this.Location.X, screen.WorkingArea.Bottom - this.Height);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to save form position: {ex.Message}", "FenceWindow");
            }
        }
        private void FenceWindow_MouseEnter(object sender, EventArgs e)
        {
            isMouseInside = true;
            StopAutoHideTimer();
            ShowFence();

            if (minifyToolStripMenuItem.Checked && isMinified)
            {
                isMinified = false;
                Height = prevHeight;
            }
        }

        private void FenceWindow_MouseLeave(object sender, EventArgs e)
        {
            isMouseInside = false;
            StartAutoHideTimer();
            Minify();

            // If dragging is in progress, execute cancellation logic
            if (isDraggingItem)
            {
                _handler.HandleCancelDrag(draggingItem);
                ResetDragUI();
            }
            // Clear selection and refresh UI
            selectedItem = null;
            Refresh();
        }

        private void StartFormDrag()
        {
            if (!_isDraggingForm)
            {
                _isDraggingForm = true;

                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);

                _formDragStartPoint = this.Location;
                logger.Debug("Started form drag", "FenceWindow");
            }
        }
        
        private void Minify()
        {
            if (minifyToolStripMenuItem.Checked && !isMinified)
            {
                isMinified = true;
                prevHeight = Height;
                Height = titleHeight;
                Refresh();
            }
        }

        private void minifyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isMinified)
            {
                Height = prevHeight;
                isMinified = false;
            }
            _fenceInfo.CanMinify = minifyToolStripMenuItem.Checked;
            Save();
        }

      
        private void FenceWindow_Click(object sender, EventArgs e)
        {
            // Only handle selection if we're not dragging
            if (!isDraggingItem)
            {
                shouldUpdateSelection = true;
                Refresh();
            }
        }

        private void FenceWindow_DoubleClick(object sender, EventArgs e)
        {
            if (!isDraggingItem && selectedItem != null)
            {
                // Delegate double-click business to the handler
                bool needsRefresh = _handler.HandleDoubleClick(
                    PointToClient(MousePosition),
                    selectedItem,
                    scrollOffset, titleHeight, itemWidth, textHeight, Width,
                    out string newSelectedItem);

                this.selectedItem = newSelectedItem;

                if (needsRefresh)Refresh();
            }
        }
       
        private void FenceWindow_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                //   Collect all interaction states of the current window
                var ctx = new FencePaintContext
                {
                    Graphics = e.Graphics,
                    ClientRectangle = ClientRectangle,
                    WindowText = this.Text,
                    MousePos = PointToClient(MousePosition),
                    ScrollOffset = scrollOffset,

                    TitleHeight = titleHeight,
                    TitleOffset = titleOffset,
                    ItemWidth = itemWidth,
                    TextHeight = textHeight,
                    NewScrollHeight = scrollHeight,

                    IsDragging = isDraggingItem,
                    DraggingItemPath = draggingItem,
                    DragTargetIndex = dragTargetIndex,
                    DragCurrentPoint = dragCurrentPoint,

                    SelectedItem = selectedItem,
                    HoveringItem = hoveringItem,
                    ShouldUpdateSelection = shouldUpdateSelection,
                    ShouldRunDoubleClick = shouldRunDoubleClick
                };

                // Execute the fully stripped-down renderer
                _fenceRenderer.Render(ctx, new { IconCache = iconCache, ThumbnailProvider = thumbnailProvider });

                this.scrollHeight = ctx.NewScrollHeight;
                int visibleHeight = this.Height - titleHeight;
                if (this.scrollHeight <= visibleHeight)
                {
                    this.scrollOffset = 0;
                }
                else
                {
                    // Ensure scroll offset does not exceed the maximum range
                    this.scrollOffset = Math.Max(0, Math.Min(this.scrollOffset, this.scrollHeight - visibleHeight));
                }
                // Sync calculation results back to form
                if (ctx.ShouldUpdateSelection && !ctx.HasSelectionUpdated) this.selectedItem = null;
                if (!ctx.HasHoverUpdated) this.hoveringItem = null;

                if (ctx.HasSelectionUpdated) this.selectedItem = ctx.NewSelectedItem;
                if (ctx.HasHoverUpdated) this.hoveringItem = ctx.NewHoveringItem;

                this.scrollHeight = ctx.NewScrollHeight;
                this.scrollOffset = Math.Min(scrollOffset, Math.Max(0, ctx.NewScrollHeight - (Height - titleHeight)));

                // Reset transient flags
                shouldRunDoubleClick = false;
                shouldUpdateSelection = false;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Paint execution failed", "FenceWindow", ex);
            }
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialog = new UI.EditDialog("Edit Name", Text, "New name:");
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Text = dialog.NewName;
                _fenceInfo.Name = Text;
                Refresh();
                Save();
            }
        }

        private void newFenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FenceManager.Instance.CreateFence("New fence");
        }

        private void FenceWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Application.OpenForms.Count == 0)
                Application.Exit();
        }

        // Add method to expose FenceInfo for manager
        public FenceInfo GetFenceInfo()
        {
            return _fenceInfo;
        }

        // Method to update fence info from external source (like settings)
        public void UpdateFenceInfo(FenceInfo updatedInfo)
        {
            try
            {
                logger.Debug($"Updating fence info for '{_fenceInfo.Name}' -> '{updatedInfo.Name}'", "FenceWindow");

                // Update the fence info properties
                _fenceInfo.Name = updatedInfo.Name;
                _fenceInfo.Transparency = updatedInfo.Transparency;
                _fenceInfo.AutoHide = updatedInfo.AutoHide;
                _fenceInfo.AutoHideDelay = updatedInfo.AutoHideDelay;
                _fenceInfo.Locked = updatedInfo.Locked;
                _fenceInfo.CanMinify = updatedInfo.CanMinify;
                _fenceInfo.Width = updatedInfo.Width;
                _fenceInfo.Height = updatedInfo.Height;
                _fenceInfo.TitleHeight = updatedInfo.TitleHeight;
                _fenceInfo.PosX = updatedInfo.PosX;
                _fenceInfo.PosY = updatedInfo.PosY;

                // Update color and style properties
                _fenceInfo.BackgroundColor = updatedInfo.BackgroundColor;
                _fenceInfo.TitleBackgroundColor = updatedInfo.TitleBackgroundColor;
                _fenceInfo.TextColor = updatedInfo.TextColor;
                _fenceInfo.BorderColor = updatedInfo.BorderColor;
                _fenceInfo.BackgroundTransparency = updatedInfo.BackgroundTransparency;
                _fenceInfo.TitleBackgroundTransparency = updatedInfo.TitleBackgroundTransparency;
                _fenceInfo.TextTransparency = updatedInfo.TextTransparency;
                _fenceInfo.BorderTransparency = updatedInfo.BorderTransparency;
                _fenceInfo.BorderWidth = updatedInfo.BorderWidth;
                _fenceInfo.CornerRadius = updatedInfo.CornerRadius;
                _fenceInfo.ShowShadow = updatedInfo.ShowShadow;
                _fenceInfo.IconSize = updatedInfo.IconSize;
                _fenceInfo.ItemSpacing = updatedInfo.ItemSpacing;

                logger.Info($"Fence info updated for '{_fenceInfo.Name}'", "FenceWindow");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to update fence info for '{_fenceInfo?.Name}'", "FenceWindow", ex);
            }
        }

        // Methods for external control
        public void ForceShow()
        {
            ShowFence();
            StopAutoHideTimer();
        }

        public void ForceHide()
        {
            HideFence();
        }

        public void HighlightFence()
        {
            try
            {
                logger.Debug($"Highlighting fence '{_fenceInfo.Name}'", "FenceWindow");

                // Bring the fence to front and show it
                ForceShow();
                this.BringToFront();
                this.Focus();

                // Create a highlight effect by temporarily changing the border
                var originalOpacity = this.Opacity;
                var highlightTimer = new FormsTimer();
                var flashCount = 0;

                highlightTimer.Interval = 200;
                highlightTimer.Tick += (s, e) =>
                {
                    flashCount++;
                    if (flashCount % 2 == 0)
                    {
                        this.Opacity = originalOpacity;
                    }
                    else
                    {
                        this.Opacity = Math.Min(1.0, originalOpacity + 0.3);
                    }

                    if (flashCount >= 6) // Flash 3 times
                    {
                        this.Opacity = originalOpacity;
                        highlightTimer.Stop();
                        highlightTimer.Dispose();
                    }
                };

                highlightTimer.Start();

                logger.Info($"Fence '{_fenceInfo.Name}' highlighted", "FenceWindow");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to highlight fence '{_fenceInfo.Name}'", "FenceWindow", ex);
            }
        }

        private int ValidateAndCleanupItems()
        {
            try
            {
                var itemsToRemove = new List<string>();

                foreach (var file in _fenceInfo.Files)
                {
                    if (!ItemExists(file))
                    {
                        itemsToRemove.Add(file);
                    }
                }

                if (itemsToRemove.Count > 0)
                {
                    foreach (var item in itemsToRemove)
                    {
                        _fenceInfo.Files.Remove(item);
                        logger.Info($"Removed invalid item from fence '{_fenceInfo.Name}': {item}", "FenceWindow");
                    }

                    // Clear selection if it was removed
                    if (selectedItem != null && itemsToRemove.Contains(selectedItem))
                    {
                        selectedItem = null;
                    }

                    return itemsToRemove.Count;
                }

                return 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Error validating items in fence '{_fenceInfo.Name}'", "FenceWindow", ex);
                return 0;
            }
        }

        private void Save() => FenceManager.Instance.UpdateFence(_fenceInfo);
        private void FenceWindow_LocationChanged(object sender, EventArgs e)
        {
            throttledMove.Run(() =>
            {
                _fenceInfo.PosX = Location.X;
                _fenceInfo.PosY = Location.Y;
                Save();
            });
        }

        private void lockedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _fenceInfo.Locked = lockedToolStripMenuItem.Checked;
            Save();
        }

        private void FenceWindow_Load(object sender, EventArgs e)
        {
            // Validate items when the fence loads
            var removedCount = ValidateAndCleanupItems();
            if (removedCount > 0)
            {
                logger.Info($"Cleaned up {removedCount} invalid items from fence '{_fenceInfo.Name}' on load", "FenceWindow");
                Save();
                Refresh();
            }
        }

        private void FenceWindow_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (hoveringItem != null && !ModifierKeys.HasFlag(Keys.Shift))
            {
                shellContextMenu.CustomMenuItemSelected += OnRemoveFromFence;
                shellContextMenu.ShowContextMenu(
                    new[] { new FileInfo(hoveringItem) },
                    MousePosition,
                    (filePath) => "Remove from fence"
                );
            }
            else
            {
                appContextMenu.Show(this, e.Location);
            }
        }

        private void OnRemoveFromFence(object sender, CustomMenuEventArgs e)
        {
            try
            {
                var filePath = e.FilePath;
                if (string.IsNullOrEmpty(filePath))
                {
                    logger.Warning("Remove from fence called with empty file path", "FenceWindow");
                    return;
                }

                var fileName = Path.GetFileName(filePath);
                logger.Info($"Removing '{fileName}' from fence '{_fenceInfo.Name}' via context menu", "FenceWindow");

                // Only remove from the fence list, don't delete the actual file
                _fenceInfo.Files.Remove(filePath);
                hoveringItem = null;

                // Clear icon cache for the removed item to free memory
                iconCache.ClearCache();

                Save();
                Refresh();

                logger.Info($"Successfully removed '{fileName}' from fence via context menu", "FenceWindow");
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleException(ex, "Failed to remove item from fence via context menu", true);
            }
        }

        private void FenceWindow_MouseWheel(object sender, MouseEventArgs e)
        {
            if (scrollHeight < 1)
                return;

            scrollOffset -= Math.Sign(e.Delta) * 10;
            if (scrollOffset < 0)
                scrollOffset = 0;
            if (scrollOffset > scrollHeight)
                scrollOffset = scrollHeight;

            Invalidate();
        }

        private void ThumbnailProvider_IconThumbnailLoaded(object sender, EventArgs e)
        {
            Invalidate();
        }

        #region Shortcut Deletion
        private string DeleteShortcutFile(string filePath)
        {
            string newfilepath = filePath; // make a clone
            try
            {
                if (Path.GetExtension(filePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(filePath))
                    {
                        string shortCutLinkBakPath = AppSettings.Instance.appDataPath + "\\shortcutbak";
                        if (!Directory.Exists(shortCutLinkBakPath))
                        {
                            Directory.CreateDirectory(shortCutLinkBakPath);
                        }
                        newfilepath = Path.Combine(shortCutLinkBakPath, Path.GetFileName(filePath));
                        File.Copy(filePath, newfilepath,true);
                        if (File.Exists(newfilepath))
                        {
                            logger.Debug($"Backed up shortcut to: {newfilepath}", "DeleteShortcutFile");
                            FileAttributes attributes = File.GetAttributes(filePath);
                            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                                File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
                            File.Delete(filePath);
                            logger.Debug($"Deleted shortcut: {filePath}", "DeleteShortcutFile");
                            if (IsDesktopFile(filePath)) NotifyDesktopChanged();
                        }
                        else
                        {
                            logger.Warning($"Failed to back up shortcut to: {newfilepath}", "DeleteShortcutFile");
                        }
                       
                    }
                }
                else
                {
                    logger.Debug($"Not a shortcut file: {filePath}", "DeleteShortcutFile");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.Error($"Access denied when deleting shortcut: {filePath}", "DeleteShortcutFile");
            }
            catch (IOException ex)
            {
                logger.Error($"IO error when deleting shortcut: {filePath}", "DeleteShortcutFile");
            }
            catch (Exception ex)
            {
                logger.Error($"Error deleting shortcut: {filePath}", "DeleteShortcutFile");
            }
            return newfilepath;
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
        private void NotifyDesktopChanged()
        {
            try
            {
                // send WM_SETTINGCHANGE notify FLUSH desktop 
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                logger.Error($"Error notifying desktop change: {ex.Message}", "FenceWindow");
            }
        }
        #endregion

        #region override methods
        protected override void WndProc(ref Message m)
        {

            // new screen resolution
            if (m.Msg == WM_DISPLAYCHANGE)
            {
                int newWidth = (int)m.LParam & 0xFFFF;  // lParam low 16 bits is width
                int newHeight = (int)m.LParam >> 16;    // lParam high 16 bits is height
                int colorDepth = (int)m.WParam;         // wParam means color depth
                _fenceInfo.ScreenX = newWidth;
                _fenceInfo.ScreenY = newHeight;

                //// handle screen resolution change
                //HandleDisplayChangeGrid(newWidth, newHeight);
                FenceManager.Instance.SizeAllFence();
                EnsureFenceVisible();
            }

            // Remove border
            //if (m.Msg == 0x0083)
            //{
            //    m.Result = IntPtr.Zero;
            //    return;
            //}

            // Mouse leave
            var myrect = new Rectangle(Location, Size);
            if (m.Msg == 0x02a2 && !myrect.IntersectsWith(new Rectangle(MousePosition, new Size(1, 1))))
            {
                Minify();
            }

            // Prevent maximize/minimize
            if (m.Msg == WM_SYSCOMMAND)
            {
                var command = m.WParam.ToInt32() & 0xFFF0;
                if (command == SC_MAXIMIZE || command == SC_MINIMIZE)
                {
                    m.Result = IntPtr.Zero;
                    return;
                }
            }

            // Prevent window from being hidden (Show Desktop)
            if (m.Msg == WM_SHOWWINDOW && m.WParam == IntPtr.Zero)
            {
                // Ignore hide commands unless we're auto-hiding or user is closing
                if (!isAutoHidden && !this.IsDisposed)
                {
                    m.Result = IntPtr.Zero;
                    return;
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
                    if (!isAutoHidden && !IsDisposed)
                    {
                        wp.flags &= ~HideWindowFlag;
                        Marshal.StructureToPtr(wp, m.LParam, false);
                    }
                }
            }
            // By setting m.Result = IntPtr.Zero and returning, prevent the system from performing the default minimization operation.
            if (m.Msg == WM_SIZE && m.WParam.ToInt32() == SIZE_MINIMIZED)
            {
                EnsureFenceVisible();
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_WINDOWPOSCHANGED)
            {
                var wp = Marshal.PtrToStructure<WINDOWPOS>(m.LParam);
                if ((wp.flags & HideWindowFlag) != 0 && !isAutoHidden && !IsDisposed)
                {
                    EnsureFenceVisible();
                    m.Result = IntPtr.Zero;
                    return;
                }
            }

            if (m.Msg == WM_COMMAND)
            {
                int commandId = m.WParam.ToInt32() & 0xFFFF;
                if ((commandId == MIN_ALL || commandId == MIN_ALL_UNDO) && !isAutoHidden)
                {
                    EnsureFenceVisible();
                    m.Result = IntPtr.Zero;
                    return;
                }
            }

            // Prevent foreground
            if (m.Msg == WM_SETFOCUS)
            {
                SendToDesktopBack();
                return;
            }

            // Other messages
            base.WndProc(ref m);

            // If not locked and using the left mouse button
            if (MouseButtons == MouseButtons.Right || lockedToolStripMenuItem.Checked)
                return;

            // Then, allow dragging and resizing
            // If you comment out this section of code, it is easy to cause the form - flickering problem.
            if (m.Msg == WM_NCHITTEST)
            {
                var pt = PointToClient(new Point(m.LParam.ToInt32()));

                // Don't allow form dragging if we're dragging an item
                if (isDraggingItem)
                {
                    m.Result = (IntPtr)HTCLIENT;
                    return;
                }

                //if ((int)m.Result == HTCLIENT && pt.Y < titleHeight)     // drag the form
                //{
                //    m.Result = (IntPtr)HTCAPTION;
                //    FenceWindow_MouseEnter(null, null);
                //}

                // The following message handling will affect whether the border of the form can be adjusted.
                if (pt.X < 10 && pt.Y < 10)
                    m.Result = new IntPtr(HTTOPLEFT);
                else if (pt.X > (Width - 10) && pt.Y < 10)
                    m.Result = new IntPtr(HTTOPRIGHT);
                else if (pt.X < 10 && pt.Y > (Height - 10))
                    m.Result = new IntPtr(HTBOTTOMLEFT);
                else if (pt.X > (Width - 10) && pt.Y > (Height - 10))
                    m.Result = new IntPtr(HTBOTTOMRIGHT);
                else if (pt.Y > (Height - 10))
                    m.Result = new IntPtr(HTBOTTOM);
                else if (pt.X < 10)
                    m.Result = new IntPtr(HTLEFT);
                else if (pt.X > (Width - 10))
                    m.Result = new IntPtr(HTRIGHT);
            }
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Handle Escape key to cancel dragging
            if (keyData == Keys.Escape && isDraggingItem)
            {
                _handler.HandleCancelDrag(draggingItem);
                ResetDragUI();
                return true;
            }

            // Handle keyboard shortcuts
            if (keyData == (Keys.Control | Keys.Alt | Keys.T))
            {
                ToggleTransparency();
                return true;
            }
            else if (keyData == (Keys.Control | Keys.Alt | Keys.S))
            {
                ShowAllFences();
                return true;
            }
            else if (keyData == Keys.Delete && selectedItem != null && !lockedToolStripMenuItem.Checked)
            {
                RemoveSelectedItem();
                return true;
            }
            else if (keyData == (Keys.Control | Keys.Up) && selectedItem != null && !lockedToolStripMenuItem.Checked)
            {
                MoveSelectedItemUp();
                return true;
            }
            else if (keyData == (Keys.Control | Keys.Down) && selectedItem != null && !lockedToolStripMenuItem.Checked)
            {
                MoveSelectedItemDown();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // Additional protection: Hide from Alt+Tab after handle is created
            HideFromAltTab(Handle);

            // Prevent minimize to survive Show Desktop
            DesktopUtil.PreventMinimize(Handle);

            // Start visibility monitor to keep window visible
            InitializeVisibilityMonitor();

            logger?.Debug($"Fence window '{_fenceInfo?.Name ?? "Unknown"}' configured to prevent minimize", "FenceWindow");
        }
        // Override CreateParams to hide from Alt+Tab and prevent minimize on Show Desktop
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // Add WS_EX_TOOLWINDOW to hide from Alt+Tab
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                // Add WS_EX_NOACTIVATE to prevent being minimized on Show Desktop
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                // Remove WS_EX_APPWINDOW to prevent Show Desktop from affecting this window
                cp.ExStyle &= ~0x00040000; // Remove WS_EX_APPWINDOW
                return cp;
            }
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            DesktopUtil.GlueToDesktop(Handle);
            SendToDesktopBack();
            logger?.Debug($"Fence window '{_fenceInfo?.Name ?? "Unknown"}' attached to desktop", "FenceWindow");
        }
        protected override void SetVisibleCore(bool value)
        {
            // Prevent Show Desktop from hiding the window
            // Only allow hiding if we're auto-hiding or being disposed
            if (!value && !isAutoHidden && !this.IsDisposed && this.IsHandleCreated)
            {
                // Ignore hide requests from Show Desktop
                return;
            }
            base.SetVisibleCore(value);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                logger?.Debug("Disposing fence window", "FenceWindow");

                // Dispose icon cache (handles all cached bitmaps)
                iconCache?.Dispose();

                // Dispose timers
                autoHideTimer?.Dispose();
                dragRefreshTimer?.Dispose();
                visibilityMonitor?.Dispose();

                // Dispose fonts
                titleFont?.Dispose();
                iconFont?.Dispose();

                // Dispose other resources
                thumbnailProvider?.Dispose();
                throttledMove?.Dispose();
                throttledResize?.Dispose();
                // Note: ShellContextMenu doesn't implement IDisposable

                // Notify the stripped-down renderer to clean up
                _fenceRenderer?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}

