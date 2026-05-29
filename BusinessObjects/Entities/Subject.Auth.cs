namespace BusinessObjects.Entities;

public partial class Subject
{
    public int? CreatedByUserId { get; set; }

    public virtual AppUser? CreatedByUser { get; set; }
}
