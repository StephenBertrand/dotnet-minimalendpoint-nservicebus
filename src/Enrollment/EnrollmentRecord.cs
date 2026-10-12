namespace Enrollment;

public sealed class EnrollmentRecord
{
    public Guid TrackingId { get; set; }

    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    public string Program { get; set; } = "";

    public string Status { get; set; } = EnrollmentStatus.InProgress;

    public string? Message { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? CompletedUtc { get; set; }
}

public static class EnrollmentStatus
{
    public const string InProgress = "inprogress";

    public const string Completed = "completed";

    public const string Rejected = "rejected";

    public const string DuplicateEmailMessage = "An enrollment for this email already exists.";
}

public static class EnrollmentFeatures
{
    public const string StopProcessing = "StopProcessing";
}

public static class ProcessingModes
{
    public const string Immediate = "immediate";

    public const string Delayed = "delayed";

    public static readonly TimeSpan Delay = TimeSpan.FromMinutes(1);

    public static bool IsDelayed(string? processing) =>
        string.Equals(processing, Delayed, StringComparison.OrdinalIgnoreCase);
}
