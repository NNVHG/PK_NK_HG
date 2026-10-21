namespace Dental.Domain.Entities;

public sealed class DentalImageClinicalAddendum
{
    public int Id { get; set; }
    public int DentalImageId { get; set; }
    public int CreatedByUserId { get; set; }
    public string AuthorName { get; set; } = "";
    public string AuthorRole { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public Guid RequestId { get; set; }
    public int ImageVersion { get; set; }
}
