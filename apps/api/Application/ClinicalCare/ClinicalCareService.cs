using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.ClinicalCare;
using ClinicaRamirez.Api.Domain.Appointments;
using ClinicaRamirez.Api.DTOs;
using ClinicaRamirez.Api.DTOs.ClinicalCare;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.ClinicalCare;
public sealed class ClinicalCareService(AppDbContext db,IdentitySupport support,FinancialPayments payments)
{
    private Guid Actor => support.Actor ?? throw new ApiException(401,"Inicia sesión.");
    private async Task<Guid> Doctor()
    {
        var actor=Actor;
        if(!await db.UserRoles.AnyAsync(x=>x.UserId==actor&&x.User.IsActive&&x.Role.IsActive&&x.Role.Code=="DOCTOR")) throw new ApiException(403,"Se requiere el rol DOCTOR activo.");
        return await db.DoctorProfiles.Where(x=>x.UserId==actor&&x.IsActive).Select(x=>(Guid?)x.Id).SingleOrDefaultAsync() ?? throw new ApiException(403,"No hay perfil profesional activo.");
    }
    private async Task<Appointment> Own(Guid id)
    {
        var doctor=await Doctor();
        return await db.Appointments.SingleOrDefaultAsync(x=>x.Id==id&&x.DoctorId==doctor) ?? throw new ApiException(404,"Cita no disponible.");
    }
    private Task<bool> Can(string permission) => db.UserRoles.AnyAsync(x=>x.UserId==Actor&&x.Role.IsActive&&x.Role.RolePermissions.Any(p=>p.Permission.Code==permission));
    private async Task<ClinicalEncounter> Encounter(Guid appointmentId) => await db.Set<ClinicalEncounter>().SingleOrDefaultAsync(x=>x.AppointmentId==appointmentId) ?? throw new ApiException(404,"Inicia la consulta primero.");
    private async Task Write(Guid appointmentId,Func<Appointment,Task> action)
    {
        await using var tx=await db.Database.BeginTransactionAsync();
        // One clinical write lock orders start/sign/reviews/plan/payment operations across API instances.
        // Deliberately independent of session/administrative locks; no distributed infrastructure required.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(820030)");
        var appointment=await Own(appointmentId);await action(appointment);await db.SaveChangesAsync();await tx.CommitAsync();
    }
    private static string Text(string? text,string field,int max=8000)
    { if(string.IsNullOrWhiteSpace(text)||text.Trim().Length>max) throw new ApiException(400,$"{field}: valor obligatorio o demasiado largo.");return text.Trim(); }
    private static void Money(decimal amount,bool positive=false)
    {if(amount<(positive?0.01m:0m)||amount>9999999999.99m||decimal.Round(amount,2)!=amount)throw new ApiException(400,"Importe inválido; usa hasta dos decimales.");}
    private Task<PatientMedicalHistoryReview?> Latest(Guid patient) => db.Set<PatientMedicalHistoryReview>().Where(x=>x.PatientProfileId==patient).OrderByDescending(x=>x.ReviewedAt).ThenByDescending(x=>x.Id).FirstOrDefaultAsync();
    private async Task<PatientMedicalHistoryReview> Review(Guid patient,ReviewInput input)
    {
        var latest=await Latest(patient);
        if(input.ExpectedReviewId!=latest?.Id)throw new ApiException(409,"Los antecedentes cambiaron. Vuelve a cargar la consulta.");
        if(input.NoChanges&&latest==null)throw new ApiException(400,"Aún no hay antecedentes registrados para confirmar sin cambios.");
        if(input.NoKnownAllergies&&input.Allergies.Any(x=>x.IsCurrentlyActive))throw new ApiException(400,"No confirmes ausencia de alergias si registraste una alergia activa.");
        if(input.NoChanges&&(input.Allergies.Length+input.Conditions.Length+input.Medications.Length>0||input.NoKnownAllergies))throw new ApiException(400,"Sin cambios debe referenciar el estado previo, no enviar entradas nuevas.");
        var review=new PatientMedicalHistoryReview{PatientProfileId=patient,ReviewedByUserId=Actor,StateReviewId=input.NoChanges?latest!.StateReviewId??latest.Id:null,NoKnownAllergies=!input.NoChanges&&input.NoKnownAllergies,Notes=input.Notes};
        db.Add(review);
        if(!input.NoChanges){
            db.AddRange(input.Allergies.Select(x=>new PatientAllergyEntry{MedicalHistoryReviewId=review.Id,Name=Text(x.Name,"Alergia",200),Reaction=x.Reaction,Severity=x.Severity,IsCurrentlyActive=x.IsCurrentlyActive}));
            db.AddRange(input.Conditions.Select(x=>new PatientConditionEntry{MedicalHistoryReviewId=review.Id,ConditionName=Text(x.ConditionName,"Enfermedad",200),Details=x.Details,IsCurrentlyActive=x.IsCurrentlyActive}));
            db.AddRange(input.Medications.Select(x=>new PatientMedicationEntry{MedicalHistoryReviewId=review.Id,MedicationName=Text(x.MedicationName,"Medicamento",200),Dose=x.Dose,Frequency=x.Frequency,Reason=x.Reason,IsCurrentlyActive=x.IsCurrentlyActive}));
        }
        support.Audit(input.NoChanges?"medical_history_reviewed":"medical_history_updated","PatientMedicalHistoryReview",review.Id);
        return review;
    }
    public Task Start(Guid appointmentId,StartEncounterRequest input) => Write(appointmentId,async a=>{
        if(await db.Set<ClinicalEncounter>().AnyAsync(x=>x.AppointmentId==a.Id))return; // retry opens the same encounter
        if(a.Status is not ("SCHEDULED" or "CONFIRMED" or "IN_PROGRESS"))throw new ApiException(409,"El estado de la cita no permite iniciar atención.");
        if(!await db.PatientProfiles.AnyAsync(x=>x.Id==a.PatientId&&x.IsActive&&x.User.IsActive))throw new ApiException(409,"Paciente inactivo.");
        TreatmentEpisode episode;
        if(input.TreatmentEpisodeId is Guid id){
            episode=await db.Set<TreatmentEpisode>().SingleOrDefaultAsync(x=>x.Id==id&&x.PatientProfileId==a.PatientId&&x.PrimaryDoctorProfileId==a.DoctorId&&x.Status=="OPEN") ?? throw new ApiException(404,"Tratamiento abierto no disponible.");
            if(input.Anamnesis!=null)throw new ApiException(400,"La anamnesis inicial ya está registrada.");
        } else {
            var n=input.Anamnesis??throw new ApiException(400,"Un tratamiento nuevo requiere anamnesis inicial.");
            episode=new(){PatientProfileId=a.PatientId,PrimaryDoctorProfileId=a.DoctorId,Title=Text(input.Title,"Título",200)};db.Add(episode);
            db.Add(new TreatmentAnamnesis{TreatmentEpisodeId=episode.Id,ChiefComplaint=Text(n.ChiefComplaint,"Motivo"),PainSince=Text(n.PainSince,"Evolución del dolor"),IsPainConstant=n.IsPainConstant,ColdSensitivity=n.ColdSensitivity,HeatSensitivity=n.HeatSensitivity,PainWhenChewing=n.PainWhenChewing,WakesFromPain=n.WakesFromPain,PainMedicationTaken=n.PainMedicationTaken,PainIntensity=n.PainIntensity,InitialObservations=n.InitialObservations,RecordedByUserId=Actor});
            support.Audit("treatment_episode_created","TreatmentEpisode",episode.Id);
        }
        var review=await Review(a.PatientId,input.Review);
        var encounter=new ClinicalEncounter{AppointmentId=a.Id,TreatmentEpisodeId=episode.Id,PatientProfileId=a.PatientId,DoctorProfileId=a.DoctorId,MedicalHistoryReviewId=review.Id};
        db.Add(encounter);db.Add(new SoapNote{ClinicalEncounterId=encounter.Id});support.Audit("clinical_encounter_started","ClinicalEncounter",encounter.Id);
    });
    public Task UpdateHistory(Guid appointmentId,ReviewInput input)=>Write(appointmentId,async a=>{
        var e=await Encounter(a.Id);if(e.Status!="OPEN")throw new ApiException(409,"La sesión está cerrada.");
        var review=await Review(a.PatientId,input);e.MedicalHistoryReviewId=review.Id;e.UpdatedAt=DateTime.UtcNow;
    });
    public Task SaveSoap(Guid appointmentId,SoapInput input)=>Write(appointmentId,async a=>{
        var encounter=await Encounter(a.Id);var note=await db.Set<SoapNote>().SingleAsync(x=>x.ClinicalEncounterId==encounter.Id);
        if(note.Status=="SIGNED")throw new ApiException(409,"La nota está firmada; utiliza una adenda.");
        if(note.Revision!=input.Revision)throw new ApiException(409,"La nota cambió. Recarga antes de guardar.");
        note.Subjective=input.Subjective??"";note.Objective=input.Objective??"";note.Assessment=input.Assessment??"";note.Plan=input.Plan??"";note.Revision++;note.UpdatedAt=DateTime.UtcNow;
        if(input.Sign){Text(note.Subjective,"Subjetivo");Text(note.Objective,"Objetivo");Text(note.Assessment,"Evaluación");Text(note.Plan,"Plan");note.Status="SIGNED";note.SignedAt=DateTime.UtcNow;note.SignedByUserId=Actor;encounter.Status="COMPLETED";encounter.EndedAt=DateTime.UtcNow;encounter.UpdatedAt=DateTime.UtcNow;}
        support.Audit(input.Sign?"soap_note_signed":"soap_note_saved","SoapNote",note.Id);
    });
    public Task Addendum(Guid appointmentId,AddendumInput input)=>Write(appointmentId,async a=>{
        var e=await Encounter(a.Id);var note=await db.Set<SoapNote>().SingleAsync(x=>x.ClinicalEncounterId==e.Id);
        if(note.Status!="SIGNED")throw new ApiException(409,"La adenda requiere una nota firmada.");
        var addendum=new SoapAddendum{OriginalEntryId=note.Id,Reason=Text(input.Reason,"Motivo"),Content=Text(input.Content,"Corrección"),CreatedByUserId=Actor};db.Add(addendum);support.Audit("soap_note_addendum_created","SoapAddendum",addendum.Id);
    });
    private async Task<TreatmentPlan> Plan(Guid appointmentId){var e=await Encounter(appointmentId);return await db.Set<TreatmentPlan>().SingleOrDefaultAsync(x=>x.TreatmentEpisodeId==e.TreatmentEpisodeId)??throw new ApiException(404,"Plan no registrado.");}
    public Task CreatePlan(Guid appointmentId,PlanInput input)=>Write(appointmentId,async a=>{
        var e=await Encounter(a.Id);if(await db.Set<TreatmentPlan>().AnyAsync(x=>x.TreatmentEpisodeId==e.TreatmentEpisodeId))throw new ApiException(409,"El tratamiento ya tiene un plan.");
        var plan=new TreatmentPlan{TreatmentEpisodeId=e.TreatmentEpisodeId,Title=Text(input.Title,"Título",200)};db.Add(plan);support.Audit("treatment_plan_created","TreatmentPlan",plan.Id);
    });
    public Task AddProcedure(Guid appointmentId,ProcedureInput input)=>Write(appointmentId,async a=>{
        var plan=await Plan(a.Id);if(plan.Status is not ("DRAFT" or "PROPOSED"))throw new ApiException(409,"Los precios y procedimientos del plan aceptado se conservan como snapshot.");
        Money(input.UnitPrice);Money(input.UnitPrice*input.Quantity);
        if(input.ToothNumber is int tooth && !(tooth/10 is >=1 and <=4 && tooth%10 is >=1 and <=8 || tooth/10 is >=5 and <=8 && tooth%10 is >=1 and <=5))throw new ApiException(400,"Número dental FDI inválido.");
        db.Add(new TreatmentPlanItem{TreatmentPlanId=plan.Id,ProcedureName=Text(input.ProcedureName,"Procedimiento",200),Description=input.Description,ToothNumber=input.ToothNumber,ToothSurface=input.ToothSurface,UnitPrice=input.UnitPrice,Quantity=input.Quantity,DisplayOrder=input.DisplayOrder});plan.UpdatedAt=DateTime.UtcNow;support.Audit("treatment_plan_updated","TreatmentPlan",plan.Id);
    });
    public Task PlanStatus(Guid appointmentId,ClinicalStatusInput input)=>Write(appointmentId,async a=>{
        var plan=await Plan(a.Id);if(plan.Status==input.Status)return;
        var allowed=(plan.Status,input.Status) is ("DRAFT","PROPOSED") or ("PROPOSED","ACCEPTED") or ("ACCEPTED","IN_PROGRESS") or ("IN_PROGRESS","COMPLETED") or ("DRAFT","CANCELLED") or ("PROPOSED","CANCELLED");
        if(!allowed)throw new ApiException(409,"Transición de plan no permitida.");
        var items=await db.Set<TreatmentPlanItem>().Where(x=>x.TreatmentPlanId==plan.Id).ToArrayAsync();if(items.Length==0&&input.Status!="CANCELLED")throw new ApiException(400,"Agrega procedimientos al plan.");
        if(input.Status=="ACCEPTED"){var total=items.Sum(x=>x.UnitPrice*x.Quantity);Money(total);plan.AcceptedAt=DateTime.UtcNow;db.Add(new TreatmentFinancialAccount{TreatmentPlanId=plan.Id,AgreedTotal=total});}
        plan.Status=input.Status;plan.UpdatedAt=DateTime.UtcNow;
        foreach(var item in items){item.Status=input.Status;item.UpdatedAt=DateTime.UtcNow;}
        if(input.Status=="COMPLETED"){var episode=await db.Set<TreatmentEpisode>().SingleAsync(x=>x.Id==plan.TreatmentEpisodeId);episode.Status="COMPLETED";episode.CompletedAt=DateTime.UtcNow;episode.UpdatedAt=DateTime.UtcNow;}
        support.Audit("treatment_plan_updated","TreatmentPlan",plan.Id);
    });
    public Task Payment(Guid appointmentId,PaymentInput input)=>Write(appointmentId,async a=>{
        var plan=await Plan(a.Id);
        if(!await db.Set<TreatmentFinancialAccount>().AnyAsync(x=>x.TreatmentPlanId==plan.Id))throw new ApiException(409,"Primero acepta el plan.");
        await payments.Record(plan.Id,input);
    });
    private async Task<EpisodeDto> EpisodeDto(TreatmentEpisode e){var n=await db.Set<TreatmentAnamnesis>().AsNoTracking().SingleOrDefaultAsync(x=>x.TreatmentEpisodeId==e.Id);return new(e.Id,e.Title,e.Status,e.StartedAt,n==null?null:new(n.ChiefComplaint,n.PainSince,n.IsPainConstant,n.ColdSensitivity,n.HeatSensitivity,n.PainWhenChewing,n.WakesFromPain,n.PainMedicationTaken,n.PainIntensity,n.InitialObservations),n?.RecordedAt);}
    private async Task<ReviewDto> ReviewDto(PatientMedicalHistoryReview r){var state=r.StateReviewId??r.Id;var source=r.StateReviewId==null?r:await db.Set<PatientMedicalHistoryReview>().AsNoTracking().SingleAsync(x=>x.Id==state);return new(r.Id,r.StateReviewId,r.ReviewedAt,source.NoKnownAllergies,r.Notes,
        await db.Set<PatientAllergyEntry>().Where(x=>x.MedicalHistoryReviewId==state).Select(x=>new AllergyInput(x.Name,x.Reaction,x.Severity,x.IsCurrentlyActive)).ToArrayAsync(),
        await db.Set<PatientConditionEntry>().Where(x=>x.MedicalHistoryReviewId==state).Select(x=>new ConditionInput(x.ConditionName,x.Details,x.IsCurrentlyActive)).ToArrayAsync(),
        await db.Set<PatientMedicationEntry>().Where(x=>x.MedicalHistoryReviewId==state).Select(x=>new MedicationInput(x.MedicationName,x.Dose,x.Frequency,x.Reason,x.IsCurrentlyActive)).ToArrayAsync());}
    private async Task<PlanDto?> PlanDto(Guid episode){var p=await db.Set<TreatmentPlan>().AsNoTracking().SingleOrDefaultAsync(x=>x.TreatmentEpisodeId==episode);if(p==null)return null;
        var items=await db.Set<TreatmentPlanItem>().Where(x=>x.TreatmentPlanId==p.Id).OrderBy(x=>x.DisplayOrder).ThenBy(x=>x.CreatedAt).Select(x=>new ProcedureDto(x.Id,x.ProcedureName,x.Description,x.ToothNumber,x.ToothSurface,x.UnitPrice,x.Quantity,x.Status,x.DisplayOrder)).ToArrayAsync();
        FinanceDto? finance=null;if(await Can("payments.view")){var account=await db.Set<TreatmentFinancialAccount>().SingleOrDefaultAsync(x=>x.TreatmentPlanId==p.Id);if(account!=null){var payments=await db.Set<TreatmentPayment>().Where(x=>x.FinancialAccountId==account.Id).OrderBy(x=>x.PaidAt).Select(x=>new PaymentDto(x.Id,x.Amount,x.PaidAt,x.PaymentMethod,x.Reference)).ToArrayAsync();var paid=payments.Sum(x=>x.Amount);finance=new(account.Id,account.AgreedTotal,paid,account.AgreedTotal-paid,paid>=account.AgreedTotal?"PAID":"PENDING",payments);}}
        return new(p.Id,p.Title,p.Status,items.Sum(x=>x.UnitPrice*x.Quantity),items,finance);}
    public async Task<ClinicalContext> Get(Guid id)
    {
        var a=await Own(id);var patient=await db.PatientProfiles.AsNoTracking().Include(x=>x.User).SingleAsync(x=>x.Id==a.PatientId);var u=patient.User;var today=LocalBirthDateAttribute.Today;int? age=u.BirthDate is DateOnly birth?today.Year-birth.Year-(birth.AddYears(today.Year-birth.Year)>today?1:0):null;
        var e=await db.Set<ClinicalEncounter>().AsNoTracking().SingleOrDefaultAsync(x=>x.AppointmentId==id);EncounterDto? encounter=null;
        if(e!=null){var n=await db.Set<SoapNote>().AsNoTracking().SingleAsync(x=>x.ClinicalEncounterId==e.Id);encounter=new(e.Id,e.TreatmentEpisodeId,e.Status,e.StartedAt,new(n.Id,n.Subjective,n.Objective,n.Assessment,n.Plan,n.Status,n.Revision,n.SignedAt,n.SignedByUserId));}
        var episodes=await db.Set<TreatmentEpisode>().AsNoTracking().Where(x=>x.PatientProfileId==a.PatientId&&x.PrimaryDoctorProfileId==a.DoctorId).OrderByDescending(x=>x.StartedAt).ToArrayAsync();var episodeDtos=new List<EpisodeDto>();foreach(var ep in episodes)episodeDtos.Add(await EpisodeDto(ep));
        var latest=await Latest(a.PatientId);
        return new(new(patient.Id,u.FirstName,u.LastName,age,u.Gender,u.Ci,u.Phone,u.Email,patient.BloodType),new(a.Id,a.AppointmentDate,a.StartTime,a.EndTime,a.Reason,await db.Centers.Where(x=>x.Id==a.CenterId).Select(x=>x.Name).SingleAsync(),a.Status),encounter,episodeDtos.ToArray(),latest==null?null:await ReviewDto(latest),e==null||!await Can("treatments.view")?null:await PlanDto(e.TreatmentEpisodeId));
    }
    public async Task<ClinicalHistory> History(Guid appointmentId)
    {
        var a=await Own(appointmentId);var reviews=await db.Set<PatientMedicalHistoryReview>().AsNoTracking().Where(x=>x.PatientProfileId==a.PatientId).OrderByDescending(x=>x.ReviewedAt).ToArrayAsync();var reviewDtos=new List<ReviewDto>();foreach(var r in reviews)reviewDtos.Add(await ReviewDto(r));
        var episodes=await db.Set<TreatmentEpisode>().AsNoTracking().Where(x=>x.PatientProfileId==a.PatientId).ToArrayAsync();var eps=new List<EpisodeDto>();foreach(var e in episodes)eps.Add(await EpisodeDto(e));var ids=episodes.Select(x=>x.Id).ToArray();
        var encounters=await db.Set<ClinicalEncounter>().AsNoTracking().Where(x=>x.PatientProfileId==a.PatientId).ToArrayAsync();var encounterIds=encounters.Select(x=>x.Id).ToArray();var notes=await db.Set<SoapNote>().Where(x=>encounterIds.Contains(x.ClinicalEncounterId)&&x.Status=="SIGNED").ToArrayAsync();var noteIds=notes.Select(x=>x.Id).ToArray();
        var events=new List<TimelineDto>();events.AddRange(reviews.Select(x=>new TimelineDto(x.Id,"medical",x.ReviewedAt,x.StateReviewId==null?"Antecedentes actualizados":"Antecedentes revisados sin cambios",null)));events.AddRange(episodes.Select(x=>new TimelineDto(x.Id,"treatment",x.StartedAt,x.Title,x.Status)));events.AddRange(encounters.Select(x=>new TimelineDto(x.Id,"session",x.StartedAt,"Sesión clínica",x.Status)));events.AddRange(notes.Select(x=>new TimelineDto(x.Id,"soap",x.SignedAt!.Value,"SOAP firmado",$"S: {x.Subjective}\nO: {x.Objective}\nA: {x.Assessment}\nP: {x.Plan}")));
        events.AddRange((await db.Set<SoapAddendum>().Where(x=>noteIds.Contains(x.OriginalEntryId)).ToArrayAsync()).Select(x=>new TimelineDto(x.Id,"soap",x.CreatedAt,"Adenda: "+x.Reason,x.Content)));
        if(await Can("treatments.view")){var plans=await db.Set<TreatmentPlan>().Where(x=>ids.Contains(x.TreatmentEpisodeId)).ToArrayAsync();var planIds=plans.Select(x=>x.Id).ToArray();events.AddRange(plans.Select(x=>new TimelineDto(x.Id,"plan",x.CreatedAt,x.Title,x.Status)));events.AddRange((await db.Set<TreatmentPlanItem>().Where(x=>planIds.Contains(x.TreatmentPlanId)).ToArrayAsync()).Select(x=>new TimelineDto(x.Id,"procedure",x.CreatedAt,x.ProcedureName,$"{x.Quantity} × {x.UnitPrice:0.00} Bs · {x.Status}")));
            if(await Can("payments.view")){var accounts=await db.Set<TreatmentFinancialAccount>().Where(x=>planIds.Contains(x.TreatmentPlanId)).Select(x=>x.Id).ToArrayAsync();events.AddRange((await db.Set<TreatmentPayment>().Where(x=>accounts.Contains(x.FinancialAccountId)).ToArrayAsync()).Select(x=>new TimelineDto(x.Id,"payment",x.PaidAt,"Pago",$"{x.Amount:0.00} Bs")));}}
        return new(reviewDtos.ToArray(),eps.ToArray(),events.OrderByDescending(x=>x.At).ThenBy(x=>x.Id).ToArray());
    }
}
