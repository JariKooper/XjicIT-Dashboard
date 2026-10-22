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
        var tiles = await db.Tiles.AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToListAsync();
        return View(tiles);
    }

    /// <summary>Opens a tile's target. Files are streamed only through this authorized action.</summary>
    public async Task<IActionResult> Open(int id)
    {
        var tile = await db.Tiles.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id);
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

    [AllowAnonymous]
    public IActionResult Error() => View();
}
