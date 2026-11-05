using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore; 
using Microsoft.EntityFrameworkCore;

namespace Heimevernet_Prosjekt.DataAccess;

// Database tables for resources and login (Identity) - Teacher: HeimevernetDbContext.cs
public class HeimevernetDbContext(DbContextOptions<HeimevernetDbContext> options)
        : IdentityDbContext<ApplicationUser, IdentityRole, string>(options) 
{
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<ResourceNeed> ResourceNeeds => Set<ResourceNeed>();
    public DbSet<ResourceType> ResourceTypes => Set<ResourceType>();
    public DbSet<ResourceReport> ResourceReports => Set<ResourceReport>();
    public DbSet<ResourceNeedItem> ResourceNeedItems => Set<ResourceNeedItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Deleted resources are hidden everywhere, but kept in the database - Docs: Global Query Filters
        // https://learn.microsoft.com/ef/core/querying/filters
        modelBuilder.Entity<Resource>().HasQueryFilter(r => !r.IsDeleted);

        modelBuilder.Entity<Resource>(entity =>
        {
            entity.ToTable("Resources");
            entity.HasKey(resource => resource.Id);
            entity.Property(resource => resource.Name)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(resource => resource.Description)
                .HasMaxLength(2000)
                .IsRequired();
            entity.Property(resource => resource.Type)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(resource => resource.Address)
                .HasMaxLength(200)
                .IsRequired(false);
            entity.Property(resource => resource.City)
                .HasMaxLength(100)
                .IsRequired(false);
            entity.Property(resource => resource.ZipCode)
                .HasMaxLength(6)
                .IsRequired(false);
            entity.Property(resource => resource.OwnerTelephoneNumber)
                .HasMaxLength(20)
                .IsRequired(false);
        });

        modelBuilder.Entity<ResourceNeed>().HasQueryFilter(r => !r.IsDeleted);

        modelBuilder.Entity<ResourceNeed>(entity =>
        {
            entity.ToTable("ResourceNeeds");
            entity.HasKey(resourceneed => resourceneed.Id);
            entity.Property(resourceneed => resourceneed.Name)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(resourceneed => resourceneed.Description)
                .HasMaxLength(2000)
                .IsRequired();
            entity.Property(resourceneed => resourceneed.Type)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(resourceneed => resourceneed.Address)
                .HasMaxLength(200)
                .IsRequired(false);
            entity.Property(resourceneed => resourceneed.City)
                .HasMaxLength(100)
                .IsRequired(false);
            entity.Property(resourceneed => resourceneed.ZipCode)
                .HasMaxLength(6)
                .IsRequired(false);

            // Existing needs get 2 km in the migration; new needs get what the operator picks in the form
            entity.Property(resourceneed => resourceneed.RadiusKm)
                .HasDefaultValue(2.0);

            // One need has many lines; deleting a need deletes its lines - Docs: Relationships (one-to-many)
            // https://learn.microsoft.com/ef/core/modeling/relationships/one-to-many
            entity.HasMany(resourceneed => resourceneed.Items)
                .WithOne(item => item.ResourceNeed)
                .HasForeignKey(item => item.ResourceNeedId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Lines of a soft-deleted need are hidden too, otherwise EF warns about the query filter
        modelBuilder.Entity<ResourceNeedItem>().HasQueryFilter(item => !item.ResourceNeed.IsDeleted);

        modelBuilder.Entity<ResourceNeedItem>(entity =>
        {
            entity.ToTable("ResourceNeedItems");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Type)
                .HasMaxLength(100)
                .IsRequired();
        });
    }
}

