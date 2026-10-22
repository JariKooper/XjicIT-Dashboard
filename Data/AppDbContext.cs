using Microsoft.EntityFrameworkCore;
using XjicIT.Dashboard.Models;

namespace XjicIT.Dashboard.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Tile> Tiles => Set<Tile>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryUser> CategoryUsers => Set<CategoryUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(u => u.NormalizedUserName).IsUnique();
        modelBuilder.Entity<Tile>().HasIndex(t => t.SortOrder);
        modelBuilder.Entity<Tile>().Property(t => t.Type).HasConversion<string>().HasMaxLength(10);
        modelBuilder.Entity<Tile>().HasOne(t => t.Category).WithMany(c => c.Tiles)
            .HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CategoryUser>().HasKey(cu => new { cu.CategoryId, cu.UserId });
        modelBuilder.Entity<CategoryUser>().HasOne(cu => cu.Category).WithMany(c => c.AllowedUsers)
            .HasForeignKey(cu => cu.CategoryId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CategoryUser>().HasOne(cu => cu.User).WithMany()
            .HasForeignKey(cu => cu.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
