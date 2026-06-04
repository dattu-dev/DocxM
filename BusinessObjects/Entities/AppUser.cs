namespace BusinessObjects.Entities;

public sealed class AppUser
{
    public int UserId { get; set; }

    public string UserName { get; set; } = null!;

    public string NormalizedUserName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string NormalizedEmail { get; set; } = null!;

    public string FullName { get; set; } = null!;

    // Role quyết định nhánh phân quyền Instructor/Student trong controller và repository.
    public string Role { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();

    public ICollection<Document> Documents { get; set; } = new List<Document>();

    public ICollection<ChatConversation> ChatConversations { get; set; } = new List<ChatConversation>();

    public ICollection<SubjectPermission> SubjectPermissions { get; set; } = new List<SubjectPermission>();

    public ICollection<SubjectPermission> GrantedSubjectPermissions { get; set; } = new List<SubjectPermission>();
}
