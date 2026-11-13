using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace ClinicaRamirez.Api.DTOs.Patients;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatientBookingRequest(Guid FeaturedDoctorId, DateOnly Date, TimeOnly StartTime, Guid CenterId, [MaxLength(1000)] string? Reason);
public sealed record PatientBookingSlot(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, Guid CenterId, string CenterName);
public sealed record PatientBookingDoctor(Guid FeaturedDoctorId, string Name, string Specialty, DateTime? PhotoVersion);
public sealed record PatientBookingAvailability(PatientBookingDoctor Doctor, PatientAppointmentResponse? ExistingAppointment, PatientBookingSlot[] Slots);
