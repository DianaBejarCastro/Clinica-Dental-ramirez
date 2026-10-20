using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace ClinicaRamirez.Api.DTOs.Cashier;
public sealed record CashierPatient(Guid PatientId,string FirstName,string LastName,string? Ci,string? Phone,string? Email);
public sealed record CashierTreatment(Guid TreatmentPlanId,Guid TreatmentEpisodeId,Guid PatientProfileId,string PatientName,string? PatientCi,string? PatientPhone,int? PatientAge,string TreatmentTitle,string DoctorName,DateTime StartedAt,decimal AgreedTotal,decimal PaidTotal,decimal PendingBalance,string PaymentStatus);
public sealed record CashierProcedure(string ProcedureName,decimal UnitPrice,int Quantity,decimal Subtotal,string Status);
public sealed record CashierPayment(Guid Id,Guid TreatmentPlanId,string TreatmentTitle,string DoctorName,decimal Amount,DateTime PaidAt,string? PaymentMethod,string? Reference,string? Notes,string RecordedByName,string Status);
public sealed record CashierDetail(CashierTreatment Treatment,CashierProcedure[] Procedures,CashierPayment[] Payments);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CashierPaymentRequest(Guid IdempotencyKey,decimal Amount,
    [Required,RegularExpression("^(CASH|TRANSFER|CARD|QR|OTHER)$")] string PaymentMethod,
    [MaxLength(200)] string? Reference,[MaxLength(2000)] string? Notes);
