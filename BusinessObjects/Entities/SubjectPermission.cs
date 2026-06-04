namespace BusinessObjects.Entities;

public sealed class SubjectPermission
{
    public int SubjectPermissionId { get; set; }

    // Bản ghi này nối Student với Subject được phép xem/chat.
    public int SubjectId { get; set; }

    public int StudentUserId { get; set; }

    public int GrantedByUserId { get; set; }

    public DateTime GrantedAt { get; set; }

    public Subject Subject { get; set; } = null!;

    public AppUser StudentUser { get; set; } = null!;

    public AppUser GrantedByUser { get; set; } = null!;
}
