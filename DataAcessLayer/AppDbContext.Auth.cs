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
            entity.Property(chunk => chunk.SectionTitle).HasMaxLength(250);
            entity.Property(chunk => chunk.VectorId).HasMaxLength(100);
        });

        modelBuilder.Entity<ChatConversation>(entity =>
        {
            entity.HasKey(conversation => conversation.ChatConversationId);
            entity.HasIndex(conversation => conversation.UserId, "IX_ChatConversations_UserId");
            entity.HasIndex(
                conversation => new
                {
                    conversation.UserId,
                    conversation.SubjectId,
                    conversation.ChapterId,
                    conversation.DocumentId
                },
                "IX_ChatConversations_Scope");

            entity.Property(conversation => conversation.Title).HasMaxLength(250);
            entity.Property(conversation => conversation.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(conversation => conversation.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(conversation => conversation.User)
                .WithMany(user => user.ChatConversations)
                .HasForeignKey(conversation => conversation.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ChatConversations_AppUsers");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(message => message.ChatMessageId);
            entity.HasIndex(message => message.ChatConversationId, "IX_ChatMessages_ConversationId");
            entity.Property(message => message.UserQuestion).HasMaxLength(1000);
            entity.Property(message => message.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(message => message.ChatConversation)
                .WithMany(conversation => conversation.ChatMessages)
                .HasForeignKey(message => message.ChatConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ChatMessages_ChatConversations");
        });

        modelBuilder.Entity<ChatCitation>(entity =>
        {
            entity.HasKey(citation => citation.ChatCitationId);
            entity.HasIndex(citation => citation.ChatMessageId, "IX_ChatCitations_MessageId");
            entity.HasIndex(citation => citation.DocumentId, "IX_ChatCitations_DocumentId");
            entity.Property(citation => citation.DocumentName).HasMaxLength(260);
            entity.Property(citation => citation.Snippet).HasMaxLength(1000);

            entity.HasOne(citation => citation.ChatMessage)
                .WithMany(message => message.ChatCitations)
                .HasForeignKey(citation => citation.ChatMessageId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ChatCitations_ChatMessages");
        });
    }
}
