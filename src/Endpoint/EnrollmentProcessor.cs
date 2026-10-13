using System.Transactions;
using Enrollment;
using Microsoft.EntityFrameworkCore;

namespace EnrollmentEndpoint;

/// <summary>
/// Writes the tracking row outside the transport transaction so a poll can see it
/// before the handler finishes, and again when the enrollment is saved.
/// </summary>
public sealed class EnrollmentProcessor(DbContextOptions<EnrollmentsDbContext> options)
{
    public async Task<EnrollmentOutcome> ProcessAsync(EnrollStudentCommand message, CancellationToken cancellationToken)
    {
        var existing = await FindAsync(message.TrackingId, cancellationToken);
        if (existing is { Status: EnrollmentStatus.Completed })
        {
            return EnrollmentOutcome.Completed;
        }

        if (existing is { Status: EnrollmentStatus.Rejected })
        {
            return EnrollmentOutcome.Rejected;
        }

        if (existing is null)
        {
            await SaveAsync(new EnrollmentRecord
            {
                TrackingId = message.TrackingId,
                Name = message.Name,
                Email = message.Email,
                Program = message.Program,
                Status = EnrollmentStatus.InProgress,
                CreatedUtc = DateTimeOffset.UtcNow
            }, cancellationToken);
        }

        var row = await FindAsync(message.TrackingId, cancellationToken)
                  ?? throw new InvalidOperationException($"Enrollment {message.TrackingId} was not started.");

        row.Name = message.Name;
        row.Email = message.Email;
        row.Program = message.Program;

        if (await EmailIsAlreadyEnrolledAsync(message, cancellationToken))
        {
            row.Status = EnrollmentStatus.Rejected;
            row.Message = EnrollmentStatus.DuplicateEmailMessage;
            row.CompletedUtc = null;
            await SaveAsync(row, cancellationToken);
            return EnrollmentOutcome.Rejected;
        }

        row.Status = EnrollmentStatus.Completed;
        row.Message = null;
        row.CompletedUtc = DateTimeOffset.UtcNow;
        try
        {
            await SaveAsync(row, cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (!await EmailIsAlreadyEnrolledAsync(message, cancellationToken))
            {
                throw;
            }

            row.Status = EnrollmentStatus.Rejected;
            row.Message = EnrollmentStatus.DuplicateEmailMessage;
            row.CompletedUtc = null;
            await SaveAsync(row, cancellationToken);
            return EnrollmentOutcome.Rejected;
        }

        return EnrollmentOutcome.Completed;
    }

    private async Task<bool> EmailIsAlreadyEnrolledAsync(EnrollStudentCommand message, CancellationToken cancellationToken)
    {
        using var suppress = SuppressAmbientTransaction();
        await using var db = new EnrollmentsDbContext(options);
        var email = message.Email.Trim().ToLower();
        var taken = await db.Enrollments.AsNoTracking().AnyAsync(
            item => item.TrackingId != message.TrackingId
                    && item.Status == EnrollmentStatus.Completed
                    && item.Email.ToLower() == email,
            cancellationToken);
        suppress.Complete();
        return taken;
    }

    private async Task<EnrollmentRecord?> FindAsync(Guid trackingId, CancellationToken cancellationToken)
    {
        using var suppress = SuppressAmbientTransaction();
        await using var db = new EnrollmentsDbContext(options);
        var row = await db.Enrollments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TrackingId == trackingId, cancellationToken);
        suppress.Complete();
        return row;
    }

    private async Task SaveAsync(EnrollmentRecord row, CancellationToken cancellationToken)
    {
        using var suppress = SuppressAmbientTransaction();
        await using var db = new EnrollmentsDbContext(options);
        var existing = await db.Enrollments.FirstOrDefaultAsync(item => item.TrackingId == row.TrackingId, cancellationToken);
        if (existing is null)
        {
            db.Enrollments.Add(row);
        }
        else
        {
            existing.Name = row.Name;
            existing.Email = row.Email;
            existing.Program = row.Program;
            existing.Status = row.Status;
            existing.Message = row.Message;
            existing.CompletedUtc = row.CompletedUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
        suppress.Complete();
    }

    private static TransactionScope SuppressAmbientTransaction() =>
        new(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
}
