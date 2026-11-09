namespace ClinicaRamirez.Api.DTOs.Doctors;

public sealed record DoctorPatientItem(Guid Id, string FirstName, string LastName, string? DocumentType,
    string? DocumentNumber, string? DocumentCountryCode, string? Phone);
public sealed record DoctorPatientsPage(DoctorPatientItem[] Items, int Total, int Page, int PageSize);
