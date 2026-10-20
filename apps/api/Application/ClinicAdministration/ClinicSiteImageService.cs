using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace ClinicaRamirez.Api.Application.ClinicAdministration;

public sealed class ClinicSiteImageService(AppDbContext db, IdentitySupport support)
{
    public const int MaxBytes = 10 * 1024 * 1024;
    private IQueryable<ClinicSiteImage> Images => db.ClinicSiteImages.Where(x => x.ImageType == ClinicSiteImageType.HERO);
    private static HeroImageResponse Dto(ClinicSiteImage x) => new(x.ContentType, x.FileName, x.Width, x.Height, x.Size, x.FocalPointX, x.FocalPointY, x.AltText, x.UpdatedAt);

    public async Task<HeroImageResponse> Upload(HeroUploadRequest request, CancellationToken ct)
    {
        var file = request.File;
        if (file.Length == 0) throw new ApiException(400, "El archivo está vacío.");
        if (file.Length > MaxBytes) throw new ApiException(413, "La portada no puede superar 10 MB.");
        if (file.ContentType is not ("image/png" or "image/jpeg" or "image/webp")) throw new ApiException(400, "Solo se admiten PNG, JPG o WebP.");
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.Info.Width != 1916 || codec.Info.Height != 821)
            throw new ApiException(400, "La portada debe ser una imagen válida de exactamente 1916 × 821 px.");
        var type = ClinicLogoService.Validate(bytes);
        if (type != file.ContentType) throw new ApiException(400, "El contenido no corresponde al tipo declarado.");
        var name = new string(Path.GetFileName(file.FileName.Replace('\\', '/')).Where(c => !char.IsControl(c)).ToArray());
        if (name.Length > 255) name = name[..255];
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await support.LockAdministration();
        var clinicId = await db.Clinics.Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct) ?? throw new ApiException(404, "Clínica inexistente.");
        var image = await Images.SingleOrDefaultAsync(x => x.ClinicId == clinicId, ct);
        var created = image is null;
        if (image is null) { image = new() { ClinicId = clinicId }; db.ClinicSiteImages.Add(image); }
        image.ImageData = bytes; image.ContentType = type; image.FileName = name;
        image.Width = 1916; image.Height = 821; image.Size = bytes.LongLength;
        image.FocalPointX = request.FocalPointX; image.FocalPointY = request.FocalPointY;
        image.AltText = string.IsNullOrWhiteSpace(request.AltText) ? null : request.AltText.Trim();
        image.IsActive = true; image.UpdatedAt = DateTime.UtcNow;
        support.Audit(created ? "clinic_hero_uploaded" : "clinic_hero_updated", "ClinicSiteImage", image.Id,
            metadata: new { assetId = image.Id, contentType = type, width = image.Width, height = image.Height, size = image.Size });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Dto(image);
    }
    public async Task<HeroImageResponse> GetAdmin(CancellationToken ct)
        => Dto(await Images.AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new ApiException(404, "No hay portada."));
    public async Task<HeroPublicMetadata> GetMetadata(CancellationToken ct)
    {
        var result = await (from x in Images.AsNoTracking() join c in db.Clinics on x.ClinicId equals c.Id
            where x.IsActive select new HeroPublicMetadata(x.FocalPointX, x.FocalPointY, x.AltText ?? ("Portada de " + c.Name), x.UpdatedAt)).SingleOrDefaultAsync(ct);
        return result ?? throw new ApiException(404, "No hay portada.");
    }
    public async Task<(byte[] Bytes, string ContentType, DateTime UpdatedAt)> Get(CancellationToken ct)
    {
        var x = await Images.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.ImageData, x.ContentType, x.UpdatedAt }).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(404, "No hay portada.");
        return (x.ImageData, x.ContentType, x.UpdatedAt);
    }
    public async Task Remove(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await support.LockAdministration();
        var image = await Images.SingleOrDefaultAsync(ct);
        if (image is not null) {
            db.ClinicSiteImages.Remove(image);
            support.Audit("clinic_hero_deleted", "ClinicSiteImage", image.Id, metadata: new { assetId = image.Id });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
