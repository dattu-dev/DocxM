using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAcessLayer;

public partial class AppDbContext
{
    public virtual DbSet<AppUser> AppUsers { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(user => user.UserId);
            entity.HasIndex(user => user.NormalizedUserName, "UX_AppUsers_NormalizedUserName").IsUnique();
            entity.HasIndex(user => user.NormalizedEmail, "UX_AppUsers_NormalizedEmail").IsUnique();

            entity.Property(user => user.UserName).HasMaxLength(100);
            entity.Property(user => user.NormalizedUserName).HasMaxLength(100);
            entity.Property(user => user.Email).HasMaxLength(256);
            entity.Property(user => user.NormalizedEmail).HasMaxLength(256);
            entity.Property(user => user.FullName).HasMaxLength(150);
            entity.Property(user => user.PasswordHash).HasMaxLength(500);
            entity.Property(user => user.IsActive).HasDefaultValue(true);
            entity.Property(user => user.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasIndex(document => document.UploadedByUserId, "IX_Documents_UploadedByUserId");

            entity.HasOne(document => document.UploadedByUser)
                .WithMany(user => user.Documents)
                .HasForeignKey(document => document.UploadedByUserId)
                .HasConstraintName("FK_Documents_AppUsers");
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasIndex(subject => subject.CreatedByUserId, "IX_Subjects_CreatedByUserId");

            entity.HasOne(subject => subject.CreatedByUser)
                .WithMany(user => user.Subjects)
                .HasForeignKey(subject => subject.CreatedByUserId)
                .HasConstraintName("FK_Subjects_AppUsers");
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.Property(chunk => chunk.VectorId).HasMaxLength(100);
        });
    }
}
