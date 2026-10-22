using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XjicIT.Dashboard.Data;
using XjicIT.Dashboard.Models;

namespace XjicIT.Dashboard.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AdminController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.TileCount = await db.Tiles.CountAsync();
        ViewBag.CategoryCount = await db.Categories.CountAsync();
        ViewBag.UserCount = await db.Users.CountAsync();
        ViewBag.AdminCount = await db.Users.CountAsync(u => u.Role == Roles.Admin);
        return View();
    }
}
