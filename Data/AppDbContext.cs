using Microsoft.EntityFrameworkCore;
using XjicIT.Dashboard.Models;

namespace XjicIT.Dashboard.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Tile> Tiles => Set<Tile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(u => u.NormalizedUserName).IsUnique();
        modelBuilder.Entity<Tile>().HasIndex(t => t.SortOrder);
        modelBuilder.Entity<Tile>().Property(t => t.Type).HasConversion<string>().HasMaxLength(10);
    }
}
