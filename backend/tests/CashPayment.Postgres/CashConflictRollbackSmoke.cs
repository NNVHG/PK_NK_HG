using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

internal static class CashConflictRollbackSmoke
{
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, int patientId, int cashierId, Action<bool, string> check)
    {
        foreach (var state in new[] { "40001", "40P01", "23505", "23503" })
        {
            await using var setup = new DentalDbContext(options);
            var visit = new Visit { PatientId = patientId, CreatedByUserId = cashierId, Status = "Completed", IsLocked = true };
            var invoice = new Invoice { Visit = visit, CreatedByUserId = cashierId, InvoiceCode = $"TEST-CONFLICT-{state}", TotalAmount = 100, Status = InvoiceStatus.PendingPayment, CreatedAt = DateTime.UtcNow };
            setup.Invoices.Add(invoice); await setup.SaveChangesAsync();
            var request = new CashPaymentRequest(100, 100, Guid.NewGuid());
            var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new WrappedFailure(state)).Options;
            await using var failing = new DentalDbContext(faultOptions);
            if (state == "23503")
            {
                var propagated = false;
                try { await new CashPaymentRepository(failing).ReceiveAsync(invoice.Id, cashierId, request, null); }
                catch (InvalidOperationException) { propagated = true; }
                check(propagated, "Cash unknown SQL state is not hidden as BIL_006");
            }
            else
            {
                var result = await new CashPaymentRepository(failing).ReceiveAsync(invoice.Id, cashierId, request, null);
                check(result.IsFailure && result.Error.Code == "BIL_006", $"Cash wrapped {state} returns BIL_006");
            }
            check(!failing.ChangeTracker.Entries().Any(), $"Cash {state} clears pending tracked rows");
            await using var verify = new DentalDbContext(options);
            var stored = await verify.Invoices.AsNoTracking().SingleAsync(x => x.Id == invoice.Id);
            check(stored.PaidAmount == 0 && stored.Status == InvoiceStatus.PendingPayment &&
                !await verify.PaymentTransactions.AnyAsync(x => x.RequestId == request.RequestId) &&
                !await verify.AuditLogs.AnyAsync(x => x.EntityType == "Invoice" && x.EntityId == invoice.Id), $"Cash {state} rolls back payment, balance and audit");
            var retried = await new CashPaymentRepository(verify).ReceiveAsync(invoice.Id, cashierId, request, null);
            var replayed = await new CashPaymentRepository(verify).ReceiveAsync(invoice.Id, cashierId, request, null);
            check(retried.IsSuccess && replayed.IsSuccess && retried.Value!.Id == replayed.Value!.Id &&
                await verify.PaymentTransactions.CountAsync(x => x.RequestId == request.RequestId) == 1, $"Cash {state} same request retry/replay creates one receipt");
        }
    }

    private sealed class WrappedFailure(string state) : SaveChangesInterceptor
    {
        private int saves;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            // Fail after the payment/balance write, before audit commit, inside a real PostgreSQL transaction.
            if (++saves == 2) throw new InvalidOperationException("Synthetic EF wrapper", new DbUpdateException("Synthetic database wrapper",
                new PostgresException("Synthetic SQL-state fault", "ERROR", "ERROR", state)));
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
