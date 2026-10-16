using Dental.Application.Common;
using Dental.Application.Features.Visits.DTOs;
using Dental.Application.Features.Visits.Services;
using Dental.Application.Features.Visits.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Patients;

public sealed class PostTreatmentNoteTests
{
    private static IUnitOfWork CreateUnitOfWork()
    {
        var uow = Substitute.For<IUnitOfWork>();
        uow.ExecuteSerializableTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<Result<PostTreatmentNoteResponse>>>>(),
            Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<PostTreatmentNoteResponse>>>>()(CancellationToken.None));
        return uow;
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_RejectsEmptyOrWhitespace(string note)
    {
        var validator = new CreatePostTreatmentNoteRequestValidator();
        var result = validator.Validate(new CreatePostTreatmentNoteRequest(note));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_RejectsOver1000Chars()
    {
        var validator = new CreatePostTreatmentNoteRequestValidator();
        var note = new string('A', 1001);
        var result = validator.Validate(new CreatePostTreatmentNoteRequest(note));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Note");
    }

    [Fact]
    public void Validator_AcceptsValidText()
    {
        var validator = new CreatePostTreatmentNoteRequestValidator();
        var result = validator.Validate(new CreatePostTreatmentNoteRequest("Bệnh nhân ổn định."));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task DbContext_GuardRejectsModifyOrDelete(EntityState state)
    {
        var options = new DbContextOptionsBuilder<DentalDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        await using var context = new DentalDbContext(options);
        var note = new PostTreatmentNote { Id = 1, VisitId = 1, CreatedByUserId = 1, Note = "Test" };
        context.PostTreatmentNotes.Attach(note);
        context.Entry(note).State = state;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Contains("PostTreatmentNote", ex.Message);
    }

    [Theory]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(RoleCodes.Assistant)]
    [InlineData(RoleCodes.Patient)]
    public async Task AddAsync_NonDentistOrAdmin_ReturnsForbidden(string role)
    {
        var service = new PostTreatmentNoteService(
            Substitute.For<IVisitRepository>(),
            Substitute.For<IPatientRepository>(),
            Substitute.For<IInvoiceRepository>(),
            Substitute.For<IPostTreatmentNoteRepository>(),
            CreateUnitOfWork(),
            Substitute.For<IAuditLogger>());

        var result = await service.AddAsync(
            1, new CreatePostTreatmentNoteRequest("Ghi chú"), 5, role, "127.0.0.1");

        Assert.True(result.IsFailure);
        Assert.Equal(Error.Forbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task AddAsync_DentistMismatch_ReturnsForbidden()
    {
        var visitRepo = Substitute.For<IVisitRepository>();
        visitRepo.GetForUpdateAsync(10, Arg.Any<CancellationToken>())
            .Returns(new Visit { VisitId = 10, DentistId = 15, Status = VisitStatuses.Completed, IsLocked = true });

        var service = new PostTreatmentNoteService(
            visitRepo,
            Substitute.For<IPatientRepository>(),
            Substitute.For<IInvoiceRepository>(),
            Substitute.For<IPostTreatmentNoteRepository>(),
            CreateUnitOfWork(),
            Substitute.For<IAuditLogger>());

        var result = await service.AddAsync(
            10, new CreatePostTreatmentNoteRequest("Ghi chú"), 99, RoleCodes.Dentist, "127.0.0.1");

        Assert.True(result.IsFailure);
        Assert.Equal(Error.DiagnosisDentistMismatch.Code, result.Error.Code);
    }
    [Fact]
    public async Task AddAsync_VisitNotCompletedOrNotLocked_ReturnsConflict()
    {
        var visitRepo = Substitute.For<IVisitRepository>();
        visitRepo.GetForUpdateAsync(10, Arg.Any<CancellationToken>())
            .Returns(new Visit { VisitId = 10, DentistId = 15, Status = VisitStatuses.InProgress, IsLocked = false });

        var service = new PostTreatmentNoteService(
            visitRepo, Substitute.For<IPatientRepository>(), Substitute.For<IInvoiceRepository>(),
            Substitute.For<IPostTreatmentNoteRepository>(), CreateUnitOfWork(), Substitute.For<IAuditLogger>());

        var result = await service.AddAsync(10, new CreatePostTreatmentNoteRequest("Ghi chú"), 15, RoleCodes.Dentist, "127.0.0.1");
        Assert.True(result.IsFailure);
        Assert.Equal("PAT_081", result.Error.Code);
    }

    [Fact]
    public async Task AddAsync_InvoiceNotPaid_ReturnsConflict()
    {
        var visitRepo = Substitute.For<IVisitRepository>();
        visitRepo.GetForUpdateAsync(10, Arg.Any<CancellationToken>())
            .Returns(new Visit { VisitId = 10, DentistId = 15, Status = VisitStatuses.Completed, IsLocked = true });

        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        invoiceRepo.GetForVisitAsync(10, Arg.Any<CancellationToken>())
            .Returns(new Invoice { VisitId = 10, Status = InvoiceStatus.PendingPayment });

        var service = new PostTreatmentNoteService(
            visitRepo, Substitute.For<IPatientRepository>(), invoiceRepo,
            Substitute.For<IPostTreatmentNoteRepository>(), CreateUnitOfWork(), Substitute.For<IAuditLogger>());

        var result = await service.AddAsync(10, new CreatePostTreatmentNoteRequest("Ghi chú"), 15, RoleCodes.Dentist, "127.0.0.1");
        Assert.True(result.IsFailure);
        Assert.Equal("PAT_082", result.Error.Code);
    }

    [Fact]
    public async Task AddAsync_RequestIdIdempotency_ReturnsExistingNote()
    {
        var requestId = Guid.NewGuid();
        var noteRepo = Substitute.For<IPostTreatmentNoteRepository>();
        var existing = new PostTreatmentNote
        {
            Id = 42, VisitId = 10, CreatedByUserId = 15, Note = "Đã tồn tại", RequestId = requestId,
            CreatedByUser = new User { FullName = "Bác sĩ A", Role = new Role { RoleName = "Nha sĩ" } }
        };
        noteRepo.GetByRequestIdAsync(requestId, Arg.Any<CancellationToken>()).Returns(existing);

        var visits = Substitute.For<IVisitRepository>();
        visits.GetForUpdateAsync(10, Arg.Any<CancellationToken>()).Returns(new Visit { VisitId = 10, DentistId = 15, Status = VisitStatuses.Completed, IsLocked = true });
        var invoices = Substitute.For<IInvoiceRepository>();
        invoices.GetForVisitAsync(10, Arg.Any<CancellationToken>()).Returns(new Invoice { Status = InvoiceStatus.Paid });
        var service = new PostTreatmentNoteService(visits, Substitute.For<IPatientRepository>(), invoices,
            noteRepo, CreateUnitOfWork(), Substitute.For<IAuditLogger>());

        var result = await service.AddAsync(10, new CreatePostTreatmentNoteRequest("Đã tồn tại", requestId), 15, RoleCodes.Dentist, "127.0.0.1");
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(42, result.Value.Id);
        await noteRepo.DidNotReceive().AddAsync(Arg.Any<PostTreatmentNote>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_Success_CreatesNoteAndAuditsMetadataOnly()
    {
        var visitRepo = Substitute.For<IVisitRepository>();
        visitRepo.GetForUpdateAsync(10, Arg.Any<CancellationToken>())
            .Returns(new Visit { VisitId = 10, DentistId = 15, Status = VisitStatuses.Completed, IsLocked = true });

        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        invoiceRepo.GetForVisitAsync(10, Arg.Any<CancellationToken>())
            .Returns(new Invoice { VisitId = 10, Status = InvoiceStatus.Paid });

        var noteRepo = Substitute.For<IPostTreatmentNoteRepository>();
        var audit = Substitute.For<IAuditLogger>();

        var service = new PostTreatmentNoteService(
            visitRepo, Substitute.For<IPatientRepository>(), invoiceRepo, noteRepo, CreateUnitOfWork(), audit);

        var result = await service.AddAsync(10, new CreatePostTreatmentNoteRequest("  Bệnh nhân ổn định.  "), 15, RoleCodes.Dentist, "127.0.0.1");
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Bệnh nhân ổn định.", result.Value.Note);

        await noteRepo.Received(1).AddAsync(
            Arg.Is<PostTreatmentNote>(n => n.VisitId == 10 && n.Note == "Bệnh nhân ổn định."), Arg.Any<CancellationToken>());

        await audit.Received(1).LogRequiredAsync(
            action: AuditActions.PostTreatmentNoteCreated,
            entityType: "Visit",
            entityId: 10,
            userId: 15,
            detail: Arg.Is<string>(d => !d.Contains("ổn định") && d.Contains("create_post_treatment_note")),
            ipAddress: "127.0.0.1",
            ct: Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task GetAsync_PatientOwner_CanRead()
    {
        var visitRepo = Substitute.For<IVisitRepository>();
        visitRepo.GetByIdAsync(10, Arg.Any<CancellationToken>()).Returns(new Visit { VisitId = 10, PatientId = 7 });

        var patientRepo = Substitute.For<IPatientRepository>();
        patientRepo.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(new Patient { PatientId = 7, UserId = 100 });

        var noteRepo = Substitute.For<IPostTreatmentNoteRepository>();
        noteRepo.GetByVisitIdAsync(10, Arg.Any<CancellationToken>())
            .Returns(new List<PostTreatmentNote> { new() { Id = 1, VisitId = 10, Note = "Theo dõi hậu phẫu" } });

        var service = new PostTreatmentNoteService(
            visitRepo, patientRepo, Substitute.For<IInvoiceRepository>(), noteRepo, CreateUnitOfWork(), Substitute.For<IAuditLogger>());

        var result = await service.GetAsync(10, userId: 100, role: RoleCodes.Patient);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value);
    }

    [Fact]
    public async Task GetAsync_PatientIdor_ReturnsNotFound()
    {
        var visitRepo = Substitute.For<IVisitRepository>();
        visitRepo.GetByIdAsync(10, Arg.Any<CancellationToken>()).Returns(new Visit { VisitId = 10, PatientId = 7 });

        var patientRepo = Substitute.For<IPatientRepository>();
        patientRepo.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(new Patient { PatientId = 7, UserId = 200 });

        var service = new PostTreatmentNoteService(
            visitRepo, patientRepo, Substitute.For<IInvoiceRepository>(), Substitute.For<IPostTreatmentNoteRepository>(), CreateUnitOfWork(), Substitute.For<IAuditLogger>());

        var result = await service.GetAsync(10, userId: 100, role: RoleCodes.Patient);
        Assert.True(result.IsFailure);
        Assert.Equal(Error.NotFound.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(11, 15, "Same", "GEN_001")]
    [InlineData(10, 99, "Same", "GEN_001")]
    [InlineData(10, 15, "Changed", "PAT_083")]
    public async Task Replay_MustMatchVisitAuthorAndContent(int savedVisit, int savedAuthor, string content, string error)
    {
        var visits = Substitute.For<IVisitRepository>();
        visits.GetForUpdateAsync(10, Arg.Any<CancellationToken>()).Returns(new Visit { VisitId = 10, DentistId = 15, Status = VisitStatuses.Completed, IsLocked = true });
        var invoices = Substitute.For<IInvoiceRepository>();
        invoices.GetForVisitAsync(10, Arg.Any<CancellationToken>()).Returns(new Invoice { Status = InvoiceStatus.Paid });
        var notes = Substitute.For<IPostTreatmentNoteRepository>(); var audit = Substitute.For<IAuditLogger>(); var key = Guid.NewGuid();
        notes.GetByRequestIdAsync(key, Arg.Any<CancellationToken>()).Returns(new PostTreatmentNote { VisitId = savedVisit, CreatedByUserId = savedAuthor, Note = "Same" });
        var service = new PostTreatmentNoteService(visits, Substitute.For<IPatientRepository>(), invoices, notes, CreateUnitOfWork(), audit);
        Assert.Equal(error, (await service.AddAsync(10, new(content, key), 15, RoleCodes.Dentist, null)).Error.Code);
        await notes.DidNotReceive().AddAsync(Arg.Any<PostTreatmentNote>(), Arg.Any<CancellationToken>()); Assert.Empty(audit.ReceivedCalls());
    }

    [Fact]
    public async Task Replay_NeverBypassesVisitOwnership()
    {
        var visits = Substitute.For<IVisitRepository>();
        visits.GetForUpdateAsync(10, Arg.Any<CancellationToken>()).Returns(new Visit { VisitId = 10, DentistId = 99, Status = VisitStatuses.Completed, IsLocked = true });
        var notes = Substitute.For<IPostTreatmentNoteRepository>();
        var service = new PostTreatmentNoteService(visits, Substitute.For<IPatientRepository>(), Substitute.For<IInvoiceRepository>(), notes, CreateUnitOfWork(), Substitute.For<IAuditLogger>());
        Assert.Equal(Error.DiagnosisDentistMismatch, (await service.AddAsync(10, new("Synthetic note", Guid.NewGuid()), 15, RoleCodes.Dentist, null)).Error);
        Assert.Empty(notes.ReceivedCalls());
    }

    [Fact]
    public void Validator_RejectsEmptyRequestId()
        => Assert.False(new CreatePostTreatmentNoteRequestValidator().Validate(new CreatePostTreatmentNoteRequest("Synthetic note", Guid.Empty)).IsValid);

}
