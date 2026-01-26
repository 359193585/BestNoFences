using DarkUI.Controls;
using DarkUI.Forms;
using Fenceless.Util;
using Fenceless.Win32;
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Fenceless.UI
{
    public partial class LogViewerForm : Form
    {
        private Panel toolbarPanel;
        private DarkButton refreshButton;
        private DarkButton clearButton;
        private DarkButton saveButton;
        private DarkCheckBox autoScrollCheckBox;
        private DarkComboBox logLevelComboBox;
        private DarkTextBox logTextBox;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;

        private readonly string logFilePath;
        private readonly Logger logger;
        private DateTime lastUpdateTime;
        private DarkLabel logLevelLabel;
        private Timer refreshTimer;

        public LogViewerForm()
        {
            logger = Logger.Instance;
            logFilePath = Path.Combine(logger.appDataPath, "application.log");

            InitializeComponent();
            LoadLogContent();
            SetupRefreshTimer();
        }
        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form setup
            this.Name = "LogViewerForm";
            this.Text = "Log Viewer";
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = true;
            ClientSize = new Size(500, 250);
            Name = "LogViewerForm";
            CreateControls();
            this.ResumeLayout(false);
        }
        private void CreateControls()
        {
            toolbarPanel = new Panel();
            logLevelLabel = new DarkUI.Controls.DarkLabel();
            refreshButton = new DarkUI.Controls.DarkButton();
            clearButton = new DarkUI.Controls.DarkButton();
            saveButton = new DarkUI.Controls.DarkButton();
            autoScrollCheckBox = new DarkUI.Controls.DarkCheckBox();
            logLevelComboBox = new DarkUI.Controls.DarkComboBox();
            logTextBox = new DarkUI.Controls.DarkTextBox();
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            toolbarPanel.SuspendLayout();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // toolbarPanel
            // 
            toolbarPanel.BackColor = SystemColors.ActiveCaptionText;
            toolbarPanel.Controls.Add(logLevelLabel);
            toolbarPanel.Controls.Add(refreshButton);
            toolbarPanel.Controls.Add(clearButton);
            toolbarPanel.Controls.Add(saveButton);
            toolbarPanel.Controls.Add(autoScrollCheckBox);
            toolbarPanel.Controls.Add(logLevelComboBox);
            toolbarPanel.Dock = DockStyle.Top;
            toolbarPanel.ForeColor = SystemColors.ButtonFace;
            toolbarPanel.Location = new Point(0, 0);
            toolbarPanel.Name = "toolbarPanel";
            toolbarPanel.Size = new Size(984, 39);
            toolbarPanel.TabIndex = 1;
            // 
            // logLevelLabel
            // 
            logLevelLabel.ForeColor = Color.FromArgb(220, 220, 220);
            logLevelLabel.Location = new Point(401, 2);
            logLevelLabel.Name = "logLevelLabel";
            logLevelLabel.Size = new Size(64, 24);
            logLevelLabel.TabIndex = 4;
            logLevelLabel.Text = "Filter:";
            logLevelLabel.TextAlign = ContentAlignment.MiddleRight;
            // 
            // refreshButton
            // 
            refreshButton.ForeColor = SystemColors.ButtonFace;
            refreshButton.Location = new Point(12, 4);
            refreshButton.Name = "refreshButton";
            refreshButton.Padding = new Padding(5);
            refreshButton.Size = new Size(75, 23);
            refreshButton.TabIndex = 0;
            refreshButton.Text = "Refresh";
            refreshButton.Click += this.RefreshButton_Click;
            // 
            // clearButton
            // 
            clearButton.ForeColor = SystemColors.ButtonFace;
            clearButton.Location = new Point(93, 4);
            clearButton.Name = "clearButton";
            clearButton.Padding = new Padding(5);
            clearButton.Size = new Size(75, 23);
            clearButton.TabIndex = 1;
            clearButton.Text = "Clear-Log";
            clearButton.Click += this.ClearButton_Click;
            // 
            // saveButton
            // 
            saveButton.ForeColor = SystemColors.ButtonFace;
            saveButton.Location = new Point(174, 4);
            saveButton.Name = "saveButton";
            saveButton.Padding = new Padding(5);
            saveButton.Size = new Size(75, 23);
            saveButton.TabIndex = 2;
            saveButton.Text = "Save As...";
            saveButton.Click += this.SaveButton_Click;
            // 
            // autoScrollCheckBox
            // 
            autoScrollCheckBox.ForeColor = SystemColors.ButtonFace;
            autoScrollCheckBox.Location = new Point(255, 3);
            autoScrollCheckBox.Name = "autoScrollCheckBox";
            autoScrollCheckBox.Size = new Size(104, 24);
            autoScrollCheckBox.TabIndex = 3;
            autoScrollCheckBox.Text = "Auto-scroll";
            // 
            // logLevelComboBox
            // 
            logLevelComboBox.DrawMode = DrawMode.OwnerDrawVariable;
            logLevelComboBox.Location = new Point(460, 2);
            logLevelComboBox.Name = "logLevelComboBox";
            logLevelComboBox.Size = new Size(121, 24);
            logLevelComboBox.TabIndex = 5;
            // 
            // logTextBox
            // 
            logTextBox.BackColor = Color.FromArgb(69, 73, 74);
            logTextBox.BorderStyle = BorderStyle.FixedSingle;
            logTextBox.Dock = DockStyle.Fill;
            logTextBox.ForeColor = Color.FromArgb(220, 220, 220);
            logTextBox.Location = new Point(0, 39);
            logTextBox.Multiline = true;
            logTextBox.Name = "logTextBox";
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.Size = new Size(984, 600);
            logTextBox.TabIndex = 0;
            // 
            // statusStrip
            // 
            statusStrip.BackColor = SystemColors.ControlDarkDark;
            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel });
            statusStrip.Location = new Point(0, 639);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(984, 22);
            statusStrip.TabIndex = 2;
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(0, 17);
            // 
            // LogViewerForm
            // 
            ClientSize = new Size(984, 661);
            Controls.Add(logTextBox);
            Controls.Add(toolbarPanel);
            Controls.Add(statusStrip);
            MinimumSize = new Size(800, 500);
            Name = "LogViewerForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Fenceless - Log Viewer";
            toolbarPanel.ResumeLayout(false);
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
      

        private void SetupRefreshTimer()
        {
            refreshTimer = new Timer
            {
                Interval = 2000 // Refresh every 2 seconds
            };
            refreshTimer.Tick += (s, e) =>
            {
                if (autoScrollCheckBox.Checked)
                {
                    RefreshLogContent();
                }
            };
            refreshTimer.Start();
        }

        private void LoadLogContent()
        {
            logLevelComboBox.Items.AddRange(new[] { "All", "Debug", "Info", "Warning", "Error", "Critical" });
            logLevelComboBox.SelectedIndex = 0;
            logLevelComboBox.SelectedIndexChanged += LogLevelComboBox_SelectedIndexChanged;

            try
            {
                if (File.Exists(logFilePath))
                {
                    var content = File.ReadAllText(logFilePath);
                    FilterAndDisplayLogs(content);

                    var fileInfo = new FileInfo(logFilePath);
                    lastUpdateTime = fileInfo.LastWriteTime;
                    statusLabel.Text = $"Log loaded - {FormatFileSize(fileInfo.Length)} - Last updated: {lastUpdateTime:HH:mm:ss}";
                }
                else
                {
                    logTextBox.Text = "No log file found. Logs will appear here once the application starts logging.";
                    statusLabel.Text = "No log file found";
                }
            }
            catch (Exception ex)
            {
                logTextBox.Text = $"Error loading log file: {ex.Message}";
                statusLabel.Text = "Error loading log";
            }
        }

        private void RefreshLogContent()
        {
            try
            {
                if (File.Exists(logFilePath))
                {
                    var fileInfo = new FileInfo(logFilePath);
                    if (fileInfo.LastWriteTime > lastUpdateTime)
                    {
                        LoadLogContent();

                        if (autoScrollCheckBox.Checked)
                        {
                            logTextBox.SelectionStart = logTextBox.Text.Length;
                            logTextBox.ScrollToCaret();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error refreshing: {ex.Message}";
            }
        }

        private void FilterAndDisplayLogs(string content)
        {
            var selectedLevel = logLevelComboBox.SelectedItem?.ToString() ?? "All";

            if (selectedLevel == "All")
            {
                logTextBox.Text = content;
                return;
            }

            var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var filteredLines = new StringBuilder();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    filteredLines.AppendLine(line);
                    continue;
                }

                // Check if line contains the selected log level
                if (line.Contains($"[{selectedLevel.ToUpper().PadRight(8)}]"))
                {
                    filteredLines.AppendLine(line);
                }
            }

            logTextBox.Text = filteredLines.ToString();
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024 * 1024 * 1024):F1} GB";
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            LoadLogContent();
            if (autoScrollCheckBox.Checked)
            {
                logTextBox.SelectionStart = logTextBox.Text.Length;
                logTextBox.ScrollToCaret();
            }
            statusLabel.Text = "Log refreshed manually";
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "This will permanently clear the log file. Are you sure?",
                "Clear Log File",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    File.WriteAllText(logFilePath, string.Empty);
                    logTextBox.Clear();
                    statusLabel.Text = "Log file cleared";
                    logger.Info("Log file cleared by user", "LogViewer");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error clearing log: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*";
                saveDialog.DefaultExt = "txt";
                saveDialog.FileName = $"Fenceless_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                saveDialog.Title = "Save Log File";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(saveDialog.FileName, logTextBox.Text);
                        statusLabel.Text = $"Log saved to: {Path.GetFileName(saveDialog.FileName)}";
                        logger.Info($"Log exported to: {saveDialog.FileName}", "LogViewer");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving log: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void LogLevelComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (File.Exists(logFilePath))
            {
                var content = File.ReadAllText(logFilePath);
                FilterAndDisplayLogs(content);
                statusLabel.Text = $"Filtered to show: {logLevelComboBox.SelectedItem}";
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Allow normal closing behavior instead of hiding
            refreshTimer?.Stop();
            refreshTimer?.Dispose();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refreshTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void toolbarPanel_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}