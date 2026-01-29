using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fenceless.Util
{
    internal class UpdateNotication
    {
        private static Logger logger;
        internal static async Task CheckForUpdatesAsync()
        {
            logger = Logger.Instance;
            try
            {
                if (logger == null)
                {
                    return; // Logger not initialized yet
                }

                logger.Info("Checking for updates...", "CheckForUpdates");

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Fenceless-UpdateChecker");

                    // Get all releases from github API
                    var response = await httpClient.GetStringAsync("https://api.github.com/repos/359193585/BestNoFences/releases");

                    if (string.IsNullOrEmpty(response))
                    {
                        logger?.Warning("Empty response from services API", "CheckForUpdates");
                        return;
                    }

                    var releasesArray = JArray.Parse(response);

                    if (releasesArray == null || releasesArray.Count == 0)
                    {
                        logger?.Info("No releases found on services", "CheckForUpdates");
                        return;
                    }

                    // Find the latest non-prerelease version
                    JObject latestRelease = null;
                    foreach (var release in releasesArray)
                    {
                        if (release == null) continue;

                        var isPrerelease = release["prerelease"]?.Value<bool>() ?? false;
                        var isDraft = release["draft"]?.Value<bool>() ?? false;

                        if (!isPrerelease && !isDraft)
                        {
                            latestRelease = (JObject)release;
                            break; // Releases are typically ordered by date, so first non-prerelease is latest
                        }
                    }

                    // If no stable release found, use the first release (even if prerelease)
                    if (latestRelease == null && releasesArray.Count > 0)
                    {
                        latestRelease = (JObject)releasesArray[0];
                        logger?.Info("No stable release found, using latest prerelease", "CheckForUpdates");
                    }

                    if (latestRelease == null)
                    {
                        logger?.Warning("Could not find any suitable release", "CheckForUpdates");
                        return;
                    }

                    var latestVersion = latestRelease["tag_name"]?.ToString();
                    if (string.IsNullOrEmpty(latestVersion))
                    {
                        logger?.Warning("Could not parse latest version from API response", "CheckForUpdates");
                        return;
                    }

                    // Remove 'v' prefix if present
                    if (latestVersion.StartsWith("v"))
                        latestVersion = latestVersion.Substring(1);

                    var currentVersion = GetCurrentVersion();
                    if (currentVersion == "v0.0") return;

                    logger?.Info($"Current version: {currentVersion}, Latest version: {latestVersion}", "CheckForUpdates");

                    // Compare versions
                    if (IsNewerVersion(latestVersion, currentVersion))
                    {
                        logger?.Info("New version available, showing update notification", "CheckForUpdates");
                        ShowUpdateNotification(latestVersion);
                    }
                    else
                    {
                        logger?.Info("Application is up to date", "CheckForUpdates");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Error($"Error checking for updates: {ex.Message}", "CheckForUpdates");
            }
        }
        internal static void ShowUpdateNotification(string newVersion)
        {
            try
            {
                if (string.IsNullOrEmpty(newVersion))
                {
                    logger?.Warning("Cannot show update notification with empty version", "ShowUpdateNotification");
                    return;
                }

                var assembly = Assembly.GetExecutingAssembly();
                var currentVersionString = assembly?.GetName()?.Version?.ToString() ?? "Unknown";

                var result = MessageBox.Show(
                    $"A new version of Fenceless is available!\n\nCurrent version: {currentVersionString}\nNew version: {newVersion}\n\nWould you like to visit the releases page to download the update?",
                    "Update Available",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Information
                );

                if (result == DialogResult.OK)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/359193585/BestNoFences/releases/",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                logger?.Error($"Error showing update notification: {ex.Message}", "ShowUpdateNotification");
            }
        }

        internal static bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            try
            {
                if (string.IsNullOrEmpty(latestVersion) || string.IsNullOrEmpty(currentVersion))
                {
                    logger?.Warning("Invalid version strings for comparison", "IsNewerVersion");
                    return false;
                }

                var latest = new Version(latestVersion);
                var current = new Version(currentVersion);
                return latest > current;
            }
            catch (Exception ex)
            {
                logger?.Error($"Error comparing versions: {ex.Message}", "IsNewerVersion");
                return false;
            }
        }

        internal static string GetCurrentVersion()
        {
            // Get current version from assembly
            var assembly = Assembly.GetExecutingAssembly();
            if (assembly?.GetName()?.Version == null)
            {
                logger?.Warning("Could not get current assembly version", "CheckForUpdates");
                return "v0.0";
            }
            var currentVersion = assembly.GetName().Version.ToString();
            return currentVersion;
        }
    }
}
