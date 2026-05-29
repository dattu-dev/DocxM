using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class DocumentEmbedding
{
    public long DocumentEmbeddingId { get; set; }

    public long DocumentChunkId { get; set; }

    public int EmbeddingModelId { get; set; }

    public string? VectorJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual DocumentChunk DocumentChunk { get; set; } = null!;

    public virtual EmbeddingModel EmbeddingModel { get; set; } = null!;
}
