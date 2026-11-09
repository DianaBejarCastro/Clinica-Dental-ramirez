using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.ClinicAdministration;
public sealed class FeaturedDoctorsService(AppDbContext db, IdentitySupport support)
{
    private IQueryable<DoctorProfile> Eligible => db.DoctorProfiles.Where(d => d.IsActive && d.User.IsActive &&
        d.DoctorCenters.Any(c => c.IsActive && c.Center.IsActive && c.Center.ClinicId == Clinic.SingletonId && c.Center.Clinic.IsActive));
    private IQueryable<ClinicFeaturedDoctor> Selected => db.ClinicFeaturedDoctors.Where(x => x.ClinicId == Clinic.SingletonId && x.IsActive);
    private IQueryable<ClinicFeaturedDoctor> Published => Selected.Where(x => Eligible.Any(d => d.Id == x.DoctorProfileId));
    public Task<AvailableDoctorResponse[]> Available(CancellationToken ct) => Eligible.AsNoTracking().AsSingleQuery()
        .Where(d => !Selected.Any(x => x.DoctorProfileId == d.Id)).OrderBy(d => d.User.FirstName).ThenBy(d => d.Id)
        .Select(d => new AvailableDoctorResponse(d.Id, d.UserId, d.User.FirstName, d.User.LastName, d.ProfessionalLicense,
            d.DoctorSpecialties.Where(s => s.Specialty.IsActive).OrderBy(s => s.Specialty.Name).Select(s => s.Specialty.Name).ToArray(),
            db.UserProfilePhotos.Where(p => p.UserId == d.UserId).Select(p => (DateTime?)p.UpdatedAt).FirstOrDefault())).ToArrayAsync(ct);
    public Task<FeaturedDoctorResponse[]> List(CancellationToken ct) => Selected.AsNoTracking().AsSingleQuery()
        .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
        .Select(x => new FeaturedDoctorResponse(x.Id, x.DoctorProfileId, x.Doctor.UserId, x.Doctor.User.FirstName, x.Doctor.User.LastName,
            x.Doctor.ProfessionalLicense, x.Doctor.DoctorSpecialties.Where(s => s.Specialty.IsActive).OrderBy(s => s.Specialty.Name).Select(s => s.Specialty.Name).ToArray(),
            db.UserProfilePhotos.Where(p => p.UserId == x.Doctor.UserId).Select(p => (DateTime?)p.UpdatedAt).FirstOrDefault(),
            x.DisplayOrder, Eligible.Any(d => d.Id == x.DoctorProfileId))).ToArrayAsync(ct);
    public Task<PublicTeamResponse[]> Public(CancellationToken ct) => Published.AsNoTracking().AsSingleQuery()
        .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
        .Select(x => new PublicTeamResponse(x.Id, x.Doctor.User.FirstName, x.Doctor.User.LastName,
            x.Doctor.DoctorSpecialties.Where(s => s.Specialty.IsActive).OrderBy(s => s.Specialty.Name).Select(s => s.Specialty.Name).ToArray(),
            db.UserProfilePhotos.Where(p => p.UserId == x.Doctor.UserId).Select(p => (DateTime?)p.UpdatedAt).FirstOrDefault())).ToArrayAsync(ct);
    public async Task<Guid> Add(FeaturedDoctorRequest request, CancellationToken ct)
    {
        if (request.DisplayOrder < 0) throw new ApiException(400, "El orden debe ser mayor o igual a cero.");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await support.LockAdministration();
        if (!await Eligible.AnyAsync(d => d.Id == request.DoctorProfileId, ct)) throw new ApiException(400, "Selecciona un doctor activo asignado a esta clínica.");
        var item = await db.ClinicFeaturedDoctors.SingleOrDefaultAsync(x => x.ClinicId == Clinic.SingletonId && x.DoctorProfileId == request.DoctorProfileId, ct);
        if (item?.IsActive == true) throw new ApiException(409, "Este profesional ya está seleccionado.");
        if (item is null) { item = new() { ClinicId = Clinic.SingletonId, DoctorProfileId = request.DoctorProfileId }; db.ClinicFeaturedDoctors.Add(item); }
        item.IsActive = true; item.DisplayOrder = request.DisplayOrder;
        support.Audit("clinic_featured_doctor_added", "ClinicFeaturedDoctor", item.Id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return item.Id;
    }
    public async Task Change(Guid id, int? order, CancellationToken ct)
    {
        if (order < 0) throw new ApiException(400, "El orden debe ser mayor o igual a cero.");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await support.LockAdministration();
        var item = await Selected.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApiException(404, "Profesional no seleccionado.");
        if (order.HasValue) item.DisplayOrder = order.Value; else item.IsActive = false;
        support.Audit(order.HasValue ? "clinic_featured_doctor_reordered" : "clinic_featured_doctor_removed", "ClinicFeaturedDoctor", id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    // Public access is restricted by the live publication query; no arbitrary user photo IDs are exposed.
    public async Task<(byte[] Bytes, string MimeType, DateTime Version)> Photo(Guid id, CancellationToken ct)
    {
        var photo = await (from selection in Published.Where(x => x.Id == id)
            join p in db.UserProfilePhotos on selection.Doctor.UserId equals p.UserId
            select new { p.ImageData, p.MimeType, p.UpdatedAt }).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(404, "Foto no disponible.");
        return (photo.ImageData, photo.MimeType, photo.UpdatedAt);
    }
}
