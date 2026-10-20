namespace Dental.Application.Features.MOD_RX.DTOs;
public sealed record PrescriptionDispenseRequest(int ExpectedVersion, bool Confirmed,
    bool ReceivedByProxy = false, string? ReceiverName = null, string? ReceiverPhone = null,
    bool OverrideExpired = false, string? OverrideReason = null);
public sealed record PrescriptionUndoDispenseRequest(int ExpectedVersion, bool Confirmed, string Reason);
public sealed record PrescriptionHandoverStatus(int PrescriptionId, int VisitId, int Version, string PrescriptionCode,
    string PatientName, bool ReceiverRequired, DateTime ExpiresAt, DateTime? DispensedAt, string? DispensedByName,
    bool ReceivedByProxy, string? ReceiverName, string? ReceiverPhone,
    bool CanDispense, bool CanUndo, bool CanOverrideExpired, string? BlockReason, PrescriptionStockStatus? Stock = null);
