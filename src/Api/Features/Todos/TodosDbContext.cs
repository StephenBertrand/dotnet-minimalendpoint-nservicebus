using Microsoft.EntityFrameworkCore;

namespace Api.Features.Todos;

public sealed class TodosDbContext(DbContextOptions<TodosDbContext> options) : DbContext(options)
{
    public DbSet<Todo> Todos => Set<Todo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var todo = modelBuilder.Entity<Todo>();
        todo.ToTable("Todos");
        todo.HasKey(t => t.Id);
        todo.Property(t => t.Id).UseIdentityColumn();
        todo.Property(t => t.Title).HasMaxLength(200).IsRequired();
        todo.Property(t => t.IsComplete).IsRequired();
        todo.Property(t => t.CreatedUtc).IsRequired();
    }
}
