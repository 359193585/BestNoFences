using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Fenceless.Util
{
    /// <summary>
    /// Manages Windows startup registry settings for the application
    /// </summary>
    public static class StartupManager
    {
        private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "Fenceless";

        private static readonly Logger logger = Logger.Instance;

        /// <summary>
        /// Checks if the application is set to start with Windows
        /// </summary>
        /// <returns>True if startup is enabled, false otherwise</returns>
        public static bool IsStartupEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false))
                {
                    if (key == null)
                    {
                        logger?.Warning("Could not open startup registry key", "StartupManager");
                        return false;
                    }

                    var value = key.GetValue(AppName) as string;
                    var exePath = GetApplicationPath();

                    // Check if the registry value matches our current exe path
                    return !string.IsNullOrEmpty(value) && value.Equals(exePath, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                logger?.Error("Failed to check startup status", "StartupManager", ex);
                return false;
            }
        }

        /// <summary>
        /// Enables the application to start with Windows
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public static bool EnableStartup()
        {
            try
            {
                var exePath = GetApplicationPath();

                if (string.IsNullOrEmpty(exePath))
                {
                    logger?.Error("Could not get application path for startup registration", "StartupManager");
                    return false;
                }

                using (var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true))
                {
                    if (key == null)
                    {
                        logger?.Error("Could not open startup registry key for writing", "StartupManager");
                        return false;
                    }

                    key.SetValue(AppName, exePath, RegistryValueKind.String);
                    logger?.Info($"Successfully enabled startup with path: {exePath}", "StartupManager");
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger?.Error("Failed to enable startup", "StartupManager", ex);
                return false;
            }
        }

        /// <summary>
        /// Disables the application from starting with Windows
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public static bool DisableStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true))
                {
                    if (key == null)
                    {
                        logger?.Warning("Could not open startup registry key for writing", "StartupManager");
                        return false;
                    }

                    // Check if the value exists before trying to delete
                    if (key.GetValue(AppName) != null)
                    {
                        key.DeleteValue(AppName, false);
                        logger?.Info("Successfully disabled startup", "StartupManager");
                    }
                    else
                    {
                        logger?.Debug("Startup was already disabled", "StartupManager");
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                logger?.Error("Failed to disable startup", "StartupManager", ex);
                return false;
            }
        }

        /// <summary>
        /// Toggles the startup setting
        /// </summary>
        /// <returns>The new state (true = enabled, false = disabled)</returns>
        public static bool ToggleStartup()
        {
            if (IsStartupEnabled())
            {
                DisableStartup();
                return false;
            }
            else
            {
                EnableStartup();
                return true;
            }
        }

        /// <summary>
        /// Gets the full path to the application executable
        /// </summary>
        /// <returns>The application path or empty string if not found</returns>
        private static string GetApplicationPath()
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            {
                return processPath;
            }
            try
            {
                var mainModulePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(mainModulePath) && File.Exists(mainModulePath))
                {
                    if (!mainModulePath.Contains("\\dotnet\\") && !Path.GetDirectoryName(mainModulePath).Contains(".net\\"))
                    {
                        return mainModulePath;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Error("Failed to get application path", "StartupManager", ex);
            }
            var assembly = Assembly.GetEntryAssembly();
            var appName = Process.GetCurrentProcess().ProcessName + ".exe";
            var baseDir = AppContext.BaseDirectory;

            var combinedPath = Path.Combine(baseDir, appName);
            if (File.Exists(combinedPath))
            {
                return combinedPath;
            }
            return Assembly.GetExecutingAssembly().Location;
        }
    }
}
