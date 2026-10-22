using System.Text;
using Microsoft.AspNetCore.StaticFiles;

namespace XjicIT.Dashboard.Services;

/// <summary>Stores uploaded files outside wwwroot under generated names.</summary>
public class FileStorageService
{
    private readonly string _root;
    private readonly FileExtensionContentTypeProvider _contentTypes = new();

    public FileStorageService(IWebHostEnvironment env, IConfiguration config)
    {
        var folder = config["Uploads:Folder"] ?? "App_Data/uploads";
        _root = Path.GetFullPath(Path.Combine(env.ContentRootPath, folder));
        Directory.CreateDirectory(_root);
        MaxFileSizeBytes = config.GetValue("Uploads:MaxFileSizeMB", 50) * 1024L * 1024L;
    }

    public long MaxFileSizeBytes { get; }

    public string RootPath => _root;

    public async Task<StoredFile> SaveAsync(IFormFile file, CancellationToken ct = default)
    {
        var original = SanitizeFileName(file.FileName);
        var ext = Path.GetExtension(original).ToLowerInvariant();
        if (ext.Length > 20) ext = string.Empty;
        var stored = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(_root, stored);

        await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        {
            await file.CopyToAsync(stream, ct);
        }

        if (!_contentTypes.TryGetContentType(original, out var contentType))
            contentType = "application/octet-stream";

        return new StoredFile(stored, original, contentType, file.Length);
    }

    /// <summary>Returns the full path for a stored file name, or null if invalid/missing.</summary>
    public string? GetPath(string? storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName)) return null;
        var name = Path.GetFileName(storedFileName);
        if (name != storedFileName) return null;
        var full = Path.GetFullPath(Path.Combine(_root, name));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return null;
        return File.Exists(full) ? full : null;
    }

    public void Delete(string? storedFileName)
    {
        var path = GetPath(storedFileName);
        if (path != null) File.Delete(path);
    }

    public static string SanitizeFileName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        var invalid = Path.GetInvalidFileNameChars().Concat(['"', '<', '>', '|', ':', '*', '?', '/', '\\']).ToHashSet();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(char.IsControl(c) || invalid.Contains(c) ? '_' : c);
        var result = sb.ToString().Trim().Trim('.');
        if (result.Length > 200)
        {
            var ext = Path.GetExtension(result);
            if (ext.Length > 20) ext = string.Empty;
            result = result[..(200 - ext.Length)] + ext;
        }
        return string.IsNullOrWhiteSpace(result) ? "file" : result;
    }
}

public record StoredFile(string StoredFileName, string OriginalFileName, string ContentType, long Size);
