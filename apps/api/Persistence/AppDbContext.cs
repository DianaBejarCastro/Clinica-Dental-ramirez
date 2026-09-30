using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Los módulos y sus configuraciones se incorporarán en fases posteriores.
}
