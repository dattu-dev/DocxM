namespace BusinessObjects.Entities;

public partial class Document
{
    // Ownership này dùng để Instructor chỉ truy cập document do mình upload.
    public int? UploadedByUserId { get; set; }

    public virtual AppUser? UploadedByUser { get; set; }
}
