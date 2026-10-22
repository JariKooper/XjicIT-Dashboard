using System.ComponentModel.DataAnnotations;
using XjicIT.Dashboard.Models;

namespace XjicIT.Dashboard.ViewModels;

public class UserCreateViewModel
{
    [Required, StringLength(64, MinimumLength = 2), Display(Name = "Username")]
    [RegularExpression(@"^[A-Za-z0-9._@-]+$", ErrorMessage = "Only letters, digits and . _ @ - are allowed.")]
    public string UserName { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 4), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = Roles.User;
}

public class UserEditViewModel
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = Roles.User;

    [StringLength(128, MinimumLength = 4), DataType(DataType.Password), Display(Name = "New password")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password), Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }
}
