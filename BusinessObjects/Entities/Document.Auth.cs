namespace BusinessObjects.Entities;

public partial class Document
{
    public int? UploadedByUserId { get; set; }

    public virtual AppUser? UploadedByUser { get; set; }
}
