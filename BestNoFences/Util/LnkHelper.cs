using System;
using System.IO;

namespace Fenceless.Util
{
    public class LnkFileManager
    {
        public string TargetFilePath { get; set; }
        public string ShortcutFilePath { get; set; }
        public string ShortcutName { get; set; }
        public string Arguments { get; set; }
        public string Description { get; set; }
        public string WorkingDirectory { get; set; }
        public string IconLocation { get; set; }
        public bool IsValid { get; set; }
        public bool TargetExists { get; set; }
        public string AnalysisMessage { get; set; }
        public Exception Error { get; set; }
        public LnkIssueType IssueType { get; set; }
    }
    public enum LnkIssueType
    {
        Normal,
        None,
        LnkFileNotFound,
        LnkFileCorrupted,
        TargetNotFound,
        WorkingDirectoryNotFound,
        IconNotFound,
        AccessDenied,
        InvalidFormat
    }
    internal class LnkHelper
    {
        private readonly Logger _logger;
        public LnkHelper()
        {
            _logger = Logger.Instance;
        }

        public LnkFileManager AnalyzeLnkFile(string lnkFilePath)
        {
            var _return = new LnkFileManager();
            if (!File.Exists(lnkFilePath))
                return new LnkFileManager { IssueType = LnkIssueType.LnkFileNotFound };
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    _logger.Debug($"can not get  WScript.Shell ", "LnkHelper");
                    return _return;
                }

                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(lnkFilePath);
                _return.ShortcutFilePath = lnkFilePath;
                _return.TargetFilePath = shortcut.TargetPath as string;
                _return.Arguments = shortcut.Arguments as string; 
                _return.Description = shortcut.Description as string;
                _return.WorkingDirectory = shortcut.WorkingDirectory as string;
                if (!Directory.Exists(_return.WorkingDirectory))
                {
                    _return.IssueType = LnkIssueType.WorkingDirectoryNotFound;
                }
                if (File.Exists(_return.TargetFilePath))
                {
                    _return.IssueType = LnkIssueType.Normal;
                    _return.TargetExists = true;
                    _return.IsValid = true;
                }
                else
                {
                    _return.IssueType = LnkIssueType.TargetNotFound;
                    _return.TargetExists = false;
                    _return.IsValid = false;
                }
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);

            }
            catch (Exception ex)
            {
                _logger.Error($"can not get  WScript.Shell ", "LnkHelper");
            }
            return _return;
        }


        public string CreateShortcutDynamic(LnkFileManager lnk)
        {
            if (!File.Exists(lnk.TargetFilePath)) return null;

            if (!Directory.Exists(lnk.ShortcutFilePath))
                Directory.CreateDirectory(lnk.ShortcutFilePath);

            string shortcutPath = Path.Combine(lnk.ShortcutFilePath, lnk.ShortcutName);
            if (!shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                shortcutPath += ".lnk";

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);

            shortcut.TargetPath = lnk.TargetFilePath;
            shortcut.Arguments = lnk.Arguments;
            shortcut.Description = lnk.Description;
            shortcut.WorkingDirectory = lnk.WorkingDirectory ?? Path.GetDirectoryName(lnk.TargetFilePath);
            shortcut.Save();

            return shortcutPath;
        }
    }
}
