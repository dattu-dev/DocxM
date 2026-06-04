namespace BusinessObjects.Entities;

public partial class Subject
{
    // Ownership này dùng để Instructor chỉ quản lý Subject do mình tạo.
    public int? CreatedByUserId { get; set; }

    public virtual AppUser? CreatedByUser { get; set; }
}
