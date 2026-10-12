namespace Api.Features.Todos;

public sealed record TodoResponse(int Id, string Title, bool IsComplete, DateTimeOffset CreatedUtc);

public sealed record CreateTodoRequest(string Title);

public sealed record UpdateTodoRequest(string Title, bool IsComplete);
