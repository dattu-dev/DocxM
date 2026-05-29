using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Chapter
{
    public int ChapterId { get; set; }

    public int SubjectId { get; set; }

    public string Title { get; set; } = null!;

    public int ChapterNumber { get; set; }

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual Subject Subject { get; set; } = null!;
}
