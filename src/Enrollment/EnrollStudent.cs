namespace Enrollment;

public sealed class EnrollStudent
{
    public Guid TrackingId { get; set; }

    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    public string Program { get; set; } = "";
}
