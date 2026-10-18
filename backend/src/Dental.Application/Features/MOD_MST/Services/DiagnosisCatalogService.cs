using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
namespace Dental.Application.Features.MOD_MST.Services;
public sealed class DiagnosisCatalogService(IDiagnosisCatalogRepository diagnoses, IUnitOfWork uow, IAuditLogger audit)
{
    public async Task<Result<PagedResult<DiagnosisResponse>>> GetAsync(string? role, DiagnosisQuery request, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist)) return Result<PagedResult<DiagnosisResponse>>.Failure(Error.Forbidden);
        if (request.IncludeInactive && role != RoleCodes.Admin) return Result<PagedResult<DiagnosisResponse>>.Failure(Error.Forbidden);
        if (!(await new DiagnosisQueryValidator().ValidateAsync(request, ct)).IsValid) return Result<PagedResult<DiagnosisResponse>>.Failure(Error.Validation);
        var page = await diagnoses.GetPageAsync(request, ct);
        return Result<PagedResult<DiagnosisResponse>>.Success(PagedResult<DiagnosisResponse>.Create(page.Items.Select(ToResponse).ToList(), page.TotalCount, page.Page, page.PageSize));
    }
    public async Task<Result<DiagnosisResponse>> SaveAsync(int? id, int actorId, string? role, DiagnosisWriteRequest request, CancellationToken ct = default)
    {
        if (role != RoleCodes.Admin) return Result<DiagnosisResponse>.Failure(Error.Forbidden);
        request = request with { Code = request.Code?.Trim() ?? "", Name = request.Name?.Trim() ?? "", Category = request.Category?.Trim() ?? "" };
        if (actorId <= 0 || id <= 0 || !(await new DiagnosisWriteValidator().ValidateAsync(request, ct)).IsValid || id.HasValue && request.ExpectedVersion <= 0)
            return Result<DiagnosisResponse>.Failure(Error.Validation);
        try
        {
            return await uow.ExecuteSerializableTransactionAsync(async tct =>
            {
                var item = id.HasValue ? await diagnoses.FindAsync(id.Value, tct) : new DentalDiagnosis();
                if (item is null) return Result<DiagnosisResponse>.Failure(Error.NotFound);
                if (id.HasValue && item.Version != request.ExpectedVersion) return Result<DiagnosisResponse>.Failure("MST_072", "Danh mục vừa thay đổi; tải lại để đối chiếu.");
                var code = request.Code.Trim().ToUpperInvariant();
                if (await diagnoses.CodeExistsAsync(code, id, tct)) return Result<DiagnosisResponse>.Failure("MST_071", "Mã chẩn đoán đã tồn tại.");
                item.Code = code; item.Name = request.Name.Trim(); item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
                item.Category = request.Category.Trim(); item.IsActive = request.IsActive;
                item.SearchText = DiagnosisSearch.Normalize(item.Code + " " + item.Name);
                if (id.HasValue) item.Version++; else await diagnoses.AddAsync(item, tct);
                await uow.SaveChangesAsync(tct);
                await audit.LogRequiredAsync(id.HasValue ? "MOD_MST_DIAGNOSIS_UPDATED" : "MOD_MST_DIAGNOSIS_CREATED", "DentalDiagnosis", item.Id, actorId,
                    JsonSerializer.Serialize(new { item.Version, item.IsActive, fields = new[] { "Code", "Name", "Description", "Category", "IsActive" } }), ct: tct);
                return Result<DiagnosisResponse>.Success(ToResponse(item));
            }, ct);
        }
        catch (ConcurrentOperationException) { return Result<DiagnosisResponse>.Failure("MST_072", "Có thao tác đồng thời; tải lại danh mục trước khi thử lại."); }
    }
    private static DiagnosisResponse ToResponse(DentalDiagnosis x) => new(x.Id, x.Code, x.Name, x.Description, x.Category, x.IsActive, x.Version);
}
