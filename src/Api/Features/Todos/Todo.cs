namespace Api.Features.Todos;

public sealed class Todo
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public bool IsComplete { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
