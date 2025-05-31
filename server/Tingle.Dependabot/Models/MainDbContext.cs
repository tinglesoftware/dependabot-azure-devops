using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tingle.Dependabot.Models.Management;
using SC = Tingle.Dependabot.DependabotSerializerContext;

namespace Tingle.Dependabot.Models;

public class MainDbContext(DbContextOptions<MainDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<UpdateJob> UpdateJobs => Set<UpdateJob>();

    DbSet<DataProtectionKey> IDataProtectionKeyContext.DataProtectionKeys => Set<DataProtectionKey>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<AzureDevOpsProjectUrl>()
                            .HaveConversion<AzureDevOpsProjectUrlConverter, AzureDevOpsProjectUrlComparer>();

        configurationBuilder.Properties<DockerImage>()
                            .HaveConversion<DockerImageConverter, DockerImageComparer>();

        configurationBuilder.Properties<DateTimeOffset>()
                            .HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>(builder =>
        {
            builder.OwnsOne(p => p.AutoApprove);
            builder.OwnsOne(p => p.AutoComplete);
            builder.Property(p => p.Secrets).HasJsonConversion(SC.Default.DictionaryStringString);
            builder.Property(p => p.Experiments).HasJsonConversion(SC.Default.DictionaryStringString);

            builder.HasIndex(p => p.Created).IsDescending(); // faster filtering
            builder.HasIndex(p => p.ProviderId).IsUnique();
            builder.HasIndex(p => p.Password).IsUnique(); // password should be unique per project

            builder.HasMany<Repository>().WithOne().HasForeignKey(uc => uc.ProjectId).IsRequired();
        });

        modelBuilder.Entity<Repository>(builder =>
        {
            builder.Property(r => r.Updates).HasJsonConversion(SC.Default.ListRepositoryUpdate);
            builder.Property(r => r.Registries).HasJsonConversion(SC.Default.DictionaryStringDependabotRegistry);

            builder.HasIndex(r => r.Created).IsDescending(); // faster filtering
            builder.HasIndex(r => r.ProviderId).IsUnique();
        });

        modelBuilder.Entity<UpdateJob>(builder =>
        {
            builder.Property(j => j.PackageEcosystem).IsRequired();
            builder.Property(e => e.Errors).HasJsonConversion(SC.Default.ListUpdateJobError);
            builder.Property(e => e.UnknownErrors).HasJsonConversion(SC.Default.ListUpdateJobError);

            builder.HasIndex(j => j.Created).IsDescending(); // faster filtering
            builder.HasIndex(j => j.ProjectId);
            builder.HasIndex(j => j.RepositoryId);
            builder.HasIndex(j => new { j.PackageEcosystem, j.Directory, j.Directories }); // faster filtering
            builder.HasIndex(j => new { j.PackageEcosystem, j.Directory, j.Directories, j.EventBusId, }).IsUnique();
            builder.HasIndex(j => j.AuthKey).IsUnique();

            builder.OwnsOne(j => j.Resources);
        });
    }

    private class AzureDevOpsProjectUrlConverter : ValueConverter<AzureDevOpsProjectUrl, string>
    {
        public AzureDevOpsProjectUrlConverter() : base(convertToProviderExpression: v => v.ToString(),
                                                       convertFromProviderExpression: v => v == null ? default : new AzureDevOpsProjectUrl(v))
        { }
    }
    private class AzureDevOpsProjectUrlComparer : ValueComparer<AzureDevOpsProjectUrl>
    {
        public AzureDevOpsProjectUrlComparer() : base(equalsExpression: (l, r) => l == r,
                                                      hashCodeExpression: v => v.GetHashCode(),
                                                      snapshotExpression: v => new AzureDevOpsProjectUrl(v.ToString()))
        { }
    }

    private class DockerImageConverter : ValueConverter<DockerImage, string>
    {
        public DockerImageConverter() : base(convertToProviderExpression: v => v.ToString(),
                                             convertFromProviderExpression: v => v == null ? default : DockerImage.Parse(v))
        { }
    }
    private class DockerImageComparer : ValueComparer<DockerImage>
    {
        public DockerImageComparer() : base(equalsExpression: (l, r) => l == r,
                                            hashCodeExpression: v => v.GetHashCode(),
                                            snapshotExpression: v => DockerImage.Parse(v.ToString()))
        { }
    }
}
