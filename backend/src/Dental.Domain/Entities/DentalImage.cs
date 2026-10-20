using Dental.Domain.Enums;
namespace Dental.Domain.Entities;

public sealed class DentalImage
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int? VisitId { get; set; }
    public int? ToothNumber { get; set; }
    public DentalImageCategory Category { get; set; } = DentalImageCategory.Intraoral;
    public string? Notes { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? DoctorDiagnosis { get; set; }
    public int? ClinicalUpdatedByUserId { get; set; }
    public DateTime? ClinicalUpdatedAt { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string StoragePath { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
}
