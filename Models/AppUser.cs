using System.ComponentModel.DataAnnotations;

namespace XjicIT.Dashboard.Models;

public class AppUser
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Upper-cased user name used for case-insensitive unique lookups.</summary>
    [Required, MaxLength(64)]
    public string NormalizedUserName { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string Role { get; set; } = Roles.User;

    /// <summary>Changes whenever credentials or role change; invalidates existing sign-in cookies.</summary>
    [Required]
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public static string Normalize(string userName) => userName.Trim().ToUpperInvariant();
}
