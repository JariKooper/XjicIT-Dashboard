using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using XjicIT.Dashboard.Models;

namespace XjicIT.Dashboard.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        if (!await db.Users.AnyAsync())
        {
            var hasher = services.GetRequiredService<IPasswordHasher<AppUser>>();
            var admin = new AppUser
            {
                UserName = "admin",
                NormalizedUserName = AppUser.Normalize("admin"),
                Role = Roles.Admin
            };
            admin.PasswordHash = hasher.HashPassword(admin, "admin");
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }
    }
}
