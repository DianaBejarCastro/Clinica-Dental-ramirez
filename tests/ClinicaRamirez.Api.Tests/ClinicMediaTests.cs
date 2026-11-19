using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicaRamirez.Api.Application.ClinicAdministration;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ClinicaRamirez.Api.Persistence;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using SkiaSharp;
using Xunit;
namespace ClinicaRamirez.Api.Tests;
public sealed class ClinicMediaTests(IdentityFixture f):IClassFixture<IdentityFixture> {
 [Fact] public async Task RemovingLegacyGalleryEntryPreservesSourceImage(){
  using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
  var clinic=await db.Clinics.FirstAsync();
  using var bitmap=new SKBitmap(1916,821);bitmap.Erase(SKColors.Teal);using var image=SKImage.FromBitmap(bitmap);using var encoded=image.Encode(SKEncodedImageFormat.Png,90);var bytes=encoded.ToArray();
  var source=new ClinicSiteImage{ClinicId=clinic.Id,ImageType=ClinicSiteImageType.HERO,ImageData=bytes,ContentType="image/png",Width=1916,Height=821,Size=bytes.Length};
  db.Set<ClinicSiteImage>().Add(source);await db.SaveChangesAsync();
  var item=new ClinicMediaItem{ClinicId=clinic.Id,MediaType=ClinicMediaType.IMAGE,LegacyImageId=source.Id};
  db.ClinicMediaItems.Add(item);await db.SaveChangesAsync();
  await scope.ServiceProvider.GetRequiredService<ClinicMediaService>().Delete(item.Id,default);
  db.ChangeTracker.Clear();Assert.False(await db.ClinicMediaItems.AnyAsync(x=>x.Id==item.Id));
  Assert.Equal(bytes,(await db.Set<ClinicSiteImage>().SingleAsync(x=>x.Id==source.Id)).ImageData);
 }
 private async Task<HttpClient> Admin(){var c=f.Client();var login=await f.Login(c);c.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken);return c;}
 private static MultipartFormDataContent Form(string type="TIKTOK",string url="https://www.tiktok.com/@clinic/video/1234567890123456789",byte[]? bytes=null,string mime="image/png",string name="photo.png") {
  var form=new MultipartFormDataContent();form.Add(new StringContent(type),"mediaType");form.Add(new StringContent(url),"externalUrl");form.Add(new StringContent("Instalaciones"),"altText");
  if(bytes!=null){var file=new ByteArrayContent(bytes);file.Headers.ContentType=new MediaTypeHeaderValue(mime);form.Add(file,"file",name);}return form;
 }
 [Fact] public async Task LifecycleFilteringVisibilityAndDeletion(){
  using var c=await Admin();using var response=await c.PostAsync("/api/admin/clinic/media",Form());Assert.Equal(HttpStatusCode.OK,response.StatusCode);
  var id=(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  var list=await f.Client().GetFromJsonAsync<JsonElement>("/api/clinic/media?type=TIKTOK");Assert.Contains(list.GetProperty("items").EnumerateArray(),x=>x.GetProperty("id").GetGuid()==id);
  Assert.Equal(HttpStatusCode.NoContent,(await c.PutAsJsonAsync($"/api/admin/clinic/media/{id}",new{title="Video",sortOrder=3,isActive=false})).StatusCode);
  list=await f.Client().GetFromJsonAsync<JsonElement>("/api/clinic/media?type=TIKTOK");Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(),x=>x.GetProperty("id").GetGuid()==id);
  Assert.Equal(HttpStatusCode.NoContent,(await c.DeleteAsync($"/api/admin/clinic/media/{id}")).StatusCode);
 }
 [Theory][InlineData("https://evil.test/@x/video/1234567890123")][InlineData("javascript:alert(1)")][InlineData("https://www.tiktok.com.evil.test/@x/video/1234567890123")][InlineData("https://www.tiktok.com/@x")]
 public async Task RejectsInvalidExternalLinks(string url){using var c=await Admin();Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/api/admin/clinic/media",Form(url:url))).StatusCode);}
 [Fact] public async Task ImagesHaveThumbnailsAndCanBeDeleted(){
  using var bmp=new SKBitmap(200,100);bmp.Erase(SKColors.Teal);using var img=SKImage.FromBitmap(bmp);using var encoded=img.Encode(SKEncodedImageFormat.Png,90);
  using var c=await Admin();var response=await c.PostAsync("/api/admin/clinic/media",Form("IMAGE",bytes:encoded.ToArray()));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
  var id=(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();var thumbnail=await f.Client().GetAsync($"/api/clinic/media/{id}/content?thumbnail=true");Assert.Equal("image/webp",thumbnail.Content.Headers.ContentType!.MediaType);
  Assert.Equal(HttpStatusCode.NoContent,(await c.DeleteAsync($"/api/admin/clinic/media/{id}")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await f.Client().GetAsync($"/api/clinic/media/{id}/content")).StatusCode);
 }
 [Fact] public async Task RejectsFakeAndOversizedImages(){using var c=await Admin();Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/api/admin/clinic/media",Form("IMAGE",bytes:"not an image"u8.ToArray()))).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/api/admin/clinic/media",Form("IMAGE",bytes:new byte[10485761]))).StatusCode);}
 [Fact] public async Task PrivateEndpointsAndPaginationProtected(){var c=f.Client();Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/admin/clinic/media")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await c.PostAsync("/api/admin/clinic/media",Form())).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync("/api/clinic/media?pageSize=1000")).StatusCode);}
 private sealed class Storage:IMediaObjectStorage {public bool Available=>true;public int Puts,Deletes;public Task Put(string key,byte[] bytes,CancellationToken ct){Puts++;return Task.CompletedTask;}public Task Delete(string key,CancellationToken ct){Deletes++;return Task.CompletedTask;}public string PublicUrl(string key)=>"https://media.example/"+key;}
 [Fact] public async Task VideoUsesObjectStorageAndCleanup(){using var scope=f.Services.CreateScope();var store=new Storage();var service=ActivatorUtilities.CreateInstance<ClinicMediaService>(scope.ServiceProvider,store);var bytes=new byte[32];"ftyp"u8.CopyTo(bytes.AsSpan(4));var file=new FormFile(new MemoryStream(bytes),0,bytes.Length,"file","clip.mp4"){Headers=new HeaderDictionary(),ContentType="video/mp4"};var id=await service.Create(new CreateMediaItemRequest{MediaType="VIDEO_UPLOAD",File=file},default);Assert.Equal(1,store.Puts);var content=await service.Content(id,true,false,default);Assert.Null(content.Bytes);Assert.StartsWith("https://media.example/",content.Url);await service.Delete(id,default);Assert.Equal(1,store.Deletes);}
}
