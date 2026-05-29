using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Subject
{
    public int SubjectId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}
