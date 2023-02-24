using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Tingle.Dependabot.Models;

public class MainDbContext : DbContext, IDataProtectionKeyContext
{
    public MainDbContext(DbContextOptions<MainDbContext> options) : base(options) { }

    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<UpdateJob> UpdateJobs => Set<UpdateJob>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Repository>(b =>
        {
            b.HasIndex(r => r.Created).IsDescending(); // faster filtering
            b.HasIndex(r => r.ProviderId).IsUnique();

            b.OwnsMany(r => r.Updates).ToJson();
            b.OwnsMany(r => r.Registries).ToJson();
        });

        modelBuilder.Entity<UpdateJob>(b =>
        {
            b.HasIndex(j => j.Created).IsDescending(); // faster filtering

            b.HasIndex(j => j.RepositoryId);
            b.HasIndex(j => new { j.PackageEcosystem, j.Directory, }); // faster filtering
            b.HasIndex(j => new { j.PackageEcosystem, j.Directory, j.EventBusId, }).IsUnique();
            b.HasIndex(j => j.AuthKey).IsUnique();

            b.OwnsOne(j => j.Resources);
        });
    }
}
