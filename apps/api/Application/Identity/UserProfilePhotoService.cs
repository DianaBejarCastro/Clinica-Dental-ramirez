using ClinicaRamirez.Api.Application.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
namespace ClinicaRamirez.Api.Application.Identity;

public sealed record ProfilePhotoMetadata(Guid UserId, int Width, int Height, string MimeType, long FileSizeBytes, DateTime UpdatedAt);
public sealed class UserProfilePhotoService(AppDbContext db, IdentitySupport support)
{
    public const int MaxUploadBytes = 10 * 1024 * 1024;
    private static readonly string[] StaffRoles = ["ADMINISTRATOR", "SECRETARY", "DOCTOR", "CASHIER", "DENTAL_ASSISTANT"];
    private async Task Authorize(Guid userId, bool write, CancellationToken ct)
    {
        var actor = support.Actor ?? throw new ApiException(401, "Sesión requerida.");
        var roles = db.UserRoles.Where(x => x.UserId == actor && x.Role.IsActive);
        if (!await roles.AnyAsync(x => x.Role.Code == "ADMINISTRATOR", ct) ||
            !await roles.SelectMany(x => x.Role.RolePermissions).AnyAsync(x => x.Permission.Code == (write ? "users.update" : "users.view"), ct))
            throw new ApiException(403, "No tienes permiso para administrar fotos de personal.");
        if (!await db.Users.AnyAsync(x => x.Id == userId, ct)) throw new ApiException(404, "Usuario inexistente.");
        if (!await db.UserRoles.AnyAsync(x => x.UserId == userId && x.Role.IsActive && StaffRoles.Contains(x.Role.Code), ct))
            throw new ApiException(400, "Las fotos están disponibles únicamente para personal.");
    }
    public async Task<ProfilePhotoMetadata?> Metadata(Guid userId, CancellationToken ct)
    {
        await Authorize(userId, false, ct);
        return await db.UserProfilePhotos.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new ProfilePhotoMetadata(x.UserId, x.Width, x.Height, x.MimeType, x.FileSizeBytes, x.UpdatedAt)).SingleOrDefaultAsync(ct);
    }
    public async Task<(byte[] Bytes, string Type, DateTime Version)> Content(Guid userId, CancellationToken ct)
    {
        await Authorize(userId, false, ct);
        var photo = await db.UserProfilePhotos.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct)
            ?? throw new ApiException(404, "Sin foto de perfil.");
        return (photo.ImageData, photo.MimeType, photo.UpdatedAt);
    }
    public async Task<ProfilePhotoMetadata> Upload(Guid userId, IFormFile file, CancellationToken ct)
    {
        await Authorize(userId, true, ct);
        if (file.Length <= 0 || file.Length > MaxUploadBytes) throw new ApiException(400, "Selecciona una imagen de hasta 10 MB.");
        if (file.ContentType is not ("image/jpeg" or "image/png" or "image/webp")) throw new ApiException(400, "Formato no permitido.");
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.Info.Width != 1080 || codec.Info.Height != 1350)
            throw new ApiException(400, "La foto debe medir exactamente 1080 × 1350 px.");
        if (ClinicLogoService.Validate(bytes) != file.ContentType) throw new ApiException(400, "El contenido no coincide con el tipo de imagen.");
        using var bitmap = SKBitmap.Decode(bytes);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, 90);
        var optimized = encoded.ToArray();
        if (optimized.Length > 5 * 1024 * 1024) throw new ApiException(400, "La foto procesada supera 5 MB.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await support.LockAdministration();
        await Authorize(userId, true, ct);
        var photo = await db.UserProfilePhotos.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        var created = photo is null;
        if (photo is null) { photo = new() { UserId = userId }; db.UserProfilePhotos.Add(photo); }
        photo.ImageData = optimized; photo.MimeType = "image/webp"; photo.Width = 1080; photo.Height = 1350;
        photo.FileSizeBytes = optimized.LongLength; photo.UpdatedAt = DateTime.UtcNow;
        support.Audit(created ? "user_profile_photo_uploaded" : "user_profile_photo_replaced", "User", userId, metadata: new { UserId = userId });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(userId, photo.Width, photo.Height, photo.MimeType, photo.FileSizeBytes, photo.UpdatedAt);
    }
    public async Task Delete(Guid userId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await support.LockAdministration(); await Authorize(userId, true, ct);
        var photo = await db.UserProfilePhotos.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (photo is not null) {
            db.UserProfilePhotos.Remove(photo);
            support.Audit("user_profile_photo_deleted", "User", userId, metadata: new { UserId = userId });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
