using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using XjicIT.Dashboard.Data;
using XjicIT.Dashboard.Models;
using XjicIT.Dashboard.Services;

var builder = WebApplication.CreateBuilder(args);

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDir);

var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=App_Data/dashboard.db";
connectionString = connectionString.Replace("Data Source=App_Data/", $"Data Source={dataDir}{Path.DirectorySeparatorChar}");

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddSingleton<FileStorageService>();

var maxUploadBytes = builder.Configuration.GetValue("Uploads:MaxFileSizeMB", 50) * 1024L * 1024L;
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxUploadBytes + 1024 * 1024);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxUploadBytes + 1024 * 1024);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        // Reject cookies of deleted users or users whose role/password changed since sign-in.
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var idValue = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var stamp = ctx.Principal?.FindFirstValue(AppClaims.SecurityStamp);
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var valid = int.TryParse(idValue, out var id)
                && await db.Users.AnyAsync(u => u.Id == id && u.SecurityStamp == stamp);
            if (!valid)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews(o =>
{
    // Every endpoint requires an authenticated user unless marked [AllowAnonymous].
    o.Filters.Add(new AuthorizeFilter());
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
    await UpgradeSchemaAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
}

// EnsureCreated does not alter an existing database, so add newer tables/columns idempotently.
static async Task UpgradeSchemaAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "Categories" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Categories" PRIMARY KEY AUTOINCREMENT,
            "Name" TEXT NOT NULL,
            "SortOrder" INTEGER NOT NULL,
            "IsPublic" INTEGER NOT NULL,
            "CreatedUtc" TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS "CategoryUsers" (
            "CategoryId" INTEGER NOT NULL,
            "UserId" INTEGER NOT NULL,
            CONSTRAINT "PK_CategoryUsers" PRIMARY KEY ("CategoryId", "UserId"),
            CONSTRAINT "FK_CategoryUsers_Categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_CategoryUsers_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS "IX_CategoryUsers_UserId" ON "CategoryUsers" ("UserId");
        """);

    async Task AddColumnAsync(string column, string definition)
    {
        var exists = (await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM pragma_table_info('Tiles') WHERE name = {0}", column)
            .ToListAsync()).Single() > 0;
        if (!exists)
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"Tiles\" ADD COLUMN \"{column}\" {definition};");
    }

    await AddColumnAsync("IconStoredFileName", "TEXT NULL");
    await AddColumnAsync("IconContentType", "TEXT NULL");
    await AddColumnAsync("CategoryId", "INTEGER NOT NULL DEFAULT 0");
    await db.Database.ExecuteSqlRawAsync("""
        CREATE INDEX IF NOT EXISTS "IX_Tiles_CategoryId" ON "Tiles" ("CategoryId");
        INSERT INTO "Categories" ("Name", "SortOrder", "IsPublic", "CreatedUtc")
            SELECT 'General', 0, 1, strftime('%Y-%m-%d %H:%M:%S', 'now')
            WHERE EXISTS (SELECT 1 FROM "Tiles" WHERE "CategoryId" NOT IN (SELECT "Id" FROM "Categories"))
              AND NOT EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'General');
        UPDATE "Tiles" SET "CategoryId" = (SELECT MIN("Id") FROM "Categories" WHERE "Name" = 'General')
            WHERE "CategoryId" NOT IN (SELECT "Id" FROM "Categories");
        """);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
