using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class DocumentChunk
{
    public long DocumentChunkId { get; set; }

    public int DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Content { get; set; } = null!;

    public int? TokenCount { get; set; }

    public int? PageNumber { get; set; }

    public string? SectionTitle { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Document Document { get; set; } = null!;

    public virtual ICollection<DocumentEmbedding> DocumentEmbeddings { get; set; } = new List<DocumentEmbedding>();
}
