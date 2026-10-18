namespace Dental.Application.Features.MOD_RX.DTOs;
public sealed record PrescriptionPrintRequest(int ExpectedVersion);
public sealed record PrescriptionPrintLine(string MedicineName, string? Strength, string Unit, int Quantity, string UsageInstructions, int? TreatmentDays);
public sealed record PrescriptionPrintDocument(int PrescriptionId, int VisitId, int Version, string PrescriptionCode,
    string ClinicName, string ClinicAddress, string ClinicPhone, string PatientName, DateOnly DateOfBirth, string? Gender, string? PatientAddress, string PatientPhone,
    string Diagnosis, IReadOnlyList<PrescriptionPrintLine> Items, string? DoctorAdvice, DateOnly? RevisitDate, DateOnly PrescribedDate,
    string DoctorName, string DoctorTitle, int SignatureVersion, string SignaturePngBase64, string RelativeLookupPath, string? ReceiverName, string? ReceiverPhone);
