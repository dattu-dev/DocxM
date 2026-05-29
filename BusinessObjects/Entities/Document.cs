using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Document
{
    public int DocumentId { get; set; }

    public int SubjectId { get; set; }

    public int? ChapterId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string OriginalFileName { get; set; } = null!;

    public string StoredFileName { get; set; } = null!;

    public string StoragePath { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public string FileExtension { get; set; } = null!;

    public long FileSizeBytes { get; set; }

    public string ProcessingStatus { get; set; } = null!;

    public int ChunkCount { get; set; }

    public DateTime UploadedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Chapter? Chapter { get; set; }

    public virtual ICollection<DocumentChunk> DocumentChunks { get; set; } = new List<DocumentChunk>();

    public virtual Subject Subject { get; set; } = null!;
}
