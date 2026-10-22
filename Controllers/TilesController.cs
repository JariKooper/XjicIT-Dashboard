using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    private const long MaxIconBytes = 1024 * 1024;
    private static readonly string[] IconExtensions = [".png", ".jpg", ".jpeg", ".webp", ".gif"];

    public async Task<IActionResult> Index()
    {
        var tiles = await db.Tiles.AsNoTracking().Include(t => t.Category).OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToListAsync();
        return View(tiles);
    }

    [HttpGet]
    public Task<IActionResult> Create() => FormViewAsync(new TileFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(UploadRequestLimit), RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
    public async Task<IActionResult> Create(TileFormViewModel model)
    {
        Validate(model, requireFile: true);
        await ValidateCategoryAsync(model);
        if (!ModelState.IsValid) return await FormViewAsync(model);

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
        return await FormViewAsync(new TileFormViewModel
        {
            Id = tile.Id,
            Title = tile.Title,
            Description = tile.Description,
            CategoryId = tile.CategoryId,
            HasIcon = tile.IconStoredFileName != null,
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
        model.HasIcon = tile.IconStoredFileName != null;

        Validate(model, requireFile: tile.Type != TileType.File || storage.GetPath(tile.StoredFileName) == null);
        await ValidateCategoryAsync(model);
        if (!ModelState.IsValid) return await FormViewAsync(model);

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
        storage.Delete(tile.IconStoredFileName);
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

        if (model.IconUpload != null)
        {
            var ext = Path.GetExtension(model.IconUpload.FileName).ToLowerInvariant();
            if (!IconExtensions.Contains(ext))
                ModelState.AddModelError(nameof(model.IconUpload), "The icon must be a PNG, JPEG, WebP or GIF image.");
            else if (model.IconUpload.Length == 0)
                ModelState.AddModelError(nameof(model.IconUpload), "The icon file is empty.");
            else if (model.IconUpload.Length > MaxIconBytes)
                ModelState.AddModelError(nameof(model.IconUpload), "The icon exceeds the maximum size of 1 MB.");
            else if (!HasImageSignature(model.IconUpload, ext))
                ModelState.AddModelError(nameof(model.IconUpload), "The icon file content does not match its image type.");
        }
    }

    private static bool HasImageSignature(IFormFile file, string ext)
    {
        var header = new byte[12];
        using (var stream = file.OpenReadStream())
        {
            if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length) return false;
        }
        static bool StartsWith(byte[] data, int offset, params byte[] sig) => data.AsSpan(offset, sig.Length).SequenceEqual(sig.AsSpan());
        return ext switch
        {
            ".png" => StartsWith(header, 0, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            ".jpg" or ".jpeg" => StartsWith(header, 0, 0xFF, 0xD8, 0xFF),
            ".gif" => StartsWith(header, 0, (byte)'G', (byte)'I', (byte)'F', (byte)'8'),
            ".webp" => StartsWith(header, 0, (byte)'R', (byte)'I', (byte)'F', (byte)'F') && StartsWith(header, 8, (byte)'W', (byte)'E', (byte)'B', (byte)'P'),
            _ => false
        };
    }

    private async Task ValidateCategoryAsync(TileFormViewModel model)
    {
        if (model.CategoryId.HasValue && !await db.Categories.AnyAsync(c => c.Id == model.CategoryId))
            ModelState.AddModelError(nameof(model.CategoryId), "The selected category does not exist.");
    }

    private async Task<IActionResult> FormViewAsync(TileFormViewModel model)
    {
        model.Categories = await db.Categories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();
        return View("Form", model);
    }

    private async Task ApplyAsync(Tile tile, TileFormViewModel model)
    {
        tile.Title = model.Title.Trim();
        tile.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        tile.CategoryId = model.CategoryId!.Value;

        if (model.IconUpload != null)
        {
            var icon = await storage.SaveAsync(model.IconUpload, HttpContext.RequestAborted);
            storage.Delete(tile.IconStoredFileName);
            tile.IconStoredFileName = icon.StoredFileName;
            tile.IconContentType = icon.ContentType;
        }
        else if (model.RemoveIcon)
        {
            storage.Delete(tile.IconStoredFileName);
            tile.IconStoredFileName = tile.IconContentType = null;
        }

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
