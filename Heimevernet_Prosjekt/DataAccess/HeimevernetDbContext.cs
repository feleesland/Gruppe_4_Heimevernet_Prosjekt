using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Identity.EntityFrameworkCore; er for login
using Microsoft.EntityFrameworkCore;

namespace Heimevernet_Prosjekt.DataAccess;

public class HeimevernetDbContext(DbContextOptions<HeimevernetDbContext> options)
       // : IdentityDbContext<IdentityUser, IdentityRole, string>(options) er for login 
       : DbContext(options) // midlertidig for å se at det virker uten login
{
    public DbSet<Resource> Resources => Set<Resource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
    }
}