using ClinicaRamirez.Api.Application.ClinicalCare;
using ClinicaRamirez.Api.Domain.ClinicalCare;
using ClinicaRamirez.Api.Domain.Appointments;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.Patients;
public sealed class PatientPortalService(AppDbContext db, IdentitySupport support)
{
    internal async Task<Guid> OwnProfile(CancellationToken ct)
    {
        var userId = support.Actor ?? throw new ApiException(401, "Inicia sesión para continuar.");
        if (!await db.UserRoles.AnyAsync(x => x.UserId == userId && x.User.IsActive && x.Role.IsActive && x.Role.Code == "PATIENT", ct))
            throw new ApiException(403, "Este portal es exclusivo para pacientes.");
        return await db.PatientProfiles.Where(x => x.UserId == userId && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(403, "No hay un perfil de paciente activo vinculado a esta cuenta.");
    }
    public async Task<PatientAccountResponse> Me(CancellationToken ct)
    {
        var id = await OwnProfile(ct);
        return await db.PatientProfiles.AsNoTracking().Where(x => x.Id == id).Select(x => new PatientAccountResponse(
            x.User.FirstName, x.User.LastName, x.User.DocumentType, x.User.DocumentNumber, x.User.DocumentCountryCode, x.User.BirthDate, x.User.Gender, x.User.Phone, x.User.Email)).SingleAsync(ct);
    }
    private IQueryable<PatientAppointmentResponse> AppointmentRows(IQueryable<Appointment> source) =>
        from a in source
        join d in db.DoctorProfiles on a.DoctorId equals d.Id
        join u in db.Users on d.UserId equals u.Id
        join c in db.Centers on a.CenterId equals c.Id
        join s in db.Specialties on a.SpecialtyId equals s.Id
        select new PatientAppointmentResponse(a.Id,a.AppointmentDate,a.StartTime,a.EndTime,a.Status,u.FirstName+" "+u.LastName,c.Name,s.Name,a.Reason,a.CreatedAt);
    private static DateTime ClinicNow => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,"America/La_Paz");
    private IQueryable<Appointment> FilterAppointments(Guid id,string filter)
    {
        var now=ClinicNow;var date=DateOnly.FromDateTime(now);var time=TimeOnly.FromDateTime(now);
        var q=db.Appointments.AsNoTracking().Where(a=>a.PatientId==id);
        var upcoming=q.Where(a=>(a.AppointmentDate>date || a.AppointmentDate==date && a.EndTime>time) && (a.Status=="SCHEDULED" || a.Status=="CONFIRMED" || a.Status=="IN_PROGRESS"));
        return filter switch {
            "upcoming" => upcoming,
            "history" => q.Where(a=>!upcoming.Select(x=>x.Id).Contains(a.Id)),
            "completed" => q.Where(a=>a.Status=="COMPLETED"),
            "cancelled" => q.Where(a=>a.Status=="CANCELLED"),
            "all" => q,
            _ => throw new ApiException(400,"Filtro de citas inválido.")
        };
    }
    private static void ValidatePage(int page,int size) { if(page<1 || page>100000 || size<1 || size>20) throw new ApiException(400,"Paginación inválida."); }
    public async Task<PatientPage<PatientAppointmentResponse>> AppointmentPage(string filter,int page,int pageSize,CancellationToken ct)
    {
        ValidatePage(page,pageSize);var id=await OwnProfile(ct);var q=FilterAppointments(id,filter);
        var total=await q.CountAsync(ct);
        var ordered=filter=="upcoming" ? q.OrderBy(a=>a.AppointmentDate).ThenBy(a=>a.StartTime).ThenBy(a=>a.Id) : q.OrderByDescending(a=>a.AppointmentDate).ThenByDescending(a=>a.StartTime).ThenBy(a=>a.Id);
        var next=await AppointmentRows(FilterAppointments(id,"upcoming").OrderBy(a=>a.AppointmentDate).ThenBy(a=>a.StartTime).ThenBy(a=>a.Id).Take(1)).SingleOrDefaultAsync(ct);
        return new(await AppointmentRows(ordered.Skip((page-1)*pageSize).Take(pageSize)).ToArrayAsync(ct),total,page,pageSize,await ClinicName(ct),next);
    }
    // Retains the original response shape for older clients, now bounded.
    public async Task<PatientAppointmentResponse[]> Appointments(CancellationToken ct) => (await AppointmentPage("all",1,20,ct)).Items;
    private IQueryable<PatientPayment> PaymentRows(Guid id) =>
        from payment in db.Set<TreatmentPayment>().AsNoTracking()
        join account in db.Set<TreatmentFinancialAccount>() on payment.FinancialAccountId equals account.Id
        join plan in db.Set<TreatmentPlan>() on account.TreatmentPlanId equals plan.Id
        join episode in db.Set<TreatmentEpisode>() on plan.TreatmentEpisodeId equals episode.Id
        where episode.PatientProfileId==id
        orderby payment.PaidAt descending,payment.Id
        select new PatientPayment(payment.Id,episode.Title,payment.PaidAt,payment.Amount,payment.PaymentMethod,payment.Reference,"PAID");
    private async Task<PatientFinanceSummary> Summary(Guid id,CancellationToken ct)
    {
        var q=FinancialAccountRead.Query(db,id);
        if(await q.AnyAsync(x=>x.Paid>x.AgreedTotal,ct)) throw new ApiException(409,"La cuenta requiere revisión financiera: saldo inconsistente.");
        return await q.GroupBy(x=>1).Select(g=>new PatientFinanceSummary(g.Sum(x=>x.AgreedTotal),g.Sum(x=>x.Paid),g.Sum(x=>x.AgreedTotal-x.Paid),g.Count(x=>x.Paid<x.AgreedTotal))).SingleOrDefaultAsync(ct) ?? new(0,0,0,0);
    }
    public async Task<PatientPayments> Payments(int page,int treatmentPage,int pageSize,CancellationToken ct)
    {
        ValidatePage(page,pageSize);ValidatePage(treatmentPage,pageSize);var id=await OwnProfile(ct);
        var summary=await Summary(id,ct);var accounts=FinancialAccountRead.Query(db,id);var payments=PaymentRows(id);
        var totalAccounts=await accounts.CountAsync(ct);var totalPayments=await payments.CountAsync(ct);
        var treatments=await accounts.OrderByDescending(x=>x.StartedAt).ThenBy(x=>x.Plan).Skip((treatmentPage-1)*pageSize).Take(pageSize)
            .Select(x=>new PatientTreatment(x.Plan,x.Title,x.StartedAt,x.AgreedTotal,x.Paid,x.AgreedTotal-x.Paid,x.Paid==x.AgreedTotal?"PAID":"PENDING")).ToArrayAsync(ct);
        return new(summary,new(treatments,totalAccounts,treatmentPage,pageSize),new(await payments.Skip((page-1)*pageSize).Take(pageSize).ToArrayAsync(ct),totalPayments,page,pageSize),await ClinicName(ct));
    }
    private async Task<string> ClinicName(CancellationToken ct) => await db.Clinics.Select(c=>c.Name).FirstOrDefaultAsync(ct) ?? "Clínica";
    public async Task<PatientDashboardResponse> Dashboard(CancellationToken ct)
    {
        var id=await OwnProfile(ct);var account=await Me(ct);
        var clinic=await ClinicName(ct);
        var next=await AppointmentRows(FilterAppointments(id,"upcoming").OrderBy(a=>a.AppointmentDate).ThenBy(a=>a.StartTime).ThenBy(a=>a.Id).Take(1)).SingleOrDefaultAsync(ct);
        var recent=await AppointmentRows(FilterAppointments(id,"history").OrderByDescending(a=>a.AppointmentDate).ThenByDescending(a=>a.StartTime).ThenBy(a=>a.Id).Take(3)).ToArrayAsync(ct);
        return new(clinic,account,await Summary(id,ct),next,recent,await PaymentRows(id).Take(3).ToArrayAsync(ct));
    }
}
