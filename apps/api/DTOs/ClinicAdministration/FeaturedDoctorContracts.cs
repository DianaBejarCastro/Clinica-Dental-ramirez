using System.ComponentModel.DataAnnotations;
namespace ClinicaRamirez.Api.DTOs.ClinicAdministration;
public sealed record FeaturedDoctorRequest(Guid DoctorProfileId, [Range(0, int.MaxValue)] int DisplayOrder);
public sealed record FeaturedDoctorOrderRequest([Range(0, int.MaxValue)] int DisplayOrder);
public sealed record AvailableDoctorResponse(Guid DoctorProfileId, Guid UserId, string FirstName, string LastName,
    string ProfessionalLicense, string[] Specialties, DateTime? PhotoVersion);
public sealed record FeaturedDoctorResponse(Guid Id, Guid DoctorProfileId, Guid UserId, string FirstName, string LastName,
    string ProfessionalLicense, string[] Specialties, DateTime? PhotoVersion, int DisplayOrder, bool IsEligible);
public sealed record PublicTeamResponse(Guid Id, string FirstName, string LastName, string[] Specialties, DateTime? PhotoVersion);
