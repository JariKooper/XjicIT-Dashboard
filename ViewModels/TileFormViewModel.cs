using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using XjicIT.Dashboard.Models;

namespace XjicIT.Dashboard.ViewModels;

public class TileFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose a category."), Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Display(Name = "Icon image")]
    public IFormFile? IconUpload { get; set; }

    [Display(Name = "Remove icon")]
    public bool RemoveIcon { get; set; }

    /// <summary>Whether the tile currently has an icon image (edit mode only).</summary>
    public bool HasIcon { get; set; }

    public List<SelectListItem> Categories { get; set; } = [];

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
