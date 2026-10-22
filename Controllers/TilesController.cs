using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XjicIT.Dashboard.Data;
using XjicIT.Dashboard.Models;
using XjicIT.Dashboard.Services;
using XjicIT.Dashboard.ViewModels;

namespace XjicIT.Dashboard.Controllers;

[Authorize(Roles = Roles.Admin)]
public class TilesController(AppDbContext db, FileStorageService storage) : Controller
{
    private const long UploadRequestLimit = 60L * 1024 * 1024;

    public async Task<IActionResult> Index()
    {
        var tiles = await db.Tiles.AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToListAsync();
        return View(tiles);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new TileFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(UploadRequestLimit), RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
    public async Task<IActionResult> Create(TileFormViewModel model)
    {
        Validate(model, requireFile: true);
        if (!ModelState.IsValid) return View("Form", model);

        var tile = new Tile
        {
            SortOrder = (await db.Tiles.MaxAsync(t => (int?)t.SortOrder) ?? 0) + 1
        };
        await ApplyAsync(tile, model);
        db.Tiles.Add(tile);
        await db.SaveChangesAsync();

        TempData["Message"] = $"Tile \"{tile.Title}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var tile = await db.Tiles.FindAsync(id);
        if (tile == null) return NotFound();
        return View("Form", new TileFormViewModel
        {
            Id = tile.Id,
            Title = tile.Title,
            Description = tile.Description,
            Icon = tile.Icon,
            Type = tile.Type,
            Url = tile.Url,
            CurrentFileName = tile.OriginalFileName
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(UploadRequestLimit), RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
    public async Task<IActionResult> Edit(int id, TileFormViewModel model)
    {
        var tile = await db.Tiles.FindAsync(id);
        if (tile == null) return NotFound();
        model.Id = id;
        model.CurrentFileName = tile.Type == TileType.File ? tile.OriginalFileName : null;

        Validate(model, requireFile: tile.Type != TileType.File || storage.GetPath(tile.StoredFileName) == null);
        if (!ModelState.IsValid) return View("Form", model);

        await ApplyAsync(tile, model);
        await db.SaveChangesAsync();

        TempData["Message"] = $"Tile \"{tile.Title}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var tile = await db.Tiles.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id);
        return tile == null ? NotFound() : View(tile);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var tile = await db.Tiles.FindAsync(id);
        if (tile == null) return NotFound();
        storage.Delete(tile.StoredFileName);
        db.Tiles.Remove(tile);
        await db.SaveChangesAsync();
        TempData["Message"] = $"Tile \"{tile.Title}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(int id, string direction)
    {
        var tiles = await db.Tiles.OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToListAsync();
        var index = tiles.FindIndex(t => t.Id == id);
        if (index < 0) return NotFound();

        var target = direction == "up" ? index - 1 : index + 1;
        if (target >= 0 && target < tiles.Count)
            (tiles[index], tiles[target]) = (tiles[target], tiles[index]);

        for (var i = 0; i < tiles.Count; i++) tiles[i].SortOrder = i + 1;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private void Validate(TileFormViewModel model, bool requireFile)
    {
        if (model.Type == TileType.Url)
        {
            if (string.IsNullOrWhiteSpace(model.Url))
                ModelState.AddModelError(nameof(model.Url), "A URL is required for URL tiles.");
            else if (!Uri.TryCreate(model.Url.Trim(), UriKind.Absolute, out var uri)
                     || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                ModelState.AddModelError(nameof(model.Url), "The URL must start with http:// or https://.");
        }
        else if (model.Type == TileType.File)
        {
            if (model.Upload == null)
            {
                if (requireFile) ModelState.AddModelError(nameof(model.Upload), "Please choose a file to upload.");
            }
            else if (model.Upload.Length == 0)
                ModelState.AddModelError(nameof(model.Upload), "The uploaded file is empty.");
            else if (model.Upload.Length > storage.MaxFileSizeBytes)
                ModelState.AddModelError(nameof(model.Upload), $"The file exceeds the maximum size of {storage.MaxFileSizeBytes / 1024 / 1024} MB.");
        }
        else
        {
            ModelState.AddModelError(nameof(model.Type), "Invalid tile type.");
        }
    }

    private async Task ApplyAsync(Tile tile, TileFormViewModel model)
    {
        tile.Title = model.Title.Trim();
        tile.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        tile.Icon = string.IsNullOrWhiteSpace(model.Icon) ? null : model.Icon.Trim();

        if (model.Type == TileType.Url)
        {
            storage.Delete(tile.StoredFileName);
            tile.StoredFileName = tile.OriginalFileName = tile.ContentType = null;
            tile.FileSize = null;
            tile.Url = new Uri(model.Url!.Trim()).AbsoluteUri;
        }
        else
        {
            tile.Url = null;
            if (model.Upload != null)
            {
                var saved = await storage.SaveAsync(model.Upload, HttpContext.RequestAborted);
                storage.Delete(tile.StoredFileName);
                tile.StoredFileName = saved.StoredFileName;
                tile.OriginalFileName = saved.OriginalFileName;
                tile.ContentType = saved.ContentType;
                tile.FileSize = saved.Size;
            }
        }
        tile.Type = model.Type;
    }
}
