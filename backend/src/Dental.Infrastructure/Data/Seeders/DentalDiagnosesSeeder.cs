using Dental.Domain.Entities;
using Dental.Application.Features.MOD_MST.Services;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Data.Seeders;
// Initial sample master catalog DL167/Q264; internal codes, not an ICD classification or treatment guide.
public static class DentalDiagnosesSeeder
{
    public static async Task SeedAsync(DentalDbContext db, CancellationToken ct = default)
    {
        if (await db.DentalDiagnoses.AnyAsync(ct)) return;
        db.DentalDiagnoses.AddRange(new DentalDiagnosis[] {
            new DentalDiagnosis { Code = "NK001", Name = "Sâu men răng", Category = "Sâu răng", SearchText = DiagnosisSearch.Normalize("NK001 Sâu men răng") },
            new DentalDiagnosis { Code = "NK002", Name = "Sâu ngà răng", Category = "Sâu răng", SearchText = DiagnosisSearch.Normalize("NK002 Sâu ngà răng") },
            new DentalDiagnosis { Code = "NK003", Name = "Sâu chân răng", Category = "Sâu răng", SearchText = DiagnosisSearch.Normalize("NK003 Sâu chân răng") },
            new DentalDiagnosis { Code = "NK004", Name = "Sâu răng tái phát", Category = "Sâu răng", SearchText = DiagnosisSearch.Normalize("NK004 Sâu răng tái phát") },
            new DentalDiagnosis { Code = "NK005", Name = "Viêm tủy hồi phục", Category = "Bệnh lý tủy", SearchText = DiagnosisSearch.Normalize("NK005 Viêm tủy hồi phục") },
            new DentalDiagnosis { Code = "NK006", Name = "Viêm tủy không hồi phục", Category = "Bệnh lý tủy", SearchText = DiagnosisSearch.Normalize("NK006 Viêm tủy không hồi phục") },
            new DentalDiagnosis { Code = "NK007", Name = "Hoại tử tủy", Category = "Bệnh lý tủy", SearchText = DiagnosisSearch.Normalize("NK007 Hoại tử tủy") },
            new DentalDiagnosis { Code = "NK008", Name = "Viêm quanh cuống cấp", Category = "Bệnh lý tủy", SearchText = DiagnosisSearch.Normalize("NK008 Viêm quanh cuống cấp") },
            new DentalDiagnosis { Code = "NK009", Name = "Viêm quanh cuống mạn", Category = "Bệnh lý tủy", SearchText = DiagnosisSearch.Normalize("NK009 Viêm quanh cuống mạn") },
            new DentalDiagnosis { Code = "NK010", Name = "Áp xe quanh cuống", Category = "Bệnh lý tủy", SearchText = DiagnosisSearch.Normalize("NK010 Áp xe quanh cuống") },
            new DentalDiagnosis { Code = "NK011", Name = "Viêm lợi mạn tính", Category = "Nha chu", SearchText = DiagnosisSearch.Normalize("NK011 Viêm lợi mạn tính") },
            new DentalDiagnosis { Code = "NK012", Name = "Viêm lợi cấp", Category = "Nha chu", SearchText = DiagnosisSearch.Normalize("NK012 Viêm lợi cấp") },
            new DentalDiagnosis { Code = "NK013", Name = "Viêm nha chu", Category = "Nha chu", SearchText = DiagnosisSearch.Normalize("NK013 Viêm nha chu") },
            new DentalDiagnosis { Code = "NK014", Name = "Nha chu tiến triển", Category = "Nha chu", SearchText = DiagnosisSearch.Normalize("NK014 Nha chu tiến triển") },
            new DentalDiagnosis { Code = "NK015", Name = "Tụt lợi", Category = "Nha chu", SearchText = DiagnosisSearch.Normalize("NK015 Tụt lợi") },
            new DentalDiagnosis { Code = "NK016", Name = "Cao răng", Category = "Nha chu", SearchText = DiagnosisSearch.Normalize("NK016 Cao răng") },
            new DentalDiagnosis { Code = "NK017", Name = "Răng khôn mọc lệch", Category = "Răng khôn", SearchText = DiagnosisSearch.Normalize("NK017 Răng khôn mọc lệch") },
            new DentalDiagnosis { Code = "NK018", Name = "Răng khôn mọc ngầm", Category = "Răng khôn", SearchText = DiagnosisSearch.Normalize("NK018 Răng khôn mọc ngầm") },
            new DentalDiagnosis { Code = "NK019", Name = "Viêm quanh thân răng khôn", Category = "Răng khôn", SearchText = DiagnosisSearch.Normalize("NK019 Viêm quanh thân răng khôn") },
            new DentalDiagnosis { Code = "NK020", Name = "Nứt thân răng", Category = "Chấn thương", SearchText = DiagnosisSearch.Normalize("NK020 Nứt thân răng") },
            new DentalDiagnosis { Code = "NK021", Name = "Vỡ thân răng", Category = "Chấn thương", SearchText = DiagnosisSearch.Normalize("NK021 Vỡ thân răng") },
            new DentalDiagnosis { Code = "NK022", Name = "Gãy chân răng", Category = "Chấn thương", SearchText = DiagnosisSearch.Normalize("NK022 Gãy chân răng") },
            new DentalDiagnosis { Code = "NK023", Name = "Răng lung lay do chấn thương", Category = "Chấn thương", SearchText = DiagnosisSearch.Normalize("NK023 Răng lung lay do chấn thương") },
            new DentalDiagnosis { Code = "NK024", Name = "Mòn cổ răng", Category = "Khác", SearchText = DiagnosisSearch.Normalize("NK024 Mòn cổ răng") },
            new DentalDiagnosis { Code = "NK025", Name = "Khuyết hình chêm", Category = "Khác", SearchText = DiagnosisSearch.Normalize("NK025 Khuyết hình chêm") },
            new DentalDiagnosis { Code = "NK026", Name = "Mòn mặt nhai", Category = "Khác", SearchText = DiagnosisSearch.Normalize("NK026 Mòn mặt nhai") },
            new DentalDiagnosis { Code = "NK027", Name = "Ê buốt ngà răng", Category = "Khác", SearchText = DiagnosisSearch.Normalize("NK027 Ê buốt ngà răng") },
            new DentalDiagnosis { Code = "NK028", Name = "Mất răng", Category = "Khác", SearchText = DiagnosisSearch.Normalize("NK028 Mất răng") },
            new DentalDiagnosis { Code = "NK029", Name = "Răng ngầm", Category = "Khác", SearchText = DiagnosisSearch.Normalize("NK029 Răng ngầm") },
            new DentalDiagnosis { Code = "NK030", Name = "Rối loạn mọc răng", Category = "Khác", SearchText = DiagnosisSearch.Normalize("NK030 Rối loạn mọc răng") }
        });
        await db.SaveChangesAsync(ct);
    }
}
