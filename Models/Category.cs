using System.ComponentModel.DataAnnotations;

namespace XjicIT.Dashboard.Models;

public class Category
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>Public categories are visible to everyone; private ones only to admins and AllowedUsers.</summary>
    public bool IsPublic { get; set; } = true;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<Tile> Tiles { get; set; } = [];

    public List<CategoryUser> AllowedUsers { get; set; } = [];
}

public class CategoryUser
{
    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public int UserId { get; set; }

    public AppUser User { get; set; } = null!;
}
