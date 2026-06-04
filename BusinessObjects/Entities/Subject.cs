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

    // Danh sách quyền giúp Student truy cập Subject mà không sở hữu Subject.
    public virtual ICollection<SubjectPermission> SubjectPermissions { get; set; } = new List<SubjectPermission>();
}
