using System.ComponentModel.DataAnnotations;

namespace XjicIT.Dashboard.Models;

public enum TileType
{
    Url = 0,
    File = 1
}

public class Tile
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Generated file name of the optional icon image (inside the uploads folder).</summary>
    [MaxLength(100)]
    public string? IconStoredFileName { get; set; }

    [MaxLength(100)]
    public string? IconContentType { get; set; }

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public TileType Type { get; set; }

    [MaxLength(2048)]
    public string? Url { get; set; }

    /// <summary>Generated file name on disk (inside the uploads folder).</summary>
    [MaxLength(100)]
    public string? StoredFileName { get; set; }

    /// <summary>Sanitized original file name, used for display and download.</summary>
    [MaxLength(255)]
    public string? OriginalFileName { get; set; }

    [MaxLength(255)]
    public string? ContentType { get; set; }

    public long? FileSize { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
