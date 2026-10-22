using System.ComponentModel.DataAnnotations;
using XjicIT.Dashboard.Models;

namespace XjicIT.Dashboard.ViewModels;

public class TileFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(64)]
    [RegularExpression(@"^bi-[a-z0-9-]+$", ErrorMessage = "Use a Bootstrap Icons class such as bi-file-earmark or bi-globe.")]
    public string? Icon { get; set; }

    [Display(Name = "Tile type")]
    public TileType Type { get; set; } = TileType.Url;

    [StringLength(2048), Display(Name = "URL")]
    public string? Url { get; set; }

    [Display(Name = "File")]
    public IFormFile? Upload { get; set; }

    /// <summary>Name of the currently stored file (edit mode only).</summary>
    public string? CurrentFileName { get; set; }

    public bool IsEdit => Id.HasValue;
}
