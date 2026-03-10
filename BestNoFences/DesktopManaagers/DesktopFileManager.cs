using Fenceless.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fenceless.DesktopManaagers
{
    public class DesktopFileManager
    {
        private static readonly Lazy<DesktopFileManager> _instance = new Lazy<DesktopFileManager>(() => new DesktopFileManager());
        public static DesktopFileManager Instance => _instance.Value;
        private string _deskTopPath;
        private string _targetRoot;
        private readonly string _recordFilePath;
        private OperationRecord? _lastOperation;
        private List<DesktopFileInfo>? _currentFiles;
        private readonly List<FileMoveRecord> _moveRecords = new();

        public DesktopFileManager()
        {
            _deskTopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            _recordFilePath = Path.Combine(Path.GetTempPath(), "DesktopManager_record.json");
            _targetRoot = Path.Combine(GetLastAvailableDrive(), "DesktopOrganized");
            LoadLastOperation();
        }

        #region local methods
        private void LoadLastOperation()
        {
            if (File.Exists(_recordFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_recordFilePath);
                    if (string.IsNullOrEmpty(json)) return;
                    _lastOperation = JsonSerializer.Deserialize<OperationRecord>(json);
                }
                catch { }
            }
        }

        private void SaveOperationRecord(OperationRecord record)
        {
            try
            {
                var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_recordFilePath, json);
                _lastOperation = record;
            }
            catch { }
        }
        private DesktopStatistics CalculateStatistics()
        {
            var stats = new DesktopStatistics();
            if (_currentFiles == null) return stats;

            stats.TotalFiles = _currentFiles.Count;
            stats.TotalFilesExcludeLnk = _currentFiles.Count(f => f.Extension.ToLower() != ".lnk");
            stats.TotalSize = _currentFiles.Sum(f => f.Size);
            stats.CountByCategory = _currentFiles.GroupBy(f => f.Category)
                .ToDictionary(g => g.Key, g => g.Count());
            stats.SizeByCategory = _currentFiles.GroupBy(f => f.Category)
                .ToDictionary(g => g.Key, g => g.Sum(f => f.Size));
            if (!string.IsNullOrEmpty(_targetRoot))
                stats.TargetFolders.Add(_targetRoot);

            return stats;
        }
        private static string RenameNew(string file, string destDir, string destFile)
        {
            if (File.Exists(destFile))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
                var ext = Path.GetExtension(file);
                destFile = Path.Combine(destDir, $"{nameWithoutExt}_{Guid.NewGuid():N}{ext}");
            }
            return destFile;
        }
        private async Task<List<FileMoveRecord>> OrganizeAsync()
        {
            await ScanDesktopAsync();
            if (_currentFiles == null || !_currentFiles.Any()) return new List<FileMoveRecord>();

            return await Task.Run(() =>
            {
                _moveRecords.Clear();
                Parallel.ForEach(_currentFiles, file =>
                {
                    if (file.Category == "Shortcuts") return; // skip shortcuts

                    var dateStr = file.LastWriteTime.ToString("yyyy-MM-dd");
                    var destDir = Path.Combine(_targetRoot, file.Category, dateStr);
                    Directory.CreateDirectory(destDir);

                    var destFile = Path.Combine(destDir, file.Name);
                    destFile = RenameNew(file.Name, destDir, destFile);
                    try
                    {
                        File.Move(file.FullPath, destFile);
                        _moveRecords.Add(new FileMoveRecord
                        {
                            SourcePath = file.FullPath,
                            DestinationPath = destFile
                        });
                    }
                    catch { }
                });
                return _moveRecords;
            });

        }

        private async Task<bool> UndoAsync(OperationRecord record)
        {
            return await Task.Run(() =>
            {
                if (record?.MovedFiles == null) return false;

                var success = true;
                Parallel.ForEachAsync(record.MovedFiles, async (move, ct) =>
                {
                    try
                    {
                        if (File.Exists(move.DestinationPath))
                        {
                            //check if source path already exists, if so, rename the destination file before moving back
                            var sourceDir = Path.GetDirectoryName(move.SourcePath) ?? string.Empty;
                            if (Path.Exists(sourceDir))
                            {
                                var destFile = RenameNew(move.DestinationPath, sourceDir, move.SourcePath);

                                File.Move(move.DestinationPath, destFile);
                            }
                        }
                    }
                    catch
                    {
                        success = false;
                    }
                }).Wait();

                foreach (var move in record.MovedFiles)
                {
                    try
                    {
                        if (File.Exists(move.DestinationPath))
                        {
                            // 如果原位置已存在文件，则先备份（添加后缀）
                            if (File.Exists(move.SourcePath))
                            {
                                var backup = move.SourcePath + ".bak";
                                File.Move(move.SourcePath, backup);
                            }
                            File.Move(move.DestinationPath, move.SourcePath);
                        }
                    }
                    catch
                    {
                        success = false;
                    }
                }
                return success;
            });
        }

        private async Task<List<DesktopFileInfo>> ScanDesktopAsync()
        {
            _currentFiles = await Task.Run(() =>
            {
                var result = new List<DesktopFileInfo>();
                foreach (var file in Directory.EnumerateFiles(_deskTopPath))
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        result.Add(new DesktopFileInfo
                        {
                            FullPath = fi.FullName,
                            Name = fi.Name,
                            Extension = fi.Extension,
                            Size = fi.Length,
                            LastWriteTime = fi.LastWriteTime,
                            CreationTime = fi.CreationTime,
                            Category = FileCategoryProvider.GetCategory(fi.Extension)
                        });
                    }
                    catch
                    {

                    }
                }

                return result;
            });
            return _currentFiles;
        }

        private string GetUserSelectRoot()
        {
            string targetRoot = "";
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Please select the root directory for the classified items. (If you do not select one (i.e., click Cancel), the default location will be used: DesktopOrganized under the last drive.)";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    targetRoot = dialog.SelectedPath;
                }
            }

            if (string.IsNullOrEmpty(targetRoot))
            {
                // use defaule location
                targetRoot = Path.Combine(DesktopFileManager.GetLastAvailableDrive(), "DesktopOrganized");
                CustomMessageBox.Show($"Default location is：{targetRoot}", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return targetRoot;
        }
        #endregion

        #region public methods
        public async Task<List<DesktopFileInfo>> ScanDesktopAsync(bool IsNotice = false)
        {
            var result = await ScanDesktopAsync();
            if (IsNotice)
            {
                var stats = CalculateStatistics();
                if (stats.TotalFilesExcludeLnk == 0) return result;

                DialogResult dialogResult = CustomMessageBox.Show("Find files in desktop, do you want organize it? ", "Notice",
                   MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (dialogResult == DialogResult.Yes)
                {
                    var targetRoot = GetUserSelectRoot();
                    await OrganizeAsync(targetRoot);
                }
            }
            return result;
        }
        public async Task<DesktopStatistics> OrganizeAsync(string targetRoot)
        {
            _targetRoot = targetRoot ?? Path.Combine(GetLastAvailableDrive(), "DesktopOrganized");
            try
            {
                Directory.CreateDirectory(_targetRoot);
            }
            catch
            {
                CustomMessageBox.Show($"Dest path is not exist: {_targetRoot} .", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return CalculateStatistics();
            }
            var records = await OrganizeAsync();
            var operation = new OperationRecord
            {
                OperationTime = DateTime.Now,
                MovedFiles = records
            };
            SaveOperationRecord(operation);
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", targetRoot);
            }
            catch { }
            // rescan the desktop to update current files
            _currentFiles = await ScanDesktopAsync();
            return CalculateStatistics();
        }

        public async Task<bool> UndoAsync()
        {
            if (_lastOperation == null) return false;
            var result = await UndoAsync(_lastOperation);
            if (result)
            {
                try { File.Delete(_recordFilePath); } catch { }
                _lastOperation = null;
                _currentFiles = await ScanDesktopAsync();
            }
            return result;
        }

        public DesktopStatistics GetStatistics()
        {
            return CalculateStatistics();
        }


        public string GetLastTargetRoot() => _targetRoot;

        public static string GetLastAvailableDrive()
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .OrderBy(d => d.Name)
                .ToList();

            if (drives.Any())
                return drives.Last().RootDirectory.FullName;

            return Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? "C:\\";
        }
        #endregion
    }

}
