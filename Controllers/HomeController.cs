using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using XjicIT.Dashboard.Data;
using XjicIT.Dashboard.Models;
using XjicIT.Dashboard.Services;

namespace XjicIT.Dashboard.Controllers;

public class HomeController(AppDbContext db, FileStorageService storage) : Controller
{
    // File types that are safe to display inline in the browser; everything else is downloaded.
    private static readonly HashSet<string> InlineTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/png", "image/jpeg", "image/gif", "image/webp", "text/plain"
    };

    public async Task<IActionResult> Index()
    {
        var categories = await db.Categories.AsNoTracking().VisibleTo(User)
            .Where(c => c.Tiles.Any())
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Include(c => c.Tiles.OrderBy(t => t.SortOrder).ThenBy(t => t.Id))
            .ToListAsync();
        return View(categories);
    }

    /// <summary>Tiles whose category is visible to the current user.</summary>
    private IQueryable<Tile> VisibleTiles()
    {
        var categoryIds = db.Categories.VisibleTo(User).Select(c => c.Id);
        return db.Tiles.AsNoTracking().Where(t => categoryIds.Contains(t.CategoryId));
    }

    /// <summary>Opens a tile's target. Files are streamed only through this authorized action.</summary>
    public async Task<IActionResult> Open(int id)
    {
        var tile = await VisibleTiles().SingleOrDefaultAsync(t => t.Id == id);
        if (tile == null) return NotFound();

        if (tile.Type == TileType.Url)
        {
            if (!Uri.TryCreate(tile.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return NotFound();
            return Redirect(uri.AbsoluteUri);
        }

        var path = storage.GetPath(tile.StoredFileName);
        if (path == null) return NotFound();

        var contentType = tile.ContentType ?? MediaTypeNames.Application.Octet;
        var fileName = tile.OriginalFileName ?? "file";
        var disposition = new ContentDispositionHeaderValue(InlineTypes.Contains(contentType) ? "inline" : "attachment");
        disposition.SetHttpFileName(fileName);
        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers[HeaderNames.ContentSecurityPolicy] = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";
        return PhysicalFile(path, contentType, enableRangeProcessing: true);
    }

    /// <summary>Serves a tile's icon image; uploads are never exposed as static files.</summary>
    public async Task<IActionResult> Icon(int id)
    {
        var tile = await VisibleTiles().SingleOrDefaultAsync(t => t.Id == id);
        var path = storage.GetPath(tile?.IconStoredFileName);
        if (tile == null || path == null) return NotFound();

        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        return PhysicalFile(path, tile.IconContentType ?? MediaTypeNames.Application.Octet);
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
