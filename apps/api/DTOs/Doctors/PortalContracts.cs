namespace ClinicaRamirez.Api.DTOs.Doctors;

// Local clinic dates/times, matching the existing Appointment/DoctorSchedule model.
public sealed record DoctorPatient(Guid PatientProfileId, string FirstName, string LastName);
public sealed record DoctorAppointmentLocation(Guid Id, string Name);
public sealed record DoctorAppointment(Guid AppointmentId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    DoctorPatient Patient, DoctorAppointmentLocation Specialty, DoctorAppointmentLocation Center, string? Reason, string Status);
public sealed record DoctorAgenda(DateOnly From, DateOnly To, DoctorAppointment[] Items);
