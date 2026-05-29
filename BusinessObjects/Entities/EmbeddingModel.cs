using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class EmbeddingModel
{
    public int EmbeddingModelId { get; set; }

    public string Provider { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int Dimension { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<DocumentEmbedding> DocumentEmbeddings { get; set; } = new List<DocumentEmbedding>();
}
