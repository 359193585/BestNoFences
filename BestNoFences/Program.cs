using Fenceless.Model;
using Fenceless.Util;
using Fenceless.Win32;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fenceless
{
    static class Program
    {
        private static Logger logger;
        private static UI.LogViewerForm logViewerForm;

        #region test  code for DPI awareness - may be used in the future if we decide to make the app per-monitor DPI aware
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);

        // dpi awareness levels
        private enum DPI_AWARENESS
        {
            DPI_AWARENESS_INVALID = -1,
            DPI_AWARENESS_UNAWARE = 0,
            DPI_AWARENESS_SYSTEM_AWARE = 1,
            DPI_AWARENESS_PER_MONITOR_AWARE = 2
        }

        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // set process DPI awareness to handle high-DPI displays properly. This should be done before any UI is initialized.
            EnableDPIAwareness();

       
            try
            {
                // Initialize logging first
                logger = Logger.Instance;

                // Load settings to configure logging properly
                var settings = AppSettings.Instance;

                logger.Info("Fenceless application starting...", "Main");



                //allows the context menu to be in dark mode
                //inherits from the system settings
                WindowUtil.SetPreferredAppMode(1);

                // Check  if a new release is available 
                _ = Task.Run(UpdateNotication.CheckForUpdatesAsync);
                using (var mutex = new Mutex(true, "BettetNoFences", out var createdNew))
                {
                    if (createdNew)
                    {
                        logger.Info("Application mutex acquired, starting main application", "Main");

                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);

                        // Handle application exit
                        Application.ApplicationExit += Application_ApplicationExit;

                        // Handle unhandled exceptions
                        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                        Application.ThreadException += Application_ThreadException;
                        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                        var trayIcon = new NotifyIcon();
                        try
                        {
                            using (var ms = new MemoryStream(Properties.Resources.AppIconIco))
                            {
                                trayIcon.Icon = new Icon(ms);
                            }
                            var currentVersion = UpdateNotication.GetCurrentVersion();
                            trayIcon.Visible = true;
                            trayIcon.Text = "Fenceless - Desktop organization tool v" + currentVersion;

                            var contextMenu = new ContextMenuStrip();
                            #region Init right-click pop-up menus for taskbar icon

                            // Add Fence menu item with sub menu for fence type
                            var addFenceMenuItem = new ToolStripMenuItem("Add Fence");

                            var normalFenceMenuItem = new ToolStripMenuItem("Normal Fence");
                            normalFenceMenuItem.Click += (s, e) =>
                            {
                                logger.Info("Add Normal Fence requested from tray menu", "Main");
                                FenceManager.Instance.CreateFence("New Fence");
                            };

                            addFenceMenuItem.DropDownItems.Add(normalFenceMenuItem);

                            contextMenu.Items.Add(addFenceMenuItem);

                            // Add Log Viewer menu item
                            var logViewerMenuItem = new ToolStripMenuItem("View Logs");
                            logViewerMenuItem.Click += (s, e) => ShowLogViewer();
                            contextMenu.Items.Add(logViewerMenuItem);

                            // Add Settings menu item
                            var settingsMenuItem = new ToolStripMenuItem("Settings");
                            settingsMenuItem.Click += (s, e) => FenceManager.Instance.ShowGlobalSettings();
                            contextMenu.Items.Add(settingsMenuItem);

                            contextMenu.Items.Add(new ToolStripSeparator()); // -----------------

                            // Add auto size menu item with sub ment
                            var autoSizeFences = new ToolStripMenuItem("Fences Size");

                            var autoSizeLeftMentItem = new ToolStripMenuItem("Size Left");
                            autoSizeLeftMentItem.Click += (s, e) => FenceManager.Instance.SizeAllFenceLeft();
                            var autoSizeCenterMentItem = new ToolStripMenuItem("Size Center");
                            autoSizeCenterMentItem.Click += (s, e) => FenceManager.Instance.SizeAllFenceCenter();
                            var autoSizeRightMentItem = new ToolStripMenuItem("Size Right");
                            autoSizeRightMentItem.Click += (s, e) => FenceManager.Instance.SizeAllFenceRight();
                            var autoSizeTopRightMentItem = new ToolStripMenuItem("Size TopRight");
                            autoSizeTopRightMentItem.Click += (s, e) => FenceManager.Instance.SizeAllFenceTopRight();
                            var autoSizeFullMentItem = new ToolStripMenuItem("Size Auto");
                            autoSizeFullMentItem.Click += (s, e) => FenceManager.Instance.SizeAllFenceAuto();
                            var autoSizeHideMentItem = new ToolStripMenuItem("Hide All");
                            //autoSizeHideMentItem.Click += (s, e) => FenceManager.Instance.HideAllFences();
                            autoSizeHideMentItem.Click += (s, e) => FenceManager.Instance.SizeAllFenceMiniRightBottom();


                            autoSizeFences.DropDownItems.Add(autoSizeLeftMentItem);
                            autoSizeFences.DropDownItems.Add(autoSizeCenterMentItem);
                            autoSizeFences.DropDownItems.Add(autoSizeRightMentItem);
                            autoSizeFences.DropDownItems.Add(autoSizeTopRightMentItem);
                            autoSizeFences.DropDownItems.Add(new ToolStripSeparator()); // -----------------
                            autoSizeFences.DropDownItems.Add(autoSizeFullMentItem);
                            autoSizeFences.DropDownItems.Add(autoSizeHideMentItem);

                            contextMenu.Items.Add(autoSizeFences);
                            contextMenu.Items.Add(new ToolStripSeparator()); // -----------------

                            // Add Start with Windows checkbox
                            var startWithWindowsMenuItem = new ToolStripMenuItem("Start with Windows");
                            startWithWindowsMenuItem.CheckOnClick = true;

                            // Sync the setting with actual registry state at startup
                            bool actualStartupState = Util.StartupManager.IsStartupEnabled();
                            var appSettings = AppSettings.Instance;
                            if (appSettings.StartWithWindows != actualStartupState)
                            {
                                logger.Info($"Syncing startup setting - Registry: {actualStartupState}, Settings: {appSettings.StartWithWindows}", "Main");
                                appSettings.StartWithWindows = actualStartupState;
                                appSettings.SaveSettings();
                            }
                            startWithWindowsMenuItem.Checked = actualStartupState;
                            startWithWindowsMenuItem.CheckedChanged += (s, e) =>
                            {
                                logger.Debug($"Start with Windows toggled: {startWithWindowsMenuItem.Checked}", "Main");
                                var appSettings = AppSettings.Instance;
                                appSettings.StartWithWindows = startWithWindowsMenuItem.Checked;

                                if (startWithWindowsMenuItem.Checked)
                                {
                                    if (!Util.StartupManager.EnableStartup())
                                    {
                                        logger.Error("Failed to enable startup", "Main");
                                        MessageBox.Show("Failed to enable startup with Windows. Please check the logs for details.",
                                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        startWithWindowsMenuItem.Checked = false;
                                        appSettings.StartWithWindows = false;
                                    }
                                    else
                                    {
                                        logger.Info("Startup with Windows enabled", "Main");
                                    }
                                }
                                else
                                {
                                    if (!Util.StartupManager.DisableStartup())
                                    {
                                        logger.Error("Failed to disable startup", "Main");
                                        MessageBox.Show("Failed to disable startup with Windows. Please check the logs for details.",
                                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        startWithWindowsMenuItem.Checked = true;
                                        appSettings.StartWithWindows = true;
                                    }
                                    else
                                    {
                                        logger.Info("Startup with Windows disabled", "Main");
                                    }
                                }

                                appSettings.SaveSettings();
                            };
                            contextMenu.Items.Add(startWithWindowsMenuItem);


                            contextMenu.Items.Add(new ToolStripSeparator());

                            var exitMenuItem = new ToolStripMenuItem("Exit");
                            exitMenuItem.Click += (s, e) =>
                            {
                                logger.Info("Exit requested from tray menu", "Main");
                                trayIcon.Visible = false;
                                Application.Exit();
                            };
                            contextMenu.Items.Add(exitMenuItem);
                            #endregion
                            trayIcon.ContextMenuStrip = contextMenu;

                            trayIcon.DoubleClick += (s, e) =>
                            {
                                logger.Debug("Tray icon double-clicked - opening settings", "Main");
                                FenceManager.Instance.ShowGlobalSettings();
                            };

                            // load fences
                            try
                            {
                                logger.Info("Loading fences...", "Main");
                                FenceManager.Instance.LoadFences();
                                if (Application.OpenForms.Count == 0)
                                {
                                    logger.Info("No existing fences found, creating first fence", "Main");
                                    FenceManager.Instance.CreateFence("First fence");
                                }

                                logger.Info("Fenceless initialized successfully", "Main");
                                Application.Run();
                            }
                            catch (Exception ex)
                            {
                                logger.Critical("Application error during initialization", "Main", ex);
                                MessageBox.Show($"Application error: {ex.Message}\n\nPlease check the log files for more details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        finally
                        {
                            logger.Debug("Disposing tray icon", "Main");
                            trayIcon?.Dispose();
                        }
                    }
                    else
                    {
                        logger.Warning("Another instance of BetterNoFences is already running", "Main");
                        MessageBox.Show("BetterNoFences is already running.", "BetterNoFences", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }

            }
            catch (Exception ex)
            {
                if (logger != null)
                    logger.Critical("Critical error in main application", "Main", ex);
                
            }
        }
       
        private static void EnableDPIAwareness()
        {
            if (Environment.OSVersion.Version.Major >= 6) // Windows Vista or later
            {
                try
                {
                    // try set per-monitor DPI awareness first
                    SetProcessDpiAwareness((int)DPI_AWARENESS.DPI_AWARENESS_PER_MONITOR_AWARE);
                }
                catch
                {
                    try
                    {
                        SetProcessDPIAware();
                    }
                    catch
                    {
                    }
                }
            }
        }
        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            logger.Error("Unhandled thread exception", "Exception", e.Exception);
            
            var result = MessageBox.Show(
                $"An unexpected error occurred:\n{e.Exception.Message}\n\nWould you like to continue running the application?\n\nCheck the log viewer for more details.",
                "Unexpected Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
            
            if (result == DialogResult.No)
            {
                Application.Exit();
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Critical("Unhandled domain exception", "Exception", e.ExceptionObject as Exception);
            
            if (e.IsTerminating)
            {
                logger.Critical("Application is terminating due to unhandled exception", "Exception");
                MessageBox.Show(
                    $"A critical error occurred and the application must close:\n{e.ExceptionObject}\n\nPlease check the log files for more details.",
                    "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ShowLogViewer()
        {
            try
            {
                logger.Debug("Log viewer requested", "Main");
                if (logViewerForm == null || logViewerForm.IsDisposed)
                {
                    logViewerForm = new UI.LogViewerForm();
                }
                
                if (logViewerForm.Visible)
                {
                    logViewerForm.BringToFront();
                }
                else
                {
                    logViewerForm.Show();
                }
            }
            catch (Exception ex)
            {
                logger.Error("Failed to show log viewer", "Main", ex);
                MessageBox.Show($"Failed to show log viewer: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private static void Application_ApplicationExit(object sender, EventArgs e)
        {
            try
            {
                logger.Info("Fenceless shutting down...", "Main");
                
                // Save all data before exit
                FenceManager.Instance.SaveAllFences();
                AppSettings.Instance.SaveSettings();
                FenceManager.Instance.Dispose();
                
                logger.Info("Fenceless shutdown complete", "Main");

                // Flush and dispose logger
                logger.FlushLogs();
                logger.Dispose();
            }
            catch (Exception ex)
            {
                if (logger != null)
                    logger.Error("Error during application exit", "Main", ex);
                else
                    Debug.WriteLine($"Error during application exit: {ex.Message}");
            }
        }
    }
}
