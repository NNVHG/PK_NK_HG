using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Interfaces;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Repositories;
public sealed class ToothHistoryRepository(DentalDbContext db) : IToothHistoryRepository
{
    public async Task<PagedResult<ToothHistoryItemResponse>> GetAsync(ToothHistoryRequest request, CancellationToken ct = default)
    {
        var conditions = db.ToothConditions.AsNoTracking().Where(c => c.Visit.PatientId == request.PatientId &&
            (request.ToothNumber == null || c.ToothNumber == request.ToothNumber)).Select(c => new {
            c.Id, EventType = "Condition", c.VisitId, VisitDate = c.Visit.StartedAt ?? c.Visit.CreatedAt, c.CreatedAt,
            DentistName = c.Visit.Dentist == null ? null : c.Visit.Dentist.FullName, ToothNumber = (int?)c.ToothNumber,
            ConditionCode = (string?)c.ConditionCode, c.Surface, c.Note,
            Code = (string?)null, Name = (string?)null, Quantity = 0, UnitPrice = 0m });
        var services = db.VisitServices.AsNoTracking().Where(s => s.Visit.PatientId == request.PatientId &&
            (request.ToothNumber == null || s.ToothNumber == request.ToothNumber)).Select(s => new {
            s.Id, EventType = "ServiceAssignment", s.VisitId, VisitDate = s.Visit.StartedAt ?? s.Visit.CreatedAt, s.CreatedAt,
            DentistName = s.Visit.Dentist == null ? null : s.Visit.Dentist.FullName, s.ToothNumber,
            ConditionCode = (string?)null, s.Surface, Note = (string?)null,
            Code = (string?)s.ServiceCode, Name = (string?)s.ServiceName, s.Quantity, s.UnitPrice });
        // UNION ALL retains every append-only condition and service-only visits without duplicating assignments.
        var query = conditions.Concat(services);
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.VisitDate)
            .ThenBy(e => e.EventType).ThenByDescending(e => e.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        var items = page.Select(e => new ToothHistoryItemResponse(e.Id, e.EventType, e.VisitId, e.VisitDate, e.CreatedAt,
            e.DentistName, e.ToothNumber, e.ConditionCode, e.Surface, e.Note,
            e.EventType == "ServiceAssignment" ? [new(e.Id, e.Code!, e.Name!, e.Quantity, e.UnitPrice, e.Surface)] : [])).ToArray();
        return PagedResult<ToothHistoryItemResponse>.Create(items, total, request.Page, request.PageSize);
    }
}
