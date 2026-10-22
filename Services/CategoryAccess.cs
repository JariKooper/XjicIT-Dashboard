using System.Security.Claims;
using XjicIT.Dashboard.Models;

namespace XjicIT.Dashboard.Services;

public static class CategoryAccess
{
    /// <summary>
    /// Filters to categories the user may see: public ones, all of them for admins,
    /// and private ones the user has been explicitly allowed into.
    /// </summary>
    public static IQueryable<Category> VisibleTo(this IQueryable<Category> categories, ClaimsPrincipal user)
    {
        if (user.IsInRole(Roles.Admin)) return categories;
        var userId = int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : -1;
        return categories.Where(c => c.IsPublic || c.AllowedUsers.Any(u => u.UserId == userId));
    }
}
