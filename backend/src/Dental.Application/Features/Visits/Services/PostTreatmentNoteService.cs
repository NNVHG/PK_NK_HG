using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Visits.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Features.Visits.Services;

public sealed class PostTreatmentNoteService
{
    private readonly IVisitRepository _visits;
    private readonly IPatientRepository _patients;
    private readonly IInvoiceRepository _invoices;
    private readonly IPostTreatmentNoteRepository _notes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _audit;

    public PostTreatmentNoteService(
        IVisitRepository visits,
        IPatientRepository patients,
        IInvoiceRepository invoices,
        IPostTreatmentNoteRepository notes,
        IUnitOfWork unitOfWork,
        IAuditLogger audit)
    {
        _visits = visits;
        _patients = patients;
        _invoices = invoices;
        _notes = notes;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<PostTreatmentNoteResponse>>> GetAsync(
        int visitId, int userId, string? role, CancellationToken ct = default)
    {
        if (role != RoleCodes.Patient && !RoleCodes.StaffRoles.Contains(role))
            return Result<IReadOnlyList<PostTreatmentNoteResponse>>.Failure(Error.Forbidden);

        var visit = await _visits.GetByIdAsync(visitId, ct);
        if (visit is null)
            return Result<IReadOnlyList<PostTreatmentNoteResponse>>.Failure(Error.NotFound);

        if (role == RoleCodes.Patient)
        {
            var patient = await _patients.GetByIdAsync(visit.PatientId, ct);
            if (patient is null || patient.UserId != userId)
                return Result<IReadOnlyList<PostTreatmentNoteResponse>>.Failure(Error.NotFound);
        }

        var notes = await _notes.GetByVisitIdAsync(visitId, ct);
        return Result<IReadOnlyList<PostTreatmentNoteResponse>>.Success(notes.Select(ToResponse).ToList());
    }

    public async Task<Result<PostTreatmentNoteResponse>> AddAsync(
        int visitId, CreatePostTreatmentNoteRequest request, int userId, string? role, string? ip, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist))
            return Result<PostTreatmentNoteResponse>.Failure(Error.Forbidden);

        if (visitId <= 0 || userId <= 0 || string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 1000 || request.RequestId == Guid.Empty)
            return Result<PostTreatmentNoteResponse>.Failure(Error.Validation);
        try
        {
            return await _unitOfWork.ExecuteSerializableTransactionAsync(
                tct => AddCoreAsync(visitId, request, userId, role, ip, tct), ct);
        }
        catch (ConcurrentOperationException)
        {
            return Result<PostTreatmentNoteResponse>.Failure("PAT_083", "Thao tác đồng thời; vui lòng thử lại.");
        }
    }

    private async Task<Result<PostTreatmentNoteResponse>> AddCoreAsync(
        int visitId, CreatePostTreatmentNoteRequest request, int userId, string? role, string? ip, CancellationToken ct)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist))
            return Result<PostTreatmentNoteResponse>.Failure(Error.Forbidden);

        var visit = await _visits.GetForUpdateAsync(visitId, ct);
        if (visit is null)
            return Result<PostTreatmentNoteResponse>.Failure(Error.NotFound);

        if (role == RoleCodes.Dentist && visit.DentistId != userId)
            return Result<PostTreatmentNoteResponse>.Failure(Error.DiagnosisDentistMismatch);

        if (visit.Status != VisitStatuses.Completed || !visit.IsLocked)
            return Result<PostTreatmentNoteResponse>.Failure(
                new Error("PAT_081", "Chỉ được bổ sung ghi chú hậu điều trị cho ca khám đã hoàn tất và đã khóa."));

        var invoice = await _invoices.GetForVisitAsync(visitId, ct);
        if (invoice is null || invoice.Status != InvoiceStatus.Paid)
            return Result<PostTreatmentNoteResponse>.Failure(
                new Error("PAT_082", "Chỉ được bổ sung ghi chú hậu điều trị khi hóa đơn viện phí đã thanh toán hoàn tất."));

        if (request.RequestId.HasValue)
        {
            var existing = await _notes.GetByRequestIdAsync(request.RequestId.Value, ct);
            if (existing is not null)
            {
                // A replay must never disclose another visit/author's clinical text, even to an Admin caller.
                if (existing.VisitId != visitId || existing.CreatedByUserId != userId)
                    return Result<PostTreatmentNoteResponse>.Failure(Error.NotFound);
                if (existing.Note != request.Note.Trim())
                    return Result<PostTreatmentNoteResponse>.Failure("PAT_083", "Mã yêu cầu đã dùng cho nội dung khác; tải lại ghi chú để đối chiếu.");
                return Result<PostTreatmentNoteResponse>.Success(ToResponse(existing));
            }
        }

        var note = new PostTreatmentNote
        {
            VisitId = visitId,
            CreatedByUserId = userId,
            Note = request.Note.Trim(),
            CreatedAt = DateTime.UtcNow,
            RequestId = request.RequestId,
        };

        await _notes.AddAsync(note, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _audit.LogRequiredAsync(
            action: AuditActions.PostTreatmentNoteCreated,
            entityType: "Visit",
            entityId: visit.VisitId,
            userId: userId,
            detail: JsonSerializer.Serialize(new
            {
                visitId = visit.VisitId,
                postTreatmentNoteId = note.Id,
                action = "create_post_treatment_note",
            }),
            ipAddress: ip,
            ct: ct);

        var saved = await _notes.GetByIdAsync(note.Id, ct) ?? note;
        return Result<PostTreatmentNoteResponse>.Success(ToResponse(saved));
    }

    private static PostTreatmentNoteResponse ToResponse(PostTreatmentNote note)
        => new(
            note.Id,
            note.VisitId,
            note.CreatedByUserId,
            note.CreatedByUser?.FullName ?? string.Empty,
            note.CreatedByUser?.Role?.RoleName ?? string.Empty,
            note.Note,
            note.CreatedAt,
            note.RequestId);
}
