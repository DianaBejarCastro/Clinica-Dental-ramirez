using System.Text.RegularExpressions;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
namespace ClinicaRamirez.Api.Application.ClinicAdministration;
public sealed class ClinicMediaService(AppDbContext db,IdentitySupport support,IMediaObjectStorage storage,ILogger<ClinicMediaService> logger) {
 private IQueryable<ClinicMediaItem> Query => db.ClinicMediaItems.Where(x=>x.ClinicId==Clinic.SingletonId);
 private IQueryable<ClinicMediaItem> Filter(bool admin,string? type) {
  var q=Query.AsNoTracking();if(!admin)q=q.Where(x=>x.IsActive && db.Clinics.Any(c=>c.Id==x.ClinicId&&c.IsActive));
  if(!string.IsNullOrEmpty(type)){if(!Enum.TryParse<ClinicMediaType>(type,out var t)||!Enum.IsDefined(t))throw new ApiException(400,"Tipo no válido.");q=q.Where(x=>x.MediaType==t);}
  return q.OrderBy(x=>x.SortOrder).ThenBy(x=>x.Id);
 }
 public async Task<object> List(bool admin,string? type,int page,int pageSize,CancellationToken ct){
  if(page<1||page>100000||pageSize<1||pageSize>12)throw new ApiException(400,"Paginación no válida.");
  var q=Filter(admin,type);var total=await q.CountAsync(ct);var slice=q.Skip((page-1)*pageSize).Take(pageSize);
  if(admin)return new MediaPage<AdminMediaItemDto>(await slice.Select(x=>new AdminMediaItemDto(x.Id,x.MediaType.ToString(),x.Title,x.Caption,x.AltText,x.External==null?null:x.External.ExternalUrl,x.Asset!=null?x.Asset.Width:x.LegacyImage!.Width,x.Asset!=null?x.Asset.Height:x.LegacyImage!.Height,x.SortOrder,x.IsActive)).ToArrayAsync(ct),total,page,pageSize,storage.Available);
  return new MediaPage<PublicMediaItemDto>(await slice.Select(x=>new PublicMediaItemDto(x.Id,x.MediaType.ToString(),x.Title,x.Caption,x.AltText,x.External==null?null:x.External.ExternalUrl,x.Asset!=null?x.Asset.Width:x.LegacyImage!.Width,x.Asset!=null?x.Asset.Height:x.LegacyImage!.Height)).ToArrayAsync(ct),total,page,pageSize,storage.Available);
 }
 public static (string Url,string Id) TikTok(string? input){
  if(!Uri.TryCreate(input,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host is not ("www.tiktok.com" or "tiktok.com")||!uri.IsDefaultPort||uri.UserInfo!="")throw new ApiException(400,"Introduce un enlace HTTPS de TikTok válido.");
  var match=Regex.Match(uri.AbsolutePath,@"^/@[A-Za-z0-9_.]+/video/([0-9]{10,25})/?$");
  if(!match.Success)throw new ApiException(400,"Usa el enlace completo de TikTok: @usuario/video/identificador.");
  return ("https://www.tiktok.com"+uri.AbsolutePath.TrimEnd('/'),match.Groups[1].Value);
 }
 private static void Metadata(ClinicMediaItem item,UpdateMediaItemRequest r){item.Title=r.Title?.Trim();item.Caption=r.Caption?.Trim();item.AltText=r.AltText?.Trim();item.SortOrder=r.SortOrder;item.IsActive=r.IsActive;item.UpdatedAt=DateTime.UtcNow;if(item.MediaType==ClinicMediaType.IMAGE&&string.IsNullOrWhiteSpace(item.AltText))throw new ApiException(400,"Describe la imagen en el texto alternativo.");}
 private async Task Place(ClinicMediaItem item,CancellationToken ct){
  var rows=await Query.Where(x=>x.Id!=item.Id).OrderBy(x=>x.SortOrder).ThenBy(x=>x.Id).ToListAsync(ct);
  rows.Insert(Math.Min(item.SortOrder,rows.Count),item);
  for(var i=0;i<rows.Count;i++)rows[i].SortOrder=i;
 }
 public async Task<Guid> Create(CreateMediaItemRequest r,CancellationToken ct){
  if(!Enum.TryParse<ClinicMediaType>(r.MediaType,out var type)||!Enum.IsDefined(type))throw new ApiException(400,"Tipo no válido.");
  var item=new ClinicMediaItem{ClinicId=Clinic.SingletonId,MediaType=type};Metadata(item,r);
  byte[]? video=null;string? key=null;
  if(type==ClinicMediaType.TIKTOK){if(r.File!=null)throw new ApiException(400,"TikTok no admite archivos.");var link=TikTok(r.ExternalUrl);item.External=new(){ExternalUrl=link.Url,ExternalId=link.Id};}
  else {
   var f=r.File??throw new ApiException(400,"Selecciona un archivo.");var max=type==ClinicMediaType.IMAGE?10*1024*1024:50*1024*1024;
   if(f.Length<=0||f.Length>max)throw new ApiException(400,$"El archivo debe tener entre 1 byte y {max/1024/1024} MB.");
   using var buffer=new MemoryStream();await f.CopyToAsync(buffer,ct);var bytes=buffer.ToArray();
   var name=Path.GetFileName(f.FileName.Replace('\\','/'));if(name.Length>255)name=name[^255..];
   item.Asset=new(){OriginalFileName=name,FileSizeBytes=bytes.Length,MimeType=f.ContentType};
   if(type==ClinicMediaType.IMAGE){
    if(!new[]{".png",".jpg",".jpeg",".webp"}.Contains(Path.GetExtension(name).ToLowerInvariant()))throw new ApiException(400,"Extensión de imagen no válida.");
    var mime=ClinicLogoService.Validate(bytes);if(mime!=f.ContentType)throw new ApiException(400,"MIME no coincide con el archivo.");
    using var data=SKData.CreateCopy(bytes);using var codec=SKCodec.Create(data);
    if(codec==null||codec.Info.Width<1||codec.Info.Height<1||(long)codec.Info.Width*codec.Info.Height>40000000)throw new ApiException(400,"Dimensiones de imagen no válidas.");
    using var bitmap=SKBitmap.Decode(codec);if(bitmap==null)throw new ApiException(400,"Imagen dañada.");
    var scale=Math.Min(1d,640d/Math.Max(bitmap.Width,bitmap.Height));
    using var thumb=bitmap.Resize(new SKImageInfo(Math.Max(1,(int)(bitmap.Width*scale)),Math.Max(1,(int)(bitmap.Height*scale))),new SKSamplingOptions(SKFilterMode.Linear));
    using var img=SKImage.FromBitmap(thumb??bitmap);using var encoded=img.Encode(SKEncodedImageFormat.Webp,80);
    item.Asset.ImageData=bytes;item.Asset.ThumbnailData=encoded.ToArray();item.Asset.Width=bitmap.Width;item.Asset.Height=bitmap.Height;
   }else{
    if(!storage.Available)throw new ApiException(503,"Configura almacenamiento durable de videos antes de subir contenido.");
    if(f.ContentType!="video/mp4"||Path.GetExtension(name).ToLowerInvariant()!=".mp4"||bytes.Length<24||System.Text.Encoding.ASCII.GetString(bytes,4,4)!="ftyp")throw new ApiException(400,"Selecciona un archivo MP4 válido.");
    key=item.Id.ToString("N")+".mp4";item.Asset.StorageKey=key;video=bytes;
   }
  }
  await using var tx=await db.Database.BeginTransactionAsync(ct);await support.LockAdministration();
  if(!await db.Clinics.AnyAsync(x=>x.Id==item.ClinicId,ct))throw new ApiException(404,"Clínica inexistente.");
  try{if(key!=null)await storage.Put(key,video!,ct);await Place(item,ct);db.ClinicMediaItems.Add(item);support.Audit("clinic_media_created","ClinicMediaItem",item.Id);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}
  catch{if(key!=null){try{await storage.Delete(key,CancellationToken.None);}catch(Exception cleanup){logger.LogError(cleanup,"Media cleanup requires reconciliation for {StorageKey}",key);}}throw;}
  return item.Id;
 }
 public async Task Update(Guid id,UpdateMediaItemRequest r,CancellationToken ct){await using var tx=await db.Database.BeginTransactionAsync(ct);await support.LockAdministration();var item=await Query.SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ApiException(404,"Contenido inexistente.");Metadata(item,r);await Place(item,ct);support.Audit("clinic_media_updated","ClinicMediaItem",id);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}
 public async Task Delete(Guid id,CancellationToken ct){
  await using var tx=await db.Database.BeginTransactionAsync(ct);await support.LockAdministration();var item=await Query.Include(x=>x.Asset).Include(x=>x.External).SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ApiException(404,"Contenido inexistente.");
  // Hide and commit first: failed object deletion remains retryable, never leaves a public broken link.
  item.IsActive=false;support.Audit("clinic_media_delete_requested","ClinicMediaItem",id);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
  await tx.DisposeAsync();
  await using var deletion=await db.Database.BeginTransactionAsync(ct);await support.LockAdministration();
  if(item.Asset?.StorageKey is string key)await storage.Delete(key,ct);
  if(item.Asset!=null)db.Remove(item.Asset);if(item.External!=null)db.Remove(item.External);db.ClinicMediaItems.Remove(item);var remaining=await Query.Where(x=>x.Id!=id).OrderBy(x=>x.SortOrder).ThenBy(x=>x.Id).ToListAsync(ct);for(var i=0;i<remaining.Count;i++)remaining[i].SortOrder=i;support.Audit("clinic_media_deleted","ClinicMediaItem",id);await db.SaveChangesAsync(ct);await deletion.CommitAsync(ct);
 }
 public async Task<(byte[]? Bytes,string Mime,string? Url)> Content(Guid id,bool admin,bool thumbnail,CancellationToken ct){
  var q=Query.AsNoTracking().Where(x=>x.Id==id);if(!admin)q=q.Where(x=>x.IsActive&&db.Clinics.Any(c=>c.Id==x.ClinicId&&c.IsActive));
  var x=await q.Select(x=>new{Key=x.Asset==null?null:x.Asset.StorageKey,Bytes=x.Asset!=null?(thumbnail?x.Asset.ThumbnailData??x.Asset.ImageData:x.Asset.ImageData):x.LegacyImage!=null?x.LegacyImage.ImageData:null,Mime=x.Asset!=null?(thumbnail&&x.Asset.ThumbnailData!=null?"image/webp":x.Asset.MimeType):x.LegacyImage!=null?x.LegacyImage.ContentType:null}).SingleOrDefaultAsync(ct)??throw new ApiException(404,"Contenido inexistente.");
  if(x.Key is string key)return(null,"video/mp4",storage.PublicUrl(key));
  if(x.Bytes!=null)return(x.Bytes,x.Mime!,null);
  throw new ApiException(404,"Sin archivo local.");
 }
}
