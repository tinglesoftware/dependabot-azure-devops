using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Models.Management;

namespace Tingle.Dependabot.Models;

public class MainDbContext : DbContext, IDataProtectionKeyContext
{
    public MainDbContext(DbContextOptions<MainDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<UpdateJob> UpdateJobs => Set<UpdateJob>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>(b =>
        {
            b.Property(p => p.AutoCompleteIgnoreConfigs).HasJsonConversion();

            b.HasIndex(p => p.Created).IsDescending(); // faster filtering
            b.HasIndex(p => p.ProviderId).IsUnique();
            b.HasIndex(p => p.NotificationsPassword).IsDescending(); // faster filtering
        });

        modelBuilder.Entity<Repository>(b =>
        {
            b.Property(r => r.Updates).HasJsonConversion();
            b.Property(r => r.Registries).HasJsonConversion();

            b.HasIndex(r => r.Created).IsDescending(); // faster filtering
            b.HasIndex(r => r.ProviderId).IsUnique();
        });

        modelBuilder.Entity<UpdateJob>(b =>
        {
            b.Property(j => j.PackageEcosystem).IsRequired();
            b.Property(j => j.Error).HasJsonConversion();

            b.HasIndex(j => j.Created).IsDescending(); // faster filtering
            b.HasIndex(j => j.RepositoryId);
            b.HasIndex(j => new { j.PackageEcosystem, j.Directory, }); // faster filtering
            b.HasIndex(j => new { j.PackageEcosystem, j.Directory, j.EventBusId, }).IsUnique();
            b.HasIndex(j => j.AuthKey).IsUnique();

            b.OwnsOne(j => j.Resources);
        });
    }
}
