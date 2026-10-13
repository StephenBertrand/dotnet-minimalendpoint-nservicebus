namespace Enrollment;

public sealed class OrderRecord
{
    public Guid OrderId { get; set; }

    public Guid TrackingId { get; set; }

    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    public string Summary { get; set; } = "";

    public DateTimeOffset CreatedUtc { get; set; }
}
