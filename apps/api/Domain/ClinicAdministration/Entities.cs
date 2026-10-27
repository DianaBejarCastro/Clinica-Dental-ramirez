using ClinicaRamirez.Api.Domain.Identity;

namespace ClinicaRamirez.Api.Domain.ClinicAdministration;

public interface IAdministrativeRecord
{
    DateTime UpdatedAt { get; set; }
}

public sealed class Clinic : IAdministrativeRecord
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public Guid Id { get; set; } = SingletonId;
    public string Name { get; set; } = "";
    public string? LegalName { get; set; }
    public string? Description { get; set; }
    // Legado: conservado por compatibilidad; la carga de logos usa almacenamiento binario.
    public string? LogoUrl { get; set; }
    public byte[]? LogoData { get; set; }
    public string? LogoContentType { get; set; }
    public string? LogoFileName { get; set; }
    public long? LogoSize { get; set; }
    public DateTime? LogoUpdatedAt { get; set; }
    public string? Phone { get; set; }
    public string? Whatsapp { get; set; }
    public string? Email { get; set; }
    public string? TaxId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Center : IAdministrativeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClinicId { get; set; }
    public Clinic Clinic { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    public string Address { get; set; } = "";
    public string? City { get; set; }
    public string? Department { get; set; }
    public string Country { get; set; } = "Bolivia";
    public string? Phone { get; set; }
    public string? Whatsapp { get; set; }
    public string? Email { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsMain { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class DoctorProfile : IAdministrativeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string ProfessionalLicense { get; set; } = "";
    public string? Bio { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<DoctorSpecialty> DoctorSpecialties { get; set; } = [];
    public List<DoctorCenter> DoctorCenters { get; set; } = [];
}

public sealed class Specialty : IAdministrativeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<DoctorSpecialty> DoctorSpecialties { get; set; } = [];
}

public sealed class DoctorSpecialty
{
    public Guid DoctorId { get; set; }
    public DoctorProfile Doctor { get; set; } = null!;
    public Guid SpecialtyId { get; set; }
    public Specialty Specialty { get; set; } = null!;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

public sealed class DoctorCenter
{
    public Guid DoctorId { get; set; }
    public DoctorProfile Doctor { get; set; } = null!;
    public Guid CenterId { get; set; }
    public Center Center { get; set; } = null!;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

public sealed class DoctorSchedule : IAdministrativeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public DoctorProfile Doctor { get; set; } = null!;
    public Guid CenterId { get; set; }
    public Center Center { get; set; } = null!;
    public DoctorCenter DoctorCenter { get; set; } = null!;
    // ISO weekday: Monday=1 ... Sunday=7. TimeOnly is local wall time at the center.
    public int DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool HasBreak { get; set; }
    public TimeOnly? BreakStart { get; set; }
    public TimeOnly? BreakEnd { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
