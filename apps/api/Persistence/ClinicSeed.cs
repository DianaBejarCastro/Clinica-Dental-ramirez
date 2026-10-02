using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Persistence;

public sealed class ClinicSeed(AppDbContext db, IdentitySupport support, ILogger<ClinicSeed> logger)
{
    public static readonly (string Code, string Name)[] InitialSpecialties = [
        ("GENERAL_DENTISTRY", "Odontología general"), ("ORTHODONTICS", "Ortodoncia"),
        ("PEDIATRIC_DENTISTRY", "Odontopediatría"), ("ENDODONTICS", "Endodoncia"),
        ("PERIODONTICS", "Periodoncia"), ("ORAL_MAXILLOFACIAL_SURGERY", "Cirugía oral y maxilofacial"),
        ("PROSTHODONTICS", "Prostodoncia / Rehabilitación oral"), ("ORAL_PATHOLOGY", "Patología oral"),
        ("ORAL_MAXILLOFACIAL_RADIOLOGY", "Radiología oral y maxilofacial"),
        ("PUBLIC_HEALTH_DENTISTRY", "Salud pública odontológica"), ("IMPLANTOLOGY", "Implantología"),
        ("AESTHETIC_DENTISTRY", "Estética dental")
    ];
    public async Task Run()
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        if (!await db.Clinics.AnyAsync())
            db.Clinics.Add(new Clinic { Name = "Clínica Dental RAMÍREZ" });
        var specialties = await db.Specialties.ToArrayAsync();
        foreach (var (code, name) in InitialSpecialties)
        {
            if (specialties.Any(x => x.Code == code)) continue;
            // Preserve administrator edits; avoid assigning a catalog code to an unrelated record.
            if (specialties.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogWarning("Especialidad inicial {Code} omitida: nombre ocupado por otro código.", code);
                continue;
            }
            var specialty = new Specialty { Code = code, Name = name };
            db.Specialties.Add(specialty);
            support.Audit("specialty_created", "Specialty", specialty.Id, metadata: new { source = "seed" });
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        logger.LogInformation("Seed de clínica y especialidades completado.");
    }
}
