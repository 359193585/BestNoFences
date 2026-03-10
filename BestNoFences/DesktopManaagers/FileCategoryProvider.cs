using System;
using System.Collections.Generic;

public class FileCategoryProvider
{
    private static readonly Dictionary<string, string> _categoryMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // Documents
            { ".txt", "Documents" },
            { ".doc", "Documents" },
            { ".docx", "Documents" },
            { ".pdf", "Documents" },
            { ".xls", "Documents" },
            { ".xlsx", "Documents" },
            { ".ppt", "Documents" },
            { ".pptx", "Documents" },
            { ".odt", "Documents" },
            { ".rtf", "Documents" },
            // Images
            { ".jpg", "Images" },
            { ".jpeg", "Images" },
            { ".png", "Images" },
            { ".gif", "Images" },
            { ".bmp", "Images" },
            { ".tiff", "Images" },
            { ".webp", "Images" },
            // Videos
            { ".mp4", "Videos" },
            { ".avi", "Videos" },
            { ".mkv", "Videos" },
            { ".mov", "Videos" },
            { ".wmv", "Videos" },
            { ".flv", "Videos" },
            { ".webm", "Videos" },
            // Audio
            { ".mp3", "Audio" },
            { ".wav", "Audio" },
            { ".flac", "Audio" },
            { ".aac", "Audio" },
            { ".ogg", "Audio" },
            // Archives
            { ".zip", "Archives" },
            { ".rar", "Archives" },
            { ".7z", "Archives" },
            { ".tar", "Archives" },
            { ".gz", "Archives" },
            // Executables
            { ".exe", "Executables" },
            { ".msi", "Executables" },
            { ".bat", "Executables" },
            { ".cmd", "Executables" },
            // Code
            { ".cs", "Code" },
            { ".js", "Code" },
            { ".html", "Code" },
            { ".css", "Code" },
            { ".cpp", "Code" },
            { ".h", "Code" },
            // exclude shortcuts
             { ".lnk", "Shortcuts" },
        };

    public static string GetCategory(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return "Others";

        return _categoryMap.TryGetValue(extension, out var category) ? category : "Others";
    }

    public static IEnumerable<string> GetAllExtensions() => _categoryMap.Keys;
}
