using System.ComponentModel.DataAnnotations;

namespace XjicIT.Dashboard.ViewModels;

public class CategoryFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Sort order")]
    public int SortOrder { get; set; }

    [Display(Name = "Access")]
    public bool IsPublic { get; set; } = true;

    /// <summary>Users allowed to see the category when it is private.</summary>
    public List<int> SelectedUserIds { get; set; } = [];

    public List<UserOption> Users { get; set; } = [];

    public bool IsEdit => Id.HasValue;

    public record UserOption(int Id, string UserName);
}
