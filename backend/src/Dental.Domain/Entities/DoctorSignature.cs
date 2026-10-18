namespace Dental.Domain.Entities;
public sealed class DoctorSignature
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public int Version { get; set; }
    public byte[] ImagePng { get; set; } = [];
    public int UpdatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
