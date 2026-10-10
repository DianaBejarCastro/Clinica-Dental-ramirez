using System.Linq.Expressions;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.ClinicAdministration;
public sealed class ClinicSocialNetworkService(AppDbContext db, IdentitySupport support)
{
    public const int MaxBytes = 2 * 1024 * 1024;
    private IQueryable<ClinicSocialNetwork> Query => db.ClinicSocialNetworks.Where(x => x.ClinicId == Clinic.SingletonId);
    private static readonly Expression<Func<ClinicSocialNetwork, ClinicSocialNetworkResponse>> Projection = x =>
        new(x.Id, x.Name, x.Url, x.LogoData != null, x.LogoContentType, x.LogoFileName, x.LogoSize, x.DisplayOrder, x.IsActive, x.CreatedAt, x.UpdatedAt);
    public Task<ClinicSocialNetworkResponse[]> List(CancellationToken ct) => Query.AsNoTracking().OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).Select(Projection).ToArrayAsync(ct);
    public Task<PublicClinicSocialNetworkResponse[]> PublicList(CancellationToken ct) => Query.AsNoTracking().Where(x => x.IsActive)
        .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).Select(x => new PublicClinicSocialNetworkResponse(x.Id, x.Name, x.Url, x.LogoData != null)).ToArrayAsync(ct);
    public async Task<ClinicSocialNetworkResponse> Get(Guid id, CancellationToken ct) => await Query.AsNoTracking().Where(x => x.Id == id).Select(Projection).SingleOrDefaultAsync(ct)
        ?? throw new ApiException(404, "Red social inexistente.");
    private async Task<ClinicSocialNetwork> Find(Guid id, CancellationToken ct) => await Query.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ApiException(404, "Red social inexistente.");
    public async Task<ClinicSocialNetworkResponse> Save(Guid? id, ClinicSocialNetworkCreateRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? ""; var url = request.Url?.Trim() ?? "";
        if (name.Length is 0 or > 100 || url.Length is 0 or > 1000 || request.DisplayOrder < 0)
            throw new ApiException(400, "Nombre, URL y orden de visualización no válidos.");
        if (url.Any(char.IsWhiteSpace) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(uri.Host) || uri.UserInfo.Length > 0)
            throw new ApiException(400, "Utiliza una URL http:// o https:// válida.");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await support.LockAdministration();
        if (!await db.Clinics.AnyAsync(x => x.Id == Clinic.SingletonId, ct)) throw new ApiException(404, "Clínica inexistente.");
        var item = id.HasValue ? await Find(id.Value, ct) : new ClinicSocialNetwork { ClinicId = Clinic.SingletonId };
        if (await Query.AnyAsync(x => x.Id != item.Id && x.Url == url, ct)) throw new ApiException(409, "Esta URL ya está registrada en la clínica.");
        item.Name = name; item.Url = url; item.DisplayOrder = request.DisplayOrder;
        if (id.HasValue && item.IsActive != request.IsActive) support.Audit("clinic_social_network_status_changed", "ClinicSocialNetwork", item.Id);
        item.IsActive = request.IsActive;
        if (!id.HasValue) db.ClinicSocialNetworks.Add(item);
        support.Audit(id.HasValue ? "clinic_social_network_updated" : "clinic_social_network_created", "ClinicSocialNetwork", item.Id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await Get(item.Id, ct);
    }
    public async Task<ClinicSocialNetworkResponse> Status(Guid id, bool active, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await support.LockAdministration();
        var item = await Find(id, ct); item.IsActive = active;
        support.Audit("clinic_social_network_status_changed", "ClinicSocialNetwork", id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await Get(id, ct);
    }
    public async Task<ClinicSocialNetworkResponse> Upload(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) throw new ApiException(400, "El archivo está vacío.");
        if (file.Length > MaxBytes) throw new ApiException(413, "El logo no puede superar 2 MB.");
        if (file.ContentType is not ("image/png" or "image/jpeg" or "image/webp")) throw new ApiException(400, "Solo se admiten PNG, JPG o WebP.");
        using var buffer = new MemoryStream(); await file.CopyToAsync(buffer, ct); var bytes = buffer.ToArray();
        var type = ClinicLogoService.Validate(bytes);
        if (type != file.ContentType) throw new ApiException(400, "El contenido no corresponde al tipo declarado.");
        var name = new string(Path.GetFileName(file.FileName.Replace('\\', '/')).Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length == 0) name = "logo"; if (name.Length > 255) name = name[..255];
        await using var tx = await db.Database.BeginTransactionAsync(ct); await support.LockAdministration();
        var item = await Find(id, ct); item.LogoData = bytes; item.LogoContentType = type; item.LogoFileName = name; item.LogoSize = bytes.LongLength;
        support.Audit("clinic_social_network_logo_uploaded", "ClinicSocialNetwork", id, metadata: new { contentType = type, size = bytes.LongLength });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await Get(id, ct);
    }
    public async Task RemoveLogo(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await support.LockAdministration();
        var item = await Find(id, ct); item.LogoData = null; item.LogoContentType = null; item.LogoFileName = null; item.LogoSize = null;
        support.Audit("clinic_social_network_logo_removed", "ClinicSocialNetwork", id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<(byte[] Bytes, string ContentType)> Logo(Guid id, CancellationToken ct)
    {
        var item = await Query.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.LogoData, x.LogoContentType }).SingleOrDefaultAsync(ct);
        if (item?.LogoData is null) throw new ApiException(404, "Logo inexistente.");
        return (item.LogoData, item.LogoContentType!);
    }
}
