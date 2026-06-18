using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Zootact.Infrastructure.Data;

public sealed class ZootactDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ZootactDbContext>
{
    public ZootactDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSQL")
            ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Set ConnectionStrings__PostgreSQL or POSTGRES_CONNECTION_STRING before creating a design-time DbContext.");

        var optionsBuilder = new DbContextOptionsBuilder<ZootactDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new ZootactDbContext(optionsBuilder.Options);
    }

}
