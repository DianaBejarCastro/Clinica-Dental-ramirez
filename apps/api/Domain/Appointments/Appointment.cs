namespace ClinicaRamirez.Api.Domain.Appointments;

public static class AppointmentStatuses
{
    public const string Scheduled = "SCHEDULED";
    public const string Cancelled = "CANCELLED";
    public const string NoShow = "NO_SHOW";
    public static bool IsValid(string? value) => value is Scheduled or "CONFIRMED" or "IN_PROGRESS" or "COMPLETED" or Cancelled or NoShow;
    public static bool Blocks(string value) => value is not Cancelled and not NoShow;
}

public sealed class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid CenterId { get; set; }
    public Guid SpecialtyId { get; set; }
    // Fecha y horas locales, como DoctorSchedule; timestamps de auditoría en UTC.
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Status { get; set; } = AppointmentStatuses.Scheduled;
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
