using System.ComponentModel.DataAnnotations;

namespace ClinicaRamirez.Api.DTOs.Appointments;

public class AppointmentUpdateRequest
{
    public Guid PatientId { get; init; }
    public Guid DoctorId { get; init; }
    public Guid CenterId { get; init; }
    public Guid SpecialtyId { get; init; }
    public DateOnly AppointmentDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    [MaxLength(1000)] public string? Reason { get; init; }
    [MaxLength(4000)] public string? Notes { get; init; }
}
public sealed class AppointmentCreateRequest : AppointmentUpdateRequest;
public sealed record AppointmentStatusRequest([Required] string Status);
public sealed record AppointmentResponse(Guid Id, Guid PatientId, Guid DoctorId, Guid CenterId, Guid SpecialtyId,
    DateOnly AppointmentDate, TimeOnly StartTime, TimeOnly EndTime, string Status, string? Reason, string? Notes,
    Guid? CreatedByUserId, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record AppointmentListResponse(AppointmentResponse[] Items, int Total, int Page, int PageSize);
public sealed record AvailabilitySlot(TimeOnly StartTime, TimeOnly EndTime);
public sealed record AvailabilityResponse(Guid DoctorId, Guid CenterId, Guid SpecialtyId, DateOnly Date,
    int DurationMinutes, AvailabilitySlot[] Slots);
