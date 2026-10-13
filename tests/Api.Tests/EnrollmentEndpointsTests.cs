using System.Net;
using System.Net.Http.Json;
using Api.Features.Enrollments;
using Enrollment;
using Messages;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class EnrollmentEndpointsTests
{
    [Fact]
    public async Task Enroll_WhenInvalid_ReturnsValidationProblem()
    {
        await using var factory = new EnrollmentApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/enrollments", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("name"));
        Assert.True(problem.Errors.ContainsKey("email"));
        Assert.True(problem.Errors.ContainsKey("program"));
        Assert.True(problem.Errors.ContainsKey("processing"));
        Assert.Empty(factory.Sender.Sent);
    }

    [Fact]
    public async Task Enroll_WhenImmediate_ReturnsAcceptedWithoutDelay()
    {
        await using var factory = new EnrollmentApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/enrollments", new
        {
            name = "Ada Lovelace",
            email = "ada@example.com",
            program = "Morning cohort",
            processing = "immediate"
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = await response.Content.ReadFromJsonAsync<EnrollmentAccepted>();
        Assert.NotNull(accepted);
        Assert.NotEqual(Guid.Empty, accepted.TrackingId);
        Assert.EndsWith($"/enrollments/{accepted.TrackingId}", response.Headers.Location?.ToString());

        var sent = Assert.Single(factory.Sender.Sent);
        Assert.False(sent.Delay);
        Assert.Equal(accepted.TrackingId, sent.Message.TrackingId);
        Assert.Equal("Ada Lovelace", sent.Message.Name);
    }

    [Fact]
    public async Task Enroll_WhenDelayed_AsksTheSenderToWait()
    {
        await using var factory = new EnrollmentApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/enrollments", new
        {
            name = "Grace Hopper",
            email = "grace@example.com",
            program = "Evening cohort",
            processing = "delayed"
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var sent = Assert.Single(factory.Sender.Sent);
        Assert.True(sent.Delay);
    }

    [Fact]
    public async Task Get_WhenMissing_ReturnsNotFound()
    {
        await using var factory = new EnrollmentApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/enrollments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WhenRowExists_ReturnsStatus()
    {
        await using var factory = new EnrollmentApiFactory();
        using var client = factory.CreateClient();
        var trackingId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EnrollmentsDbContext>();
            db.Enrollments.Add(new EnrollmentRecord
            {
                TrackingId = trackingId,
                Name = "Ada Lovelace",
                Email = "ada@example.com",
                Program = "Weekend cohort",
                Status = EnrollmentStatus.InProgress,
                CreatedUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var status = await client.GetFromJsonAsync<EnrollmentStatusResponse>($"/enrollments/{trackingId}");

        Assert.NotNull(status);
        Assert.Equal(trackingId, status.TrackingId);
        Assert.Equal(EnrollmentStatus.InProgress, status.Status);
        Assert.Equal("Weekend cohort", status.Program);
    }

    [Fact]
    public async Task List_FiltersByEmail()
    {
        await using var factory = new EnrollmentApiFactory();
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EnrollmentsDbContext>();
            db.Enrollments.AddRange(
                new EnrollmentRecord
                {
                    TrackingId = Guid.NewGuid(),
                    Name = "Ada Lovelace",
                    Email = "ada@example.com",
                    Program = "Morning cohort",
                    Status = EnrollmentStatus.Completed,
                    CreatedUtc = DateTimeOffset.UtcNow
                },
                new EnrollmentRecord
                {
                    TrackingId = Guid.NewGuid(),
                    Name = "Grace Hopper",
                    Email = "grace@example.com",
                    Program = "Evening cohort",
                    Status = EnrollmentStatus.Completed,
                    CreatedUtc = DateTimeOffset.UtcNow
                });
            await db.SaveChangesAsync();
        }

        var matches = await client.GetFromJsonAsync<EnrollmentStatusResponse[]>("/enrollments?email=ADA@example.com");

        var match = Assert.Single(matches!);
        Assert.Equal("ada@example.com", match.Email);
    }
}

public sealed class EnrollmentApiFactory : WebApplicationFactory<Program>
{
    public CapturingEnrollmentSender Sender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IEnrollmentSender>(Sender);
        });
    }
}

public sealed class CapturingEnrollmentSender : IEnrollmentSender
{
    public List<(EnrollStudentCommand Message, bool Delay)> Sent { get; } = [];

    public Task SendAsync(EnrollStudentCommand message, bool delay, CancellationToken cancellationToken)
    {
        Sent.Add((message, delay));
        return Task.CompletedTask;
    }
}
