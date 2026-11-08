using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ClassEntity> Classes => Set<ClassEntity>();
    public DbSet<AdmissionHold> AdmissionHolds => Set<AdmissionHold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Classes table
        modelBuilder.Entity<ClassEntity>(entity =>
        {
            entity.ToTable("Classes");

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(c => c.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(c => c.TotalSeats)
                .IsRequired();

            // Total seats must be positive
            entity.ToTable(t => t.HasCheckConstraint("CK_Classes_TotalSeats_Positive", "\"TotalSeats\" > 0"));
        });

        // Configure AdmissionHolds table
        modelBuilder.Entity<AdmissionHold>(entity =>
        {
            entity.ToTable("AdmissionHolds");

            entity.HasKey(h => h.Id);

            entity.Property(h => h.Id)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(h => h.ClassId)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(h => h.StudentId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(h => h.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(h => h.CreatedAt)
                .IsRequired();

            entity.Property(h => h.ExpiresAt)
                .IsRequired();

            // Relationship: Each hold belongs to a class
            entity.HasOne(h => h.Class)
                .WithMany(c => c.Holds)
                .HasForeignKey(h => h.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes for query performance
            entity.HasIndex(h => new { h.ClassId, h.Status })
                .HasDatabaseName("IX_AdmissionHolds_ClassId_Status");

            entity.HasIndex(h => h.StudentId)
                .HasDatabaseName("IX_AdmissionHolds_StudentId");

            entity.HasIndex(h => new { h.Status, h.ExpiresAt })
                .HasDatabaseName("IX_AdmissionHolds_Status_ExpiresAt");

            // Enforce Rule 7 at DB level:
            // A student cannot have multiple active (HELD) or confirmed admissions in the same class.
            entity.HasIndex(h => new { h.ClassId, h.StudentId })
                .HasFilter("\"Status\" IN ('HELD', 'CONFIRMED')")
                .IsUnique()
                .HasDatabaseName("IX_AdmissionHolds_ClassId_StudentId_ActiveUnique");
        });
    }
}
