using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;
namespace ClinicaRamirez.Api.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
            connection = new NpgsqlConnectionStringBuilder {
                Host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost",
                Port = int.Parse(Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5434"),
                Database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "clinica_ramirez",
                Username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "clinica_user",
                Password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD"),
                GssEncryptionMode = GssEncryptionMode.Disable
            }.ConnectionString;
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    }
}
