using System;
using System.Collections.Generic;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAcessLayer;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Chapter> Chapters { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<DocumentChunk> DocumentChunks { get; set; }

    public virtual DbSet<DocumentEmbedding> DocumentEmbeddings { get; set; }

    public virtual DbSet<EmbeddingModel> EmbeddingModels { get; set; }

    public virtual DbSet<Subject> Subjects { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Phần này là mapping database-first; business rule chính nằm ở service/repository.
        modelBuilder.Entity<Chapter>(entity =>
        {
            entity.HasIndex(e => e.SubjectId, "IX_Chapters_SubjectId");

            entity.HasIndex(e => new { e.SubjectId, e.ChapterNumber }, "UX_Chapters_SubjectId_ChapterNumber").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Title).HasMaxLength(250);

            entity.HasOne(d => d.Subject).WithMany(p => p.Chapters)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Chapters_Subjects");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasIndex(e => e.ChapterId, "IX_Documents_ChapterId");

            entity.HasIndex(e => e.ProcessingStatus, "IX_Documents_ProcessingStatus");

            entity.HasIndex(e => e.SubjectId, "IX_Documents_SubjectId");

            entity.Property(e => e.ContentType).HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.FileExtension).HasMaxLength(20);
            entity.Property(e => e.OriginalFileName).HasMaxLength(260);
            entity.Property(e => e.ProcessingStatus)
                .HasMaxLength(50)
                .HasDefaultValue("Uploaded");
            entity.Property(e => e.StoragePath).HasMaxLength(500);
            entity.Property(e => e.StoredFileName).HasMaxLength(260);
            entity.Property(e => e.Title).HasMaxLength(250);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.UploadedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Chapter).WithMany(p => p.Documents)
                .HasForeignKey(d => d.ChapterId)
                .HasConstraintName("FK_Documents_Chapters");

            entity.HasOne(d => d.Subject).WithMany(p => p.Documents)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Documents_Subjects");
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasIndex(e => new { e.DocumentId, e.ChunkIndex }, "UX_DocumentChunks_DocumentId_ChunkIndex").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Document).WithMany(p => p.DocumentChunks)
                .HasForeignKey(d => d.DocumentId)
                .HasConstraintName("FK_DocumentChunks_Documents");
        });

        modelBuilder.Entity<DocumentEmbedding>(entity =>
        {
            entity.HasIndex(e => new { e.DocumentChunkId, e.EmbeddingModelId }, "UX_DocumentEmbeddings_Chunk_Model").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.DocumentChunk).WithMany(p => p.DocumentEmbeddings)
                .HasForeignKey(d => d.DocumentChunkId)
                .HasConstraintName("FK_DocumentEmbeddings_DocumentChunks");

            entity.HasOne(d => d.EmbeddingModel).WithMany(p => p.DocumentEmbeddings)
                .HasForeignKey(d => d.EmbeddingModelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DocumentEmbeddings_EmbeddingModels");
        });

        modelBuilder.Entity<EmbeddingModel>(entity =>
        {
            entity.HasIndex(e => new { e.Provider, e.Name }, "UX_EmbeddingModels_Provider_Name").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Provider).HasMaxLength(100);
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasIndex(e => e.Code, "UX_Subjects_Code").IsUnique();

            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
