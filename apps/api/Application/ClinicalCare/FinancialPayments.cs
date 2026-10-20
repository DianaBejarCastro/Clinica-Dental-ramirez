using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.ClinicalCare;
using ClinicaRamirez.Api.DTOs.ClinicalCare;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.ClinicalCare;

// Call inside a transaction holding advisory lock 820030, shared by clinical and cashier writes.
public sealed class FinancialPayments(AppDbContext db, IdentitySupport support)
{
    public async Task Record(Guid planId, PaymentInput input)
    {
        if(db.Database.CurrentTransaction is null) throw new InvalidOperationException("Payment requires a transaction.");
        var actor=support.Actor??throw new ApiException(401,"Inicia sesión.");
        var account=await db.Set<TreatmentFinancialAccount>().SingleOrDefaultAsync(x=>x.TreatmentPlanId==planId)
            ??throw new ApiException(404,"Cuenta financiera no disponible.");
        if(input.Amount<=0 || input.Amount>9999999999.99m || decimal.Round(input.Amount,2)!=input.Amount)
            throw new ApiException(400,"Importe inválido; usa un valor positivo con hasta dos decimales.");
        if(input.IdempotencyKey==Guid.Empty)throw new ApiException(400,"Falta clave de idempotencia.");
        var existing=await db.Set<TreatmentPayment>().SingleOrDefaultAsync(x=>x.FinancialAccountId==account.Id&&x.IdempotencyKey==input.IdempotencyKey);
        if(existing!=null){if(existing.Amount!=input.Amount||existing.PaymentMethod!=input.PaymentMethod||existing.Reference!=input.Reference||existing.Notes!=input.Notes)throw new ApiException(409,"La clave ya pertenece a otro pago.");return;}
        var paid=await db.Set<TreatmentPayment>().Where(x=>x.FinancialAccountId==account.Id).SumAsync(x=>x.Amount);
        if(paid+input.Amount>account.AgreedTotal)throw new ApiException(409,"El pago supera el saldo pendiente. Actualiza el tratamiento.");
        var payment=new TreatmentPayment{FinancialAccountId=account.Id,IdempotencyKey=input.IdempotencyKey,Amount=input.Amount,PaymentMethod=input.PaymentMethod,Reference=input.Reference,Notes=input.Notes,RecordedByUserId=actor};
        db.Add(payment);support.Audit("payment_recorded","TreatmentPayment",payment.Id,metadata:new{PaymentId=payment.Id,TreatmentPlanId=planId,FinancialAccountId=account.Id,RecordedByUserId=actor,Amount=input.Amount});
    }
}
