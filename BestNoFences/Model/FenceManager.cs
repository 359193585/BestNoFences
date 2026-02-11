using Fenceless.Properties;
using Fenceless.UI;
using Fenceless.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using static Fenceless.Win32.WindowUtil;

namespace Fenceless.Model
{
    public class FenceManager
    {
        public static FenceManager Instance { get; } = new FenceManager();

        private const string MetaFileName = "__fence_metadata.xml";
        private readonly string basePath;
        private readonly List<FenceWindow> activeFences = new List<FenceWindow>();
        private GlobalHotkeyManager hotkeyManager;
        private int toggleAutoHideHotkeyId = -1;
        private int showAllFencesHotkeyId = -1;
        private readonly Logger logger;
        private static readonly object _saveLock = new object();

        public FenceManager()
        {
            logger = Logger.Instance;
            basePath = AppSettings.Instance.appDataPath;
            EnsureDirectoryExists(basePath);
            logger.Info($"FenceManager initialized with base path: {basePath}", "FenceManager");
            InitializeGlobalHotkeys();
        }

        private void InitializeGlobalHotkeys()
        {
            try
            {
                hotkeyManager = new GlobalHotkeyManager();
                RegisterGlobalHotkeys();
                logger.Info("Global hotkeys initialized successfully", "FenceManager");
            }
            catch (Exception ex)
            {
                logger.Error("Failed to initialize global hotkeys", "FenceManager", ex);
            }
        }

