using System.Linq.Expressions;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.ClinicAdministration;
public sealed class HomeServicesService(AppDbContext db, IdentitySupport support)
{
    private IQueryable<ClinicService> Query => db.ClinicServices.Where(x => x.ClinicId == Clinic.SingletonId);
    private static readonly Expression<Func<ClinicService, ClinicServiceResponse>> Projection = x =>
        new(x.Id, x.Name, x.ShortDescription, x.VisualVariant, x.DisplayOrder, x.IsActive, x.IsPublic, x.SpecialtyId);
    public Task<ClinicServiceResponse[]> List(CancellationToken ct) => Query.AsNoTracking()
        .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).Select(Projection).ToArrayAsync(ct);
    public Task<PublicClinicServiceResponse[]> PublicList(CancellationToken ct) => Query.AsNoTracking()
        .Where(x => x.IsActive && x.IsPublic && db.Clinics.Any(c => c.Id == x.ClinicId && c.IsActive))
        .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
        .Select(x => new PublicClinicServiceResponse(x.Id, x.Name, x.ShortDescription, x.VisualVariant)).ToArrayAsync(ct);
    public async Task<ClinicServiceResponse> Save(Guid? id, ClinicServiceRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? "";
        var description = request.ShortDescription?.Trim() ?? "";
        if (name.Length is 0 or > 120 || description.Length > 300 || request.DisplayOrder < 0 ||
            request.VisualVariant is not ("TEAL" or "CYAN" or "NAVY" or "AQUA" or "MINT" or "DARK_TEAL"))
            throw new ApiException(400, "Revisa el nombre, descripción, color y orden.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await support.LockAdministration();
        if (!await db.Clinics.AnyAsync(x => x.Id == Clinic.SingletonId, ct))
            throw new ApiException(404, "Clínica inexistente.");
        if (request.SpecialtyId.HasValue && !await db.Specialties.AnyAsync(x => x.Id == request.SpecialtyId, ct))
            throw new ApiException(400, "La especialidad relacionada no existe.");
        var item = id.HasValue ? await Find(id.Value, ct) : new ClinicService { ClinicId = Clinic.SingletonId };
        item.Name = name; item.ShortDescription = description; item.VisualVariant = request.VisualVariant;
        item.DisplayOrder = request.DisplayOrder; item.IsActive = request.IsActive;
        item.IsPublic = request.IsPublic; item.SpecialtyId = request.SpecialtyId;
        if (!id.HasValue) db.ClinicServices.Add(item);
        support.Audit(id.HasValue ? "clinic_service_updated" : "clinic_service_created", "ClinicService", item.Id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await Query.Where(x => x.Id == item.Id).Select(Projection).SingleAsync(ct);
    }
    private async Task<ClinicService> Find(Guid id, CancellationToken ct) =>
        await Query.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApiException(404, "Servicio inexistente.");
    public async Task Status(Guid id, bool active, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await support.LockAdministration();
        var item = await Find(id, ct); item.IsActive = active;
        support.Audit("clinic_service_status_changed", "ClinicService", id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
