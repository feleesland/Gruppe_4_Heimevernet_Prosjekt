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
    public DbSet<ResourceRequest> ResourceRequests => Set<ResourceRequest>();
    public DbSet<ResourceNeedLog> ResourceNeedLogs => Set<ResourceNeedLog>();
    public DbSet<ResourceCategory> ResourceCategories => Set<ResourceCategory>();
    public DbSet<ResourceAttribute> ResourceAttributes => Set<ResourceAttribute>();
    public DbSet<ResourceAttributeValue> ResourceAttributeValues => Set<ResourceAttributeValue>();
    public DbSet<PermanentMapMarker> PermanentMapMarkers => Set<PermanentMapMarker>();

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

        // Requests of a soft-deleted need are hidden too (same as the lines)
        modelBuilder.Entity<ResourceRequest>().HasQueryFilter(request => !request.ResourceNeedItem.ResourceNeed.IsDeleted);

        modelBuilder.Entity<ResourceRequest>(entity =>
        {
            entity.ToTable("ResourceRequests");
            entity.HasKey(request => request.Id);

            // One line has many requests - Docs: Relationships (one-to-many)
            // https://learn.microsoft.com/ef/core/modeling/relationships/one-to-many
            entity.HasOne(request => request.ResourceNeedItem)
                .WithMany(item => item.Requests)
                .HasForeignKey(request => request.ResourceNeedItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // Quick lookup of "is this resource already asked for / in use?"
            entity.HasIndex(request => request.ResourceId);

            entity.Property(request => request.Comment).HasMaxLength(500);
        });
        // The log is never deleted, not even when the need is deleted - it is history
        modelBuilder.Entity<ResourceNeedLog>(entity =>
        {
            entity.ToTable("ResourceNeedLogs");
            entity.HasIndex(log => log.ResourceNeedId);
        });

        // "Ny ressurs" step by step: categories -> types -> questions -> answers (help with AI)
        // Docs: Relationships (one-to-many) - https://learn.microsoft.com/ef/core/modeling/relationships/one-to-many
        modelBuilder.Entity<ResourceCategory>(entity =>
        {
            entity.ToTable("ResourceCategories");
            entity.Property(category => category.Name).HasMaxLength(100).IsRequired();
            entity.Property(category => category.Icon).HasMaxLength(50);
            entity.HasMany(category => category.Types)
                .WithOne(type => type.Category)
                .HasForeignKey(type => type.CategoryId)
                .OnDelete(DeleteBehavior.SetNull); // deleting a category keeps its types
        });

        modelBuilder.Entity<ResourceAttribute>(entity =>
        {
            entity.ToTable("ResourceAttributes");
            entity.Property(attribute => attribute.Label).HasMaxLength(100).IsRequired();
            entity.Property(attribute => attribute.Options).HasMaxLength(500);
            entity.Property(attribute => attribute.Unit).HasMaxLength(20);
            entity.Property(attribute => attribute.Kind).HasConversion<string>().HasMaxLength(20); // "YesNo" in the database, easy to read
            entity.HasOne(attribute => attribute.ResourceType)
                .WithMany(type => type.Attributes)
                .HasForeignKey(attribute => attribute.ResourceTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Answers of a soft-deleted resource are hidden too, otherwise EF warns about the query filter
        modelBuilder.Entity<ResourceAttributeValue>().HasQueryFilter(value => !value.Resource.IsDeleted);

        modelBuilder.Entity<ResourceAttributeValue>(entity =>
        {
            entity.ToTable("ResourceAttributeValues");
            entity.Property(value => value.Value).HasMaxLength(200).IsRequired();
            // One answer per question per resource
            entity.HasIndex(value => new { value.ResourceId, value.ResourceAttributeId }).IsUnique();
            entity.HasOne(value => value.Resource)
                .WithMany()
                .HasForeignKey(value => value.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.ResourceAttribute)
                .WithMany()
                .HasForeignKey(value => value.ResourceAttributeId)
                .OnDelete(DeleteBehavior.Restrict); // questions are hidden, not deleted, so answers are kept
        });


        modelBuilder.Entity<PermanentMapMarker>(entity =>
        {
            entity.ToTable("PermanentMapMarkers");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Type)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.IconType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(1000);
        });

        modelBuilder.Entity<PermanentMapMarker>().HasData(

            new PermanentMapMarker
            {
                Id = 1,
                Name = "Kristiansand Politistasjon",
                Type = "Politi",
                IconType = "police",
                Description = "Politistasjon",
                Latitude = 58.146,
                Longitude = 7.996
            },

            new PermanentMapMarker
            {
                Id = 2,
                Name = "Kristiansand Brannstasjon",
                Type = "Brann",
                IconType = "firestation",
                Description = "Brannstasjon",
                Latitude = 58.169,
                Longitude = 8.019
            },

            new PermanentMapMarker
            {
                Id = 3,
                Name = "Sørlandet Sykehus Kristiansand",
                Type = "Sykehus",
                IconType = "hospital",
                Description = "Akuttsykehus",
                Latitude = 58.156,
                Longitude = 7.978
            }
        );
    }
}

