using Enrollment;
using EnrollmentEndpoint;
using Microsoft.EntityFrameworkCore;

namespace Endpoint.Tests;

public sealed class EnrollmentProcessorTests
{
    [Fact]
    public async Task Process_SavesACompletedRow()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EnrollmentsDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var processor = new EnrollmentProcessor(options);
        var trackingId = Guid.NewGuid();
        var message = new EnrollStudentCommand
        {
            TrackingId = trackingId,
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Program = "Morning cohort"
        };

        await processor.ProcessAsync(message, CancellationToken.None);

        await using var db = new EnrollmentsDbContext(options);
        var row = await db.Enrollments.SingleAsync();
        Assert.Equal(trackingId, row.TrackingId);
        Assert.Equal(EnrollmentStatus.Completed, row.Status);
        Assert.Equal("Ada Lovelace", row.Name);
        Assert.NotNull(row.CompletedUtc);
    }

    [Fact]
    public async Task Process_WhenAlreadyCompleted_DoesNotAddAnotherRow()
    {
        var options = new DbContextOptionsBuilder<EnrollmentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var processor = new EnrollmentProcessor(options);
        var message = new EnrollStudentCommand
        {
            TrackingId = Guid.NewGuid(),
            Name = "Grace Hopper",
            Email = "grace@example.com",
            Program = "Evening cohort"
        };

        await processor.ProcessAsync(message, CancellationToken.None);
        await processor.ProcessAsync(message, CancellationToken.None);

        await using var db = new EnrollmentsDbContext(options);
        Assert.Equal(1, await db.Enrollments.CountAsync());
    }

    [Fact]
    public async Task Process_WhenEmailIsAlreadyEnrolled_RejectsTheNewTrackingId()
    {
        var options = new DbContextOptionsBuilder<EnrollmentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var processor = new EnrollmentProcessor(options);
        await processor.ProcessAsync(new EnrollStudentCommand
        {
            TrackingId = Guid.NewGuid(),
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Program = "Morning cohort"
        }, CancellationToken.None);

        var duplicateId = Guid.NewGuid();
        await processor.ProcessAsync(new EnrollStudentCommand
        {
            TrackingId = duplicateId,
            Name = "Ada Lovelace",
            Email = "ADA@example.com",
            Program = "Evening cohort"
        }, CancellationToken.None);

        await using var db = new EnrollmentsDbContext(options);
        var duplicate = await db.Enrollments.SingleAsync(row => row.TrackingId == duplicateId);
        Assert.Equal(EnrollmentStatus.Rejected, duplicate.Status);
        Assert.Equal(EnrollmentStatus.DuplicateEmailMessage, duplicate.Message);
        Assert.Equal(1, await db.Enrollments.CountAsync(row => row.Status == EnrollmentStatus.Completed));
    }
}
