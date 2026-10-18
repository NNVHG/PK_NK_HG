using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Data.Seeders;

/// <summary>DL-277/279: initial names only; not a price list, product formulation or prescribing guide.</summary>
public static class MedicinesSeeder
{
    public static async Task SeedAsync(DentalDbContext db, CancellationToken ct = default)
    {
        if (await db.Medicines.AnyAsync(ct)) return;
        // No strengths or dosing instructions are invented. Admin must review actual products and prices before activation.
        (string Name, string Unit)[] names = [
            ("Amoxicillin", "Viên"), ("Amoxicillin / Acid clavulanic", "Viên"), ("Cefalexin", "Viên"),
            ("Cefuroxime", "Viên"), ("Cefixime", "Viên"), ("Azithromycin", "Viên"), ("Clarithromycin", "Viên"),
            ("Clindamycin", "Viên"), ("Metronidazole", "Viên"), ("Spiramycin", "Viên"),
            ("Paracetamol", "Viên"), ("Ibuprofen", "Viên"), ("Naproxen", "Viên"), ("Diclofenac", "Viên"),
            ("Meloxicam", "Viên"), ("Celecoxib", "Viên"), ("Prednisolone", "Viên"),
            ("Methylprednisolone", "Viên"), ("Dexamethasone", "Viên"), ("Chlorhexidine", "Chai"),
            ("Povidone iodine", "Chai"), ("Natri clorid", "Chai"), ("Natri fluorid", "Tuýp"),
            ("Lidocaine", "Ống"), ("Articaine", "Ống"), ("Mepivacaine", "Ống"), ("Benzocaine", "Tuýp"),
            ("Nystatin", "Chai"), ("Miconazole", "Tuýp"), ("Triamcinolone", "Tuýp")
        ];
        var now = DateTime.UtcNow;
        db.Medicines.AddRange(names.Select((x, i) => new Medicine {
            Code = $"MED{i + 1:D3}", Name = x.Name, ActiveIngredient = x.Name, Unit = x.Unit,
            UnitPrice = 0, IsActive = false, CreatedAt = now,
            SearchText = DiagnosisSearch.Normalize($"MED{i + 1:D3} {x.Name} {x.Name}")
        }));
        await db.SaveChangesAsync(ct);
    }
}
