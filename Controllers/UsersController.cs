using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XjicIT.Dashboard.Data;
using XjicIT.Dashboard.Models;
using XjicIT.Dashboard.ViewModels;

namespace XjicIT.Dashboard.Controllers;

[Authorize(Roles = Roles.Admin)]
public class UsersController(AppDbContext db, IPasswordHasher<AppUser> hasher) : Controller
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index()
    {
        ViewBag.CurrentUserId = CurrentUserId;
        var users = await db.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
        return View(users);
    }

    [HttpGet]
    public IActionResult Create() => View(new UserCreateViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        if (!Roles.All.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Invalid role.");

        var normalized = AppUser.Normalize(model.UserName ?? string.Empty);
        if (await db.Users.AnyAsync(u => u.NormalizedUserName == normalized))
            ModelState.AddModelError(nameof(model.UserName), "This username is already taken.");

        if (!ModelState.IsValid) return View(model);

        var user = new AppUser
        {
            UserName = model.UserName!.Trim(),
            NormalizedUserName = normalized,
            Role = model.Role
        };
        user.PasswordHash = hasher.HashPassword(user, model.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        TempData["Message"] = $"User \"{user.UserName}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();
        return View(new UserEditViewModel { Id = user.Id, UserName = user.UserName, Role = user.Role });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UserEditViewModel model)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();
        model.Id = id;
        model.UserName = user.UserName;

        if (!Roles.All.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Invalid role.");

        if (user.Role == Roles.Admin && model.Role != Roles.Admin
            && await db.Users.CountAsync(u => u.Role == Roles.Admin) <= 1)
            ModelState.AddModelError(nameof(model.Role), "You cannot demote the last remaining admin.");

        if (!ModelState.IsValid) return View(model);

        var changed = false;
        if (user.Role != model.Role)
        {
            user.Role = model.Role;
            changed = true;
        }
        if (!string.IsNullOrEmpty(model.NewPassword))
        {
            user.PasswordHash = hasher.HashPassword(user, model.NewPassword);
            changed = true;
        }
        if (changed)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
        }

        TempData["Message"] = changed ? $"User \"{user.UserName}\" updated." : "No changes were made.";
        // Changes to your own account invalidate your session; you will be asked to sign in again.
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();
        ViewBag.Error = await GetDeleteErrorAsync(user);
        return View(user);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();

        var error = await GetDeleteErrorAsync(user);
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        TempData["Message"] = $"User \"{user.UserName}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> GetDeleteErrorAsync(AppUser user)
    {
        if (user.Id == CurrentUserId) return "You cannot delete your own account.";
        if (user.Role == Roles.Admin && await db.Users.CountAsync(u => u.Role == Roles.Admin) <= 1)
            return "You cannot delete the last remaining admin.";
        return null;
    }
}
