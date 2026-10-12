using System.Net;
using System.Net.Http.Json;
using Api.Features.Todos;

namespace Api.Tests;

[Collection(SqlServerCollection.Name)]
public class TodosEndpointsTests
{
    private readonly SqlServerFixture _sql;

    public TodosEndpointsTests(SqlServerFixture sql)
    {
        _sql = sql;
    }

    [Fact]
    public async Task Todos_CrudRoundTrip()
    {
        await using var factory = new SqlApiFactory(_sql.CreateIsolatedConnectionString());
        using var client = factory.CreateClient();

        var empty = await client.GetFromJsonAsync<List<TodoResponse>>("/todos");
        Assert.NotNull(empty);
        Assert.Empty(empty);

        var created = await client.PostAsJsonAsync("/todos", new CreateTodoRequest("Ship sample"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var todo = await created.Content.ReadFromJsonAsync<TodoResponse>();
        Assert.NotNull(todo);
        Assert.True(todo.Id > 0);
        Assert.Equal("Ship sample", todo.Title);
        Assert.False(todo.IsComplete);

        var listed = await client.GetFromJsonAsync<List<TodoResponse>>("/todos");
        Assert.Contains(listed!, t => t.Id == todo.Id);

        var updatedResponse = await client.PutAsJsonAsync($"/todos/{todo.Id}", new UpdateTodoRequest("Ship sample", true));
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<TodoResponse>();
        Assert.True(updated!.IsComplete);

        var deleted = await client.DeleteAsync($"/todos/{todo.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var missing = await client.GetAsync($"/todos/{todo.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Todos_EmptyTitle_IsValidationProblem()
    {
        await using var factory = new SqlApiFactory(_sql.CreateIsolatedConnectionString());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/todos", new CreateTodoRequest("  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
