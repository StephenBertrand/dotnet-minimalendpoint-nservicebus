using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.Features.Todos;

/// <summary>
/// Used only by <c>dotnet ef migrations</c>. Runtime connections come from Aspire.
/// </summary>
public sealed class TodosDbContextFactory : IDesignTimeDbContextFactory<TodosDbContext>
{
    public TodosDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TodosDbContext>()
            .UseSqlServer("Server=127.0.0.1,1433;Database=todos;User Id=sa;Password=Local_only_not_used_at_runtime;TrustServerCertificate=True;Encrypt=True")
            .Options;

        return new TodosDbContext(options);
    }
}
