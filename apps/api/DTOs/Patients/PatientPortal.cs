namespace ClinicaRamirez.Api.DTOs.Patients;
public sealed record PatientPage<T>(T[] Items, int Total, int Page, int PageSize, string? ClinicName = null, PatientAppointmentResponse? NextAppointment = null);
public sealed record PatientFinanceSummary(decimal TotalEstimated, decimal TotalPaid, decimal PendingBalance, int PendingTreatments);
public sealed record PatientTreatment(Guid Id, string Title, DateTime Date, decimal Total, decimal Paid, decimal Balance, string Status);
public sealed record PatientPayment(Guid Id, string Concept, DateTime PaidAt, decimal Amount, string? Method, string? Reference, string Status);
public sealed record PatientPayments(PatientFinanceSummary Summary, PatientPage<PatientTreatment> Treatments, PatientPage<PatientPayment> Payments, string ClinicName);
public sealed record PatientDashboardResponse(string ClinicName, PatientAccountResponse Account, PatientFinanceSummary Finance, PatientAppointmentResponse? NextAppointment, PatientAppointmentResponse[] RecentAppointments, PatientPayment[] RecentPayments);
