using Api.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Todos;

public sealed class TodosEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/todos")
            .WithTags("Todos");

        group.MapGet("/", ListAsync).WithName("ListTodos");
        group.MapGet("/{id:int}", GetByIdAsync).WithName("GetTodo");
        group.MapPost("/", CreateAsync).WithName("CreateTodo");
        group.MapPut("/{id:int}", UpdateAsync).WithName("UpdateTodo");
        group.MapDelete("/{id:int}", DeleteAsync).WithName("DeleteTodo");
    }

    private static async Task<IResult> ListAsync(TodosDbContext db, CancellationToken cancellationToken)
    {
        var items = await db.Todos
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedUtc)
            .Select(t => new TodoResponse(t.Id, t.Title, t.IsComplete, t.CreatedUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> GetByIdAsync(int id, TodosDbContext db, CancellationToken cancellationToken)
    {
        var todo = await db.Todos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        return todo is null
            ? Results.NotFound()
            : Results.Ok(new TodoResponse(todo.Id, todo.Title, todo.IsComplete, todo.CreatedUtc));
    }

    private static async Task<IResult> CreateAsync(CreateTodoRequest request, TodosDbContext db, CancellationToken cancellationToken)
    {
        if (!TryNormalizeTitle(request.Title, out var title, out var error))
        {
            return Results.ValidationProblem(error);
        }

        var todo = new Todo
        {
            Title = title,
            IsComplete = false,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        db.Todos.Add(todo);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/todos/{todo.Id}", new TodoResponse(todo.Id, todo.Title, todo.IsComplete, todo.CreatedUtc));
    }

    private static async Task<IResult> UpdateAsync(int id, UpdateTodoRequest request, TodosDbContext db, CancellationToken cancellationToken)
    {
        if (!TryNormalizeTitle(request.Title, out var title, out var error))
        {
            return Results.ValidationProblem(error);
        }

        var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (todo is null)
        {
            return Results.NotFound();
        }

        todo.Title = title;
        todo.IsComplete = request.IsComplete;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new TodoResponse(todo.Id, todo.Title, todo.IsComplete, todo.CreatedUtc));
    }

    private static async Task<IResult> DeleteAsync(int id, TodosDbContext db, CancellationToken cancellationToken)
    {
        var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (todo is null)
        {
            return Results.NotFound();
        }

        db.Todos.Remove(todo);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static bool TryNormalizeTitle(string? title, out string normalized, out Dictionary<string, string[]> errors)
    {
        normalized = (title ?? string.Empty).Trim();
        errors = [];

        if (normalized.Length is 0)
        {
            errors["title"] = ["Title is required."];
            return false;
        }

        if (normalized.Length > 200)
        {
            errors["title"] = ["Title must be 200 characters or fewer."];
            return false;
        }

        return true;
    }
}