        private void RegisterGlobalHotkeys()
        {
            try
            {
                // Unregister existing hotkeys
                if (toggleAutoHideHotkeyId != -1)
                {
                    hotkeyManager.UnregisterHotkey(toggleAutoHideHotkeyId);
                    toggleAutoHideHotkeyId = -1;
                }
                if (showAllFencesHotkeyId != -1)
                {
                    hotkeyManager.UnregisterHotkey(showAllFencesHotkeyId);
                    showAllFencesHotkeyId = -1;
                }

                // Register toggle auto-hide hotkey (Ctrl+Alt+H by default)
                toggleAutoHideHotkeyId = hotkeyManager.RegisterHotkey(
                    Keys.H, ctrl: true, alt: true, action: ToggleAllFencesAutoHide);

                // Register show all fences hotkey (Ctrl+Alt+S by default)
                showAllFencesHotkeyId = hotkeyManager.RegisterHotkey(
                    Keys.S, ctrl: true, alt: true, action: ShowAllFences);

                logger.Info("Global hotkeys registered successfully (Ctrl+Alt+H: Toggle Auto-hide, Ctrl+Alt+S: Show All)", "FenceManager");
            }
            catch (Exception ex)
            {
                logger.Error("Failed to register global hotkeys", "FenceManager", ex);
            }
        }
        public void SizeAllFence()
        {
            // try move desktop icons arranged to left, but win10/11  not doing it properly
            DesktopIconManager.ArrangeIconsToLeft();
            // get usable screen area, 
            Rectangle usableArea = new DesktopIconManager().GetUsableScreenArea();
            SizeAllFence(usableArea);
        }
        public void SizeAllFenceMiniRightBottom()
        {
            // get usable screen area, 
            Rectangle usableArea = new Rectangle(
                x: Screen.PrimaryScreen.WorkingArea.Width-5,
                y: Screen.PrimaryScreen.WorkingArea.Height-5,
                width: 5,
                height: 5
                );
            SizeAllFence(usableArea);
        }
        public void HideAllFences()
        {
            try
            {
                logger.Info("Hiding all fences", "FenceManager");
                foreach (var fence in activeFences.ToList())
                {
                    fence.ForceHide();
                }
                logger.Info($"Hidden {activeFences.Count} fence(s)", "FenceManager");
            }
            catch (Exception ex)
            {
                logger.Error("Failed to hide all fences", "FenceManager", ex);
            }
        }
        public void SizeAllFence(Rectangle usableArea)
        {
            // calculate new layout by number of open forms
            int myFormCount = Application.OpenForms.Count;
            List<Rectangle> layoutResult = FormLayoutCalculator.CalculateLayoutOnPrimaryScreen(myFormCount, usableArea, preferMoreRows: false);

            int kk = 0;
            foreach (Form f in Application.OpenForms)
            {
                f.Left = layoutResult[kk].Left;
                f.Top = layoutResult[kk].Top;
                f.Width = layoutResult[kk].Width;
                f.Height = layoutResult[kk].Height;
                kk++;
            }
        }
        public void SizeAllFenceLeft()
        {
            // try move desktop icons arranged to right, but win10/11  not doing it properly
            DesktopIconManager.ArrangeIconsToRight();
            // half screen area minus desktop icons area
            Rectangle usableArea = new Rectangle(
                x: 0,
                y: 0,
                width: Screen.PrimaryScreen.WorkingArea.Width/2,
                height: Screen.PrimaryScreen.WorkingArea.Height
                );
            SizeAllFence(usableArea);
        }
        public void SizeAllFenceCenter()
        {
            Rectangle usableArea = new Rectangle(
                x: Screen.PrimaryScreen.WorkingArea.Width / 4,
                y: 0,
                width: Screen.PrimaryScreen.WorkingArea.Width / 2,
                height: Screen.PrimaryScreen.WorkingArea.Height
                );
            SizeAllFence(usableArea);
        }
        public void SizeAllFenceRight()
        {
            // half screen area 
            Rectangle usableArea = new Rectangle(
                x: Screen.PrimaryScreen.WorkingArea.Width / 2,
                y: 0,
                width: Screen.PrimaryScreen.WorkingArea.Width / 2,
                height: Screen.PrimaryScreen.WorkingArea.Height
                );
            SizeAllFence(usableArea);
        }
        public void LoadFences()
        {
            try
            {
                logger.Info("Loading fences from storage", "FenceManager");
                int loadedCount = 0;
                int errorCount = 0;
                
                foreach (var dir in Directory.EnumerateDirectories(basePath))
                {
                    var metaFile = Path.Combine(dir, MetaFileName);
                    if (!File.Exists(metaFile))
                        continue;

                    try
                    {
                        var fenceInfo = LoadFenceInfo(metaFile);
                        if (fenceInfo != null)
                        {
                            var fenceWindow = new FenceWindow(fenceInfo);

                            // Get source screen dimensions for scaling
                            fenceInfo.ScreenX = Screen.PrimaryScreen.Bounds.Width;
                            fenceInfo.ScreenY = Screen.PrimaryScreen.Bounds.Height;

                            activeFences.Add(fenceWindow);
                            fenceWindow.FormClosed += (s, e) => activeFences.Remove(fenceWindow);
                            fenceWindow.Show();
                            loadedCount++;
                            logger.Debug($"Loaded fence '{fenceInfo.Name}' from {dir}", "FenceManager");
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        // Log error but continue loading other fences
                        logger.Error($"Failed to load fence from {dir}", "FenceManager", ex);
                    }
                }

                if (loadedCount > 0)
                {
                    logger.Info($"Successfully loaded {loadedCount} fence(s). {errorCount} error(s) encountered.", "FenceManager");
                }
                else
                {
                    logger.Info("No fences found to load", "FenceManager");
                }
            }
            catch (Exception ex)
            {
                logger.Error("Failed to load fences", "FenceManager", ex);
            }
        }

        private FenceInfo LoadFenceInfo(string metaFile)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(FenceInfo));
                using (var reader = new StreamReader(metaFile))
                {
                    var fenceInfo = serializer.Deserialize(reader) as FenceInfo;
                    logger.Debug($"Deserialized fence info from {metaFile}", "FenceManager");
                    return fenceInfo;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to deserialize fence metadata from {metaFile}", "FenceManager", ex);
                return null;
            }
        }

