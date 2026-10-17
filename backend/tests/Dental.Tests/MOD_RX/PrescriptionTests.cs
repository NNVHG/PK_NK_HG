using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.MOD_RX.Services;
using Dental.Application.Features.MOD_RX.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using NSubstitute;
using Xunit;
namespace Dental.Tests.MOD_RX;
public sealed class PrescriptionTests
{
    private static Medicine Drug(int id = 1, string ingredient = "Alpha") => new() { Id = id, Version = 1, Code = "MED" + id, Name = "Synthetic drug " + id, ActiveIngredient = ingredient, Unit = "Viên", UnitPrice = 500, IsActive = true };
    private static PrescriptionWriteRequest Input() => new(0, "Synthetic diagnosis", [new(1, 1, 2, "Synthetic instructions")]);
    private static PrescriptionSnapshot State() => new(new Visit { VisitId = 7, PatientId = 9, DentistId = 3, Status = VisitStatuses.InProgress }, null, null, false, null, [], [Drug()], []);
    private static Prescription Existing() => new() { Id = 8, VisitId = 7, DoctorId = 3, Version = 1, Items = [new() { Id = 5, MedicineId = 1, MedicineCode = "MED1", MedicineName = "Original drug", ActiveIngredient = "Alpha", Unit = "Viên", UnitPrice = 500, Quantity = 2, UsageInstructions = "Original instructions" }] };
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Patient)] [InlineData(null)]
    public async Task OnlyDentistCanWrite(string? role)
    {
        var repo = Substitute.For<IPrescriptionRepository>(); var s = new PrescriptionService(repo);
        Assert.Equal(Error.Forbidden, (await s.PreviewAsync(7, 3, role, Input())).Error);
        Assert.Equal(Error.Forbidden, (await s.SaveAsync(7, 3, role, true, Input())).Error);
        Assert.Equal(Error.Forbidden, (await s.LatestAsync(9, 7, 3, role)).Error);
        Assert.Equal(Error.Forbidden, (await s.MedicinesAsync(7, 3, role, new())).Error); Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public async Task InvalidOrUnpreviewedRequestsNeverWrite()
    {
        var repo = Substitute.For<IPrescriptionRepository>(); var s = new PrescriptionService(repo);
        Assert.True((await s.SaveAsync(7, 3, RoleCodes.Dentist, true, Input())).IsFailure);
        Assert.True((await s.SaveAsync(7, 3, RoleCodes.Dentist, false, Input() with { PreviewToken = new string('A', 64) })).IsFailure);
        Assert.True((await s.MedicinesAsync(7, 3, RoleCodes.Dentist, new(IncludeInactive: true))).IsFailure); Assert.Empty(repo.ReceivedCalls());
    }
    [Theory] [InlineData(0, false)] [InlineData(1, true)] [InlineData(60, true)] [InlineData(100, true)] [InlineData(101, false)]
    public void QuantityUsesApproved100Limit(int quantity, bool valid) => Assert.Equal(valid, new PrescriptionWriteValidator().Validate(Input() with { Items = [new(1, 1, quantity, "Test")] }).IsValid);
    [Theory] [InlineData(0, false)] [InlineData(1, true)] [InlineData(30, true)] [InlineData(31, false)] [InlineData(null, true)]
    public void DaysOptionalAndBounded(int? days, bool valid) => Assert.Equal(valid, new PrescriptionWriteValidator().Validate(Input() with { Items = [new(1, 1, 2, "Test", days)] }).IsValid);
    [Fact] public void InvalidBoundsAndDuplicateMedicinesRejected()
    {
        foreach (var r in new[] { Input() with { Diagnosis = " " }, Input() with { Diagnosis = new string('a', 501) }, Input() with { DoctorAdvice = new string('a', 1001) }, Input() with { Items = [] }, Input() with { Items = null! }, Input() with { Items = [null!] }, Input() with { Items = [new(1, 1, 1, "T"), new(1, 1, 1, "T")] }, Input() with { Items = Enumerable.Range(1, 11).Select(i => new PrescriptionLineRequest(i, 1, 1, "Test")).ToArray() }, Input() with { Items = [new(1, 1, 1, " ")] }, Input() with { PreviewToken = "bad" } }) Assert.False(new PrescriptionWriteValidator().Validate(r).IsValid);
    }
    [Fact] public void PreviewBindsClinicalAndFinancialSnapshot()
    {
        var state = State(); var r = Input(); var p = PrescriptionRules.Build(state, r, false).Value!.Preview;
        Assert.Equal(1000, p.MedicineTotal); Assert.Equal(1000, p.FinalAmount); Assert.Equal(64, p.Token.Length);
        Assert.True(PrescriptionRules.Build(state, r with { PreviewToken = p.Token }, true).IsSuccess);
        Assert.Equal("RX_003", PrescriptionRules.Build(state, r with { PreviewToken = p.Token, DoctorAdvice = "Changed" }, true).Error.Code);
        Assert.Equal("RX_003", PrescriptionRules.Build(state with { AllergyRecordId = 10 }, r with { PreviewToken = p.Token }, true).Error.Code);
    }
    [Fact] public void AllergyRequiresAcknowledgementAndReason()
    {
        var state = State() with { AllergyRecordId = 4, Allergies = [new() { ItemId = 6, Type = MedicalHistoryTypes.Allergy, Name = "ALPHA", Detail = "Synthetic reaction" }] };
        var r = Input(); var p = PrescriptionRules.Build(state, r, false).Value!.Preview; Assert.Single(p.AllergyWarnings);
        Assert.Equal("RX_006", PrescriptionRules.Build(state, r with { PreviewToken = p.Token }, true).Error.Code);
        var confirmed = r with { PreviewToken = p.Token, Items = [r.Items[0] with { AllergyConfirmed = true, AllergyOverrideReason = "Clinical decision confirmed" }] };
        Assert.Equal("Clinical decision confirmed", PrescriptionRules.Build(state, confirmed, true).Value!.Items[0].AllergyOverrideReason);
        Assert.Equal("RX_006", PrescriptionRules.Build(state, confirmed with { Items = [confirmed.Items[0] with { AllergyOverrideReason = "no" }] }, true).Error.Code);
    }
    [Fact] public void DuplicateIngredientWarningRequiresConfirmation()
    {
        var state = State() with { Medicines = [Drug(1, "Alpha / Beta"), Drug(2, "BETA")] };
        var r = Input() with { Items = [new(1, 1, 2, "Test"), new(2, 1, 1, "Test")] }; var p = PrescriptionRules.Build(state, r, false).Value!.Preview;
        Assert.Equal("beta", Assert.Single(p.DuplicateIngredients));
        Assert.Equal("RX_007", PrescriptionRules.Build(state, r with { PreviewToken = p.Token }, true).Error.Code);
        Assert.True(PrescriptionRules.Build(state, r with { PreviewToken = p.Token, DuplicateIngredientConfirmed = true }, true).IsSuccess);
    }
    [Fact] public void AllergyNormalizationAndIngredientTokens()
    {
        MedicalHistoryItem[] h = [new() { Type = MedicalHistoryTypes.Allergy, Name = "Dược Chất", ItemId = 1 }, new() { Type = "Condition", Name = "Alpha", ItemId = 2 }];
        Assert.Equal(1, Assert.Single(PrescriptionRules.MatchingAllergies("Test", "duoc chat + Alpha", h)).ItemId);
        Assert.Equal(new[] { "alpha", "beta" }, PrescriptionRules.Ingredients("Alpha / Beta + alpha"));
    }
    [Fact] public void StaleCatalogOrInactiveMedicineRejected()
    {
        var state = State(); state.Medicines[0].Version = 2; Assert.Equal("RX_003", PrescriptionRules.Build(state, Input(), false).Error.Code);
        state.Medicines[0].IsActive = false; Assert.Equal("RX_004", PrescriptionRules.Build(state, Input(), false).Error.Code);
    }
    [Fact] public void ExistingSnapshotPriceIsFixedButNewOrderUsesCurrentPrice()
    {
        var state = State() with { Prescription = Existing() }; state.Medicines[0].UnitPrice = 900; state.Medicines[0].Name = "Changed product";
        var p = PrescriptionRules.Build(state, Input() with { ExpectedVersion = 1 }, false).Value!;
        Assert.Equal(500, p.Items[0].UnitPrice); Assert.Equal("Original drug", p.Items[0].MedicineName);
        Assert.Equal(1800, PrescriptionRules.Build(state with { Prescription = null }, Input(), false).Value!.Preview.MedicineTotal);
    }
    [Theory] [InlineData(InvoiceStatus.Paid)] [InlineData(InvoiceStatus.PartiallyPaid)]
    public void CollectedStatusLocks(InvoiceStatus status) => Assert.False(PrescriptionRules.CanEdit(State().Visit, new() { Status = status }, false, Existing()));
    [Fact] public void HistoryCancellationAndDispenseLock()
    {
        var s = State(); Assert.False(PrescriptionRules.CanEdit(s.Visit, null, true, null));
        var rx = Existing(); rx.IsCancelled = true; Assert.False(PrescriptionRules.CanEdit(s.Visit, null, false, rx));
        rx.IsCancelled = false; rx.DispensedAt = DateTime.UtcNow; Assert.False(PrescriptionRules.CanEdit(s.Visit, null, false, rx));
    }
    [Fact] public void CompletedPendingOnlyEditsExistingOrder()
    {
        var v = State().Visit; v.Status = VisitStatuses.Completed; v.IsLocked = true;
        Assert.True(PrescriptionRules.CanEdit(v, new() { Status = InvoiceStatus.PendingPayment }, false, Existing()));
        Assert.False(PrescriptionRules.CanEdit(v, new() { Status = InvoiceStatus.PendingPayment }, false, null));
        Assert.False(PrescriptionRules.CanEdit(v, new() { Status = InvoiceStatus.Draft }, false, Existing()));
    }
    [Fact] public void DiscountOverflowAndStaleVersionFail()
    {
        Assert.Equal("RX_008", PrescriptionRules.Build(State() with { Invoice = new() { Status = InvoiceStatus.Draft, DiscountAmount = 2000 } }, Input(), false).Error.Code);
        var state = State(); state.Medicines[0].UnitPrice = InvoiceServiceSnapshot.MaximumAmount; Assert.Equal("RX_005", PrescriptionRules.Build(state, Input(), false).Error.Code);
        Assert.Equal("RX_003", PrescriptionRules.Build(State() with { Prescription = Existing() }, Input(), false).Error.Code);
    }
    [Fact] public void InvoiceIncludesMedicineAndPreservesExistingDiscount()
    {
        VisitService[] services = [new() { Id = 1, ServiceCode = "S", ServiceName = "Test", Quantity = 1, UnitPrice = 2000 }];
        var invoice = new Invoice { Status = InvoiceStatus.Draft, DiscountAmount = 200 };
        Assert.True(InvoiceDraftSynchronizer.Apply(invoice, services, Existing().Items).IsSuccess); Assert.Equal(3000, invoice.TotalAmount); Assert.Equal(2800, invoice.FinalAmount);
        Assert.Equal(5, Assert.Single(invoice.Items, i => i.ItemType == "Medicine").SourcePrescriptionItemId);
        Assert.False(InvoiceDraftSynchronizer.Apply(invoice, services, Existing().Items).Value);
    }
}
