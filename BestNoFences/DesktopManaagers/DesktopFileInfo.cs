using System;
using System.Collections.Generic;

public class DesktopFileInfo
{
    public string FullPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastWriteTime { get; set; }
    public DateTime CreationTime { get; set; }
    public string Category { get; set; } = "Others";
}

public class DesktopStatistics
{
    public int TotalFiles { get; set; }
    public int TotalFilesExcludeLnk { get; set; }
    public long TotalSize { get; set; }
    public Dictionary<string, int> CountByCategory { get; set; } = new();
    public Dictionary<string, long> SizeByCategory { get; set; } = new();
    public List<string> TargetFolders { get; set; } = new();
}

public class OperationRecord
{
    public DateTime OperationTime { get; set; }
    public List<FileMoveRecord> MovedFiles { get; set; } = new();
}

public class FileMoveRecord
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
}