        public void CreateFence(string name)
        {
            
            try
            {
                logger.Info($"Creating new fence: '{name}'", "FenceManager");
                var settings = AppSettings.Instance;
                settings.DefaultFenceWidth = Screen.PrimaryScreen.WorkingArea.Width / 5;
                settings.DefaultFenceHeight = Screen.PrimaryScreen.WorkingArea.Height / 4;
                
                var nonOverlappingPos = FindNonOverlappingPosition(new Size(settings.DefaultFenceWidth, settings.DefaultFenceHeight), activeFences);
                settings.DefaultFencePosX = nonOverlappingPos.X;
                settings.DefaultFencePosY = nonOverlappingPos.Y;

                var fenceInfo = new FenceInfo(Guid.NewGuid())
                {
                    Name = name,
                    PosX = settings.DefaultFencePosX,
                    PosY = settings.DefaultFencePosY,
                    Height = settings.DefaultFenceHeight,
                    Width = settings.DefaultFenceWidth,
                    TitleHeight = settings.DefaultTitleHeight,
                    Transparency = settings.DefaultTransparency,
                    AutoHide = settings.DefaultAutoHide,
                    AutoHideDelay = settings.DefaultAutoHideDelay,
                    BackgroundColor = settings.DefaultBackgroundColor,
                    TitleBackgroundColor = settings.DefaultTitleBackgroundColor,
                    TextColor = settings.DefaultTextColor,
                    BorderColor = settings.DefaultBorderColor,
                    BackgroundTransparency = settings.DefaultBackgroundTransparency,
                    TitleBackgroundTransparency = settings.DefaultTitleBackgroundTransparency,
                    TextTransparency = settings.DefaultTextTransparency,
                    BorderTransparency = settings.DefaultBorderTransparency,
                    BorderWidth = settings.DefaultBorderWidth,
                    CornerRadius = settings.DefaultCornerRadius,
                    ShowShadow = settings.DefaultShowShadow,
                    IconSize = settings.DefaultIconSize,
                    ItemSpacing = settings.DefaultItemSpacing
                };

                UpdateFence(fenceInfo);
                var fenceWindow = new FenceWindow(fenceInfo);
                activeFences.Add(fenceWindow);
                fenceWindow.FormClosed += (s, e) => activeFences.Remove(fenceWindow);
                fenceWindow.Show();
                fenceWindow.BringToFront();

                logger.Info($"Fence '{name}' created successfully with ID {fenceInfo.Id}", "FenceManager");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to create fence '{name}'", "FenceManager", ex);
            }
        }

        public Point FindNonOverlappingPosition(Size fenceSize, List<FenceWindow> existingFences)
        {
            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            int offsetX = fenceSize.Width / 2;
            int offsetY = fenceSize.Height / 2;
            for (int y = screenBounds.Top; y <= screenBounds.Bottom - fenceSize.Height; y += offsetY)
            {
                for (int x = screenBounds.Left; x <= screenBounds.Right - fenceSize.Width; x += offsetX)
                {
                    Rectangle candidateRect = new Rectangle(x, y, fenceSize.Width, fenceSize.Height);
                    bool overlaps = false;
                    foreach (var existingFence in existingFences)
                    {
                        if (existingFence.Visible && !existingFence.IsDisposed)
                        {
                            Rectangle existingRect = new Rectangle(
                                existingFence.Location, existingFence.Size);

                            if (candidateRect.IntersectsWith(existingRect))
                            {
                                overlaps = true;
                                break;
                            }
                        }
                    }

                    if (!overlaps)
                    {
                        return new Point(x, y);
                    }
                }
            }
            int centerX = (screenBounds.Width - fenceSize.Width) / 2;
            int centerY = (screenBounds.Height - fenceSize.Height) / 2;
            return new Point(centerX, centerY);
        }
        public void RemoveFence(FenceInfo info)
        {
            try
            {
                logger.Info($"Removing fence '{info.Name}' (ID: {info.Id})", "FenceManager");
                var folderPath = GetFolderPath(info);
                if (Directory.Exists(folderPath))
                {
                    SendItemToSource(info);
                    Directory.Delete(folderPath, true);
                    logger.Debug($"Deleted fence directory: {folderPath}", "FenceManager");
                }
                logger.Info($"Fence '{info.Name}' removed successfully", "FenceManager");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to remove fence '{info.Name}'", "FenceManager", ex);
            }
        }
        private void SendItemToSource(FenceInfo info)
        {
            foreach (var item in info.Files)
            {
                string itemPath = Path.GetDirectoryName(item);
                bool isInBakPath = itemPath.TrimEnd('\\') == AppSettings.Instance.appDataPathShortcutBak.TrimEnd('\\');
                if (isInBakPath)
                {
                    try
                    {
                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        string newFile = Path.Combine(desktopPath, Path.GetFileName(item));
                        File.Copy(item, newFile,false);
                        // send WM_SETTINGCHANGE notify FLUSH desktop 
                        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
                    }
                    catch { }
                }
            }
        }

        private readonly object saveLock = new object();
        public void UpdateFence(FenceInfo fenceInfo)
        {
            lock (saveLock)
            {
                try
                {
                    var path = GetFolderPath(fenceInfo);
                    EnsureDirectoryExists(path);

                    var metaFile = Path.Combine(path, MetaFileName);
                    var serializer = new XmlSerializer(typeof(FenceInfo));

                    using (var writer = new StreamWriter(metaFile))
                    {
                        serializer.Serialize(writer, fenceInfo);
                    }
                    logger.Debug($"Updated fence '{fenceInfo.Name}' metadata", "FenceManager");
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to update fence '{fenceInfo.Name}'", "FenceManager", ex);
                }
            }
        }

        private void EnsureDirectoryExists(string dir)
        {
            try
            {
                var di = new DirectoryInfo(dir);
                if (!di.Exists)
                {
                    di.Create();
                    logger.Debug($"Created directory: {dir}", "FenceManager");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to create directory {dir}", "FenceManager", ex);
            }
        }

        private string GetFolderPath(FenceInfo fenceInfo)
        {
            return Path.Combine(basePath, fenceInfo.Id.ToString());
        }

        public void SaveAllFences()
        {
            lock (_saveLock)
            {
                try
                {
                    logger.Info("Saving all fences", "FenceManager");
                    int savedCount = 0;
                    int errorCount = 0;
                    
                    // Create a snapshot to avoid modification during enumeration
                    var fencesSnapshot = activeFences.ToList();
                    
                    foreach (var fence in fencesSnapshot)
                    {
                        try
                        {
                            var fenceInfo = fence.GetFenceInfo();
                            UpdateFence(fenceInfo);
                            savedCount++;
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            logger.Error("Failed to save individual fence", "FenceManager", ex);
                        }
                    }
                    
                    logger.Info($"Saved {savedCount} fence(s) successfully. {errorCount} error(s) encountered.", "FenceManager");
                }
                catch (Exception ex)
                {
                    logger.Error("Failed to save all fences", "FenceManager", ex);
                }
            }
        }

        // Global hotkey actions
        private void ToggleAllFencesAutoHide()
        {
            try
            {
                logger.Info("Toggling auto-hide for all fences", "FenceManager");
                bool newAutoHideState = false;
                foreach (var fence in activeFences.ToList())
                {
                    var fenceInfo = fence.GetFenceInfo();
                    fenceInfo.AutoHide = !fenceInfo.AutoHide;
                    newAutoHideState = fenceInfo.AutoHide;
                    fence.UpdateAutoHideState();
                    UpdateFence(fenceInfo);
                }
                logger.Info($"Auto-hide toggled to {newAutoHideState} for all fences", "FenceManager");
            }
            catch (Exception ex)
            {
                logger.Error("Failed to toggle auto-hide for all fences", "FenceManager", ex);
            }
        }

        public void ShowAllFences()
        {
            try
            {
                logger.Info("Showing all fences", "FenceManager");
                foreach (var fence in activeFences.ToList())
                {
                    fence.ForceShow();
                }
                logger.Info($"Showed {activeFences.Count} fence(s)", "FenceManager");
            }
            catch (Exception ex)
            {
                logger.Error("Failed to show all fences", "FenceManager", ex);
            }
        }

        public void ApplySettingsToAllFences(int transparency, bool autoHide, int autoHideDelay)
        {
            try
            {
                logger.Info($"Applying settings to all fences - Transparency: {transparency}%, AutoHide: {autoHide}, Delay: {autoHideDelay}ms", "FenceManager");
                foreach (var fence in activeFences.ToList())
                {
                    var fenceInfo = fence.GetFenceInfo();
                    fenceInfo.Transparency = transparency;
                    fenceInfo.AutoHide = autoHide;
                    fenceInfo.AutoHideDelay = autoHideDelay;
                    
                    fence.ApplySettings();
                    UpdateFence(fenceInfo);
                }
                logger.Info($"Settings applied to {activeFences.Count} fence(s)", "FenceManager");
            }
            catch (Exception ex)
            {
                logger.Error("Failed to apply settings to all fences", "FenceManager", ex);
            }
        }

        public void ShowGlobalSettings()
        {
            string formName = "FencesSetting";
            foreach (Form form in Application.OpenForms)
            {
                if (form.Name == formName)
                {
                    form.Activate();
                    form.WindowState = FormWindowState.Normal;
                    return;
                }
            }
            try
            {
                logger.Debug("Opening global settings dialog", "FenceManager");
                var settingsForm = new SettingsForm();
                settingsForm.Owner = Application.OpenForms[0];
                if (settingsForm.ShowDialog() == DialogResult.OK)
                {
                    // Refresh global hotkeys if they changed
                    RegisterGlobalHotkeys();
                    logger.Info("Global settings updated", "FenceManager");
                }
            }
            catch (Exception ex)
            {
                logger.Error("Failed to show global settings", "FenceManager", ex);
            }
        }

        public void HighlightFence(Guid fenceId)
        {
            try
            {
                logger.Debug($"Highlighting fence with ID: {fenceId}", "FenceManager");
                var fenceWindow = activeFences.FirstOrDefault(f => f.GetFenceInfo().Id == fenceId);
                if (fenceWindow != null)
                {
                    fenceWindow.HighlightFence();
                    logger.Info($"Highlighted fence '{fenceWindow.GetFenceInfo().Name}'", "FenceManager");
                }
                else
                {
                    logger.Warning($"Fence with ID {fenceId} not found for highlighting", "FenceManager");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to highlight fence with ID {fenceId}", "FenceManager", ex);
            }
        }

        public void ApplySettingsToFence(FenceInfo fenceInfo)
        {
            try
            {
                logger.Debug($"Applying settings to fence '{fenceInfo.Name}'", "FenceManager");
                var fenceWindow = activeFences.FirstOrDefault(f => f.GetFenceInfo().Id == fenceInfo.Id);
                if (fenceWindow != null)
                {
                    // Update the fence window's internal fence info reference
                    fenceWindow.UpdateFenceInfo(fenceInfo);
                    fenceWindow.ApplySettings();
                    logger.Info($"Applied settings to fence '{fenceInfo.Name}'", "FenceManager");
                }
                else
                {
                    logger.Warning($"Fence '{fenceInfo.Name}' not found for settings application", "FenceManager");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to apply settings to fence '{fenceInfo.Name}'", "FenceManager", ex);
            }
        }

        public List<FenceInfo> GetAllFenceInfos()
        {
            try
            {
                // Return current fence info from active fences
                var result = new List<FenceInfo>();
                foreach (var fence in activeFences.ToList())
                {
                    var fenceInfo = fence.GetFenceInfo();
                    result.Add(fenceInfo);
                }
                logger.Debug($"Retrieved {result.Count} fence infos", "FenceManager");
                return result;
            }
            catch (Exception ex)
            {
                logger.Error("Failed to get all fence infos", "FenceManager", ex);
                return new List<FenceInfo>();
            }
        }

        public int GetActiveFenceCount()
        {
            return activeFences.Count;
        }

        public void Dispose()
        {
            try
            {
                logger.Info("Disposing FenceManager", "FenceManager");
                hotkeyManager?.Dispose();
                logger.Debug("FenceManager disposed successfully", "FenceManager");
            }
            catch (Exception ex)
            {
                logger.Error("Error disposing FenceManager", "FenceManager", ex);
            }
        }
    }
}
