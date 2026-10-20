using ClinicaRamirez.Api.DTOs.Patients;
namespace ClinicaRamirez.Api.Application.Patients;

public sealed class PatientRegistrationService(PatientsService patients)
{
    public async Task Register(PatientRegistrationRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        PatientAccountService.Validate(request);
        await patients.Create(new PatientCreateRequest {
            FirstName = request.FirstName, LastName = request.LastName, Ci = request.Ci,
            BirthDate = request.BirthDate, Gender = request.Gender, Phone = request.Phone,
            Address = request.Address, EmergencyContactName = request.EmergencyContactName,
            EmergencyContactPhone = request.EmergencyContactPhone, BloodType = request.BloodType,
            Account = new() { Email = request.Email!, Password = request.Password, ConfirmPassword = request.ConfirmPassword }
        }, selfRegistration: true);
    }
}
