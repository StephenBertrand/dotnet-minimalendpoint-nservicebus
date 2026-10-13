using Enrollment;
using Enrollment.EnrollStudent;
using Messages;
using Microsoft.EntityFrameworkCore;

namespace Endpoint.Tests;

public sealed class EnrollStudentTests
{
    [Fact]
    public async Task Process_SavesACompletedRow()
    {
        await using var db = NewDatabase();
        var enroll = NewEnroll(db);
        var trackingId = Guid.NewGuid();

        var outcome = await enroll.ProcessAsync(new EnrollStudentCommand
        {
            TrackingId = trackingId,
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Program = "Morning cohort"
        }, CancellationToken.None);

        Assert.Equal(EnrollmentOutcome.Completed, outcome);
        var row = await db.Enrollments.SingleAsync();
        Assert.Equal(trackingId, row.TrackingId);
        Assert.Equal(EnrollmentStatus.Completed, row.Status);
        Assert.Equal("Ada Lovelace", row.Name);
        Assert.NotNull(row.CompletedUtc);
    }

    [Fact]
    public async Task Process_WhenAlreadyCompleted_ReturnsCompletedWithoutAnotherRow()
    {
        await using var db = NewDatabase();
        var enroll = NewEnroll(db);
        var message = new EnrollStudentCommand
        {
            TrackingId = Guid.NewGuid(),
            Name = "Grace Hopper",
            Email = "grace@example.com",
            Program = "Evening cohort"
        };

        await enroll.ProcessAsync(message, CancellationToken.None);
        var completedAt = (await db.Enrollments.SingleAsync()).CompletedUtc;

        var outcome = await enroll.ProcessAsync(message, CancellationToken.None);

        Assert.Equal(EnrollmentOutcome.Completed, outcome);
        var row = await db.Enrollments.SingleAsync();
        Assert.Equal(completedAt, row.CompletedUtc);
    }

    [Fact]
    public async Task Process_WhenEmailIsAlreadyEnrolled_RejectsTheNewTrackingId()
    {
        await using var db = NewDatabase();
        var enroll = NewEnroll(db);
        await enroll.ProcessAsync(new EnrollStudentCommand
        {
            TrackingId = Guid.NewGuid(),
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Program = "Morning cohort"
        }, CancellationToken.None);

        var duplicateId = Guid.NewGuid();
        var outcome = await enroll.ProcessAsync(new EnrollStudentCommand
        {
            TrackingId = duplicateId,
            Name = "Ada Lovelace",
            Email = "ADA@example.com",
            Program = "Evening cohort"
        }, CancellationToken.None);

        Assert.Equal(EnrollmentOutcome.Rejected, outcome);
        var duplicate = await db.Enrollments.SingleAsync(row => row.TrackingId == duplicateId);
        Assert.Equal(EnrollmentStatus.Rejected, duplicate.Status);
        Assert.Equal(EnrollmentStatus.DuplicateEmailMessage, duplicate.Message);
        Assert.Equal(1, await db.Enrollments.CountAsync(row => row.Status == EnrollmentStatus.Completed));
    }

    static EnrollmentsDbContext NewDatabase()
    {
        var options = new DbContextOptionsBuilder<EnrollmentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EnrollmentsDbContext(options);
    }

    static EnrollStudent NewEnroll(EnrollmentsDbContext db) => new(db);
}
