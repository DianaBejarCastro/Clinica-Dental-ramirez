using System.Buffers.Binary;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace ClinicaRamirez.Api.Application.ClinicAdministration;

public sealed class ClinicLogoService(AppDbContext db, IdentitySupport support)
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public async Task<ClinicLogoResponse> Upload(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) throw new ApiException(400, "El archivo está vacío.");
        if (file.Length > MaxBytes) throw new ApiException(413, "El logo no puede superar 5 MB.");
        if (file.ContentType is not ("image/png" or "image/jpeg" or "image/webp"))
            throw new ApiException(400, "Solo se admiten PNG, JPG o WebP.");
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var type = Validate(bytes);
        if (type != file.ContentType) throw new ApiException(400, "El contenido no corresponde al tipo de imagen declarado.");
        var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length == 0) name = "logo";
        if (name.Length > 255) name = name[..255];
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await support.LockAdministration();
        var clinic = await db.Clinics.SingleOrDefaultAsync(ct) ?? throw new ApiException(404, "Clínica inexistente.");
        clinic.LogoData = bytes; clinic.LogoContentType = type; clinic.LogoFileName = name;
        clinic.LogoSize = bytes.LongLength; clinic.LogoUpdatedAt = DateTime.UtcNow;
        support.Audit("clinic_logo_uploaded", "Clinic", clinic.Id, metadata: new { contentType = type, size = bytes.LongLength });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(type, name, bytes.LongLength, clinic.LogoUpdatedAt.Value);
    }
    internal static string Validate(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null) throw new ApiException(400, "El archivo no contiene una imagen válida.");
        var type = codec.EncodedFormat switch {
            SKEncodedImageFormat.Png => "image/png", SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Webp => "image/webp", _ => throw new ApiException(400, "Formato de imagen no permitido.")
        };
        var complete = type switch {
            "image/png" => bytes.AsSpan().EndsWith(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }),
            "image/jpeg" => bytes.AsSpan().EndsWith(new byte[] { 255, 217 }),
            _ => bytes.Length >= 12 && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)) == bytes.Length - 8
        };
        if (!complete) throw new ApiException(400, "Imagen incompleta o inválida.");
        var info = codec.Info;
        if (info.Width < 1 || info.Height < 1 || info.Width > 8192 || info.Height > 8192 || (long)info.Width * info.Height > 16_000_000)
            throw new ApiException(400, "El logo excede el límite de 8192 píxeles por lado o 16 megapíxeles.");
        if (codec.FrameCount > 1) throw new ApiException(400, "Utiliza un logo estático, sin animación.");
        // Decodificar verifica los píxeles; solo se persisten los bytes originales, sin re-encode.
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (bitmap.GetPixels() == IntPtr.Zero || codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new ApiException(400, "El contenido de la imagen es inválido o está incompleto.");
        return type;
    }
    public async Task<(byte[] Bytes, string ContentType, DateTime UpdatedAt)> Get(CancellationToken ct)
    {
        var logo = await db.Clinics.AsNoTracking().Select(c => new { c.LogoData, c.LogoContentType, c.LogoUpdatedAt }).SingleOrDefaultAsync(ct);
        if (logo?.LogoData is null) throw new ApiException(404, "La clínica no tiene logo.");
        return (logo.LogoData, logo.LogoContentType!, logo.LogoUpdatedAt!.Value);
    }
    public async Task Remove(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await support.LockAdministration();
        var clinic = await db.Clinics.SingleOrDefaultAsync(ct) ?? throw new ApiException(404, "Clínica inexistente.");
        clinic.LogoData = null; clinic.LogoContentType = null; clinic.LogoFileName = null; clinic.LogoSize = null; clinic.LogoUpdatedAt = null;
        support.Audit("clinic_logo_removed", "Clinic", clinic.Id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
