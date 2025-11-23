using System;

public class ModelInfo
{
    public string Name { get; set; }
    public string Type { get; set; }
    public long Size { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime ModifiedTime { get; set; }
    
    public string GetFormattedSize()
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = Size;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}