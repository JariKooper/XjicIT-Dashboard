using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XjicIT.Dashboard.Data;
using XjicIT.Dashboard.Models;
using XjicIT.Dashboard.ViewModels;

namespace XjicIT.Dashboard.Controllers;

[Authorize(Roles = Roles.Admin)]
public class CategoriesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var categories = await db.Categories.AsNoTracking()
            .Include(c => c.Tiles)
            .Include(c => c.AllowedUsers).ThenInclude(cu => cu.User)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync();
        return View(categories);
    }

    [HttpGet]
    public async Task<IActionResult> Create() => await FormViewAsync(new CategoryFormViewModel
    {
        SortOrder = (await db.Categories.MaxAsync(c => (int?)c.SortOrder) ?? 0) + 1
    });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid) return await FormViewAsync(model);

        var category = new Category();
        await ApplyAsync(category, model);
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        TempData["Message"] = $"Category \"{category.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await db.Categories.AsNoTracking().Include(c => c.AllowedUsers).SingleOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();
        return await FormViewAsync(new CategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            SortOrder = category.SortOrder,
            IsPublic = category.IsPublic,
            SelectedUserIds = category.AllowedUsers.Select(cu => cu.UserId).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategoryFormViewModel model)
    {
        var category = await db.Categories.Include(c => c.AllowedUsers).SingleOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();
        model.Id = id;
        if (!ModelState.IsValid) return await FormViewAsync(model);

        await ApplyAsync(category, model);
        await db.SaveChangesAsync();

        TempData["Message"] = $"Category \"{category.Name}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await db.Categories.AsNoTracking().Include(c => c.Tiles).SingleOrDefaultAsync(c => c.Id == id);
        return category == null ? NotFound() : View(category);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var category = await db.Categories.FindAsync(id);
        if (category == null) return NotFound();
        if (await db.Tiles.AnyAsync(t => t.CategoryId == id))
        {
            TempData["Error"] = $"Category \"{category.Name}\" still contains tiles. Move or delete them first.";
            return RedirectToAction(nameof(Index));
        }
        db.Categories.Remove(category);
        await db.SaveChangesAsync();
        TempData["Message"] = $"Category \"{category.Name}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> FormViewAsync(CategoryFormViewModel model)
    {
        model.Users = await db.Users.AsNoTracking().OrderBy(u => u.UserName)
            .Select(u => new CategoryFormViewModel.UserOption(u.Id, u.UserName))
            .ToListAsync();
        return View("Form", model);
    }

    private async Task ApplyAsync(Category category, CategoryFormViewModel model)
    {
        category.Name = model.Name.Trim();
        category.SortOrder = model.SortOrder;
        category.IsPublic = model.IsPublic;

        // Private categories keep an explicit allow-list; public ones need none.
        List<int> selected = model.IsPublic
            ? []
            : await db.Users.Where(u => model.SelectedUserIds.Contains(u.Id)).Select(u => u.Id).ToListAsync();
        category.AllowedUsers.RemoveAll(cu => !selected.Contains(cu.UserId));
        foreach (var userId in selected.Where(uid => category.AllowedUsers.All(cu => cu.UserId != uid)))
            category.AllowedUsers.Add(new CategoryUser { UserId = userId });
    }
}
