using Api.Hosting;
using Enrollment;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Enrollments;

public sealed class EnrollmentsEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/enrollments").WithTags("Enrollments");
        group.MapPost("/", EnrollAsync).WithName("Enroll");
        group.MapGet("/", ListAsync).WithName("ListEnrollments");
        group.MapGet("/{trackingId:guid}", GetAsync).WithName("GetEnrollment");
    }

    private static async Task<IResult> EnrollAsync(
        EnrollRequest request,
        IValidator<EnrollRequest> validator,
        IEnrollmentSender sender,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(error => ToCamelCase(error.PropertyName))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray());

            return Results.ValidationProblem(errors);
        }

        var trackingId = Guid.NewGuid();
        await sender.SendAsync(new EnrollStudentCommand
        {
            TrackingId = trackingId,
            Name = request.Name!.Trim(),
            Email = request.Email!.Trim(),
            Program = request.Program!.Trim()
        }, ProcessingModes.IsDelayed(request.Processing), cancellationToken);

        return Results.Accepted($"/enrollments/{trackingId}", new EnrollmentAccepted(trackingId));
    }

    private static async Task<IResult> GetAsync(
        Guid trackingId,
        EnrollmentsDbContext db,
        CancellationToken cancellationToken)
    {
        var row = await db.Enrollments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TrackingId == trackingId, cancellationToken);

        if (row is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToResponse(row));
    }

    private static async Task<IResult> ListAsync(
        string? email,
        EnrollmentsDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Enrollments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = email.Trim().ToLower();
            query = query.Where(row => row.Email.ToLower() == normalized);
        }

        var rows = await query
            .OrderByDescending(row => row.CreatedUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return Results.Ok(rows.Select(ToResponse).ToArray());
    }

    private static EnrollmentStatusResponse ToResponse(EnrollmentRecord row) =>
        new(row.TrackingId, row.Status, row.Name, row.Email, row.Program, row.Message);

    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0]))
        {
            return propertyName;
        }

        return char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
    }
}

public interface IEnrollmentSender
{
    Task SendAsync(EnrollStudentCommand message, bool delay, CancellationToken cancellationToken);
}

public sealed record EnrollRequest(string? Name, string? Email, string? Program, string? Processing);

public sealed record EnrollmentAccepted(Guid TrackingId);

public sealed record EnrollmentStatusResponse(
    Guid TrackingId,
    string Status,
    string Name,
    string Email,
    string Program,
    string? Message);
