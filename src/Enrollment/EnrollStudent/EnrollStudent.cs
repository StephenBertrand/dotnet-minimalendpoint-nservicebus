using Enrollment;
using Messages;
using Microsoft.EntityFrameworkCore;

namespace Enrollment.EnrollStudent;

public sealed class EnrollStudent(EnrollmentsDbContext db)
{
    public async Task<EnrollmentOutcome> ProcessAsync(EnrollStudentCommand message, CancellationToken cancellationToken)
    {
        var status = await StatusForTrackingIdAsync(message.TrackingId, cancellationToken);

        if (status == EnrollmentStatus.Completed)
        {
            return EnrollmentOutcome.Completed;
        }

        if (status == EnrollmentStatus.Rejected)
        {
            return EnrollmentOutcome.Rejected;
        }

        if (await EmailIsEnrolledAsync(message, cancellationToken))
        {
            await SaveAsync(message, EnrollmentStatus.Rejected, EnrollmentStatus.DuplicateEmailMessage, completedUtc: null, cancellationToken);
            return EnrollmentOutcome.Rejected;
        }

        await SaveAsync(message, EnrollmentStatus.Completed, messageText: null, DateTimeOffset.UtcNow, cancellationToken);
        return EnrollmentOutcome.Completed;
    }

    Task<string?> StatusForTrackingIdAsync(Guid trackingId, CancellationToken cancellationToken) =>
        db.Enrollments.AsNoTracking()
            .Where(row => row.TrackingId == trackingId)
            .Select(row => row.Status)
            .FirstOrDefaultAsync(cancellationToken);

    async Task<bool> EmailIsEnrolledAsync(EnrollStudentCommand message, CancellationToken cancellationToken)
    {
        var email = message.Email.Trim().ToLower();
        return await db.Enrollments.AsNoTracking().AnyAsync(
            row => row.TrackingId != message.TrackingId
                && row.Status == EnrollmentStatus.Completed
                && row.Email.ToLower() == email,
            cancellationToken);
    }

    async Task SaveAsync(
        EnrollStudentCommand message,
        string status,
        string? messageText,
        DateTimeOffset? completedUtc,
        CancellationToken cancellationToken)
    {
        var row = await db.Enrollments.FirstOrDefaultAsync(item => item.TrackingId == message.TrackingId, cancellationToken);
        if (row is null)
        {
            row = new EnrollmentRecord
            {
                TrackingId = message.TrackingId,
                CreatedUtc = DateTimeOffset.UtcNow
            };
            db.Enrollments.Add(row);
        }

        row.Name = message.Name;
        row.Email = message.Email;
        row.Program = message.Program;
        row.Status = status;
        row.Message = messageText;
        row.CompletedUtc = completedUtc;
        await db.SaveChangesAsync(cancellationToken);
    }
}
