using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

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
            b.Property(r => r.Updates).HasJsonConversion();
            HasJsonConversion(b.Property(r => r.Registries));

            b.HasIndex(r => r.Created).IsDescending(); // faster filtering
            b.HasIndex(r => r.ProviderId).IsUnique();
        });

        modelBuilder.Entity<UpdateJob>(b =>
        {
            b.Property(j => j.PackageEcosystem).IsRequired();

            b.HasIndex(j => j.Created).IsDescending(); // faster filtering
            b.HasIndex(j => j.RepositoryId);
            b.HasIndex(j => new { j.PackageEcosystem, j.Directory, }); // faster filtering
            b.HasIndex(j => new { j.PackageEcosystem, j.Directory, j.EventBusId, }).IsUnique();
            b.HasIndex(j => j.AuthKey).IsUnique();

            b.OwnsOne(j => j.Resources);
        });
    }

    static PropertyBuilder<T> HasJsonConversion<T>(PropertyBuilder<T> propertyBuilder, JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(propertyBuilder);

#pragma warning disable CS8603 // Possible null reference return.
        var converter = new ValueConverter<T, string?>(
            convertToProviderExpression: v => ConvertToJson(v, serializerOptions),
            convertFromProviderExpression: v => ConvertFromJson<T>(v, serializerOptions));

        var comparer = new ValueComparer<T>(
            equalsExpression: (l, r) => ConvertToJson(l, serializerOptions) == ConvertToJson(r, serializerOptions),
            hashCodeExpression: v => v == null ? 0 : ConvertToJson(v, serializerOptions).GetHashCode(),
            snapshotExpression: v => ConvertFromJson<T>(ConvertToJson(v, serializerOptions), serializerOptions));
#pragma warning restore CS8603 // Possible null reference return.

        propertyBuilder.HasConversion(converter);
        propertyBuilder.Metadata.SetValueConverter(converter);
        propertyBuilder.Metadata.SetValueComparer(comparer);

        return propertyBuilder;
    }

    private static string ConvertToJson<T>(T value, JsonSerializerOptions? serializerOptions) => JsonSerializer.Serialize(value, serializerOptions);
    private static T? ConvertFromJson<T>(string? value, JsonSerializerOptions? serializerOptions) => value is null ? default : JsonSerializer.Deserialize<T>(value, serializerOptions);
}
