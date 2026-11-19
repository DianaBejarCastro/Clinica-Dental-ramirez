using System.Net.Http.Headers;
using ClinicaRamirez.Api.Application.Identity;
namespace ClinicaRamirez.Api.Application.ClinicAdministration;
// Durable object gateway contract: HTTPS PUT/DELETE per key; public HTTPS GET with Range support.
public interface IMediaObjectStorage {
 bool Available { get; }
 Task Put(string key,byte[] data,CancellationToken ct);
 Task Delete(string key,CancellationToken ct);
 string PublicUrl(string key);
}
public sealed class MediaObjectStorage(IConfiguration config,IHttpClientFactory clients) : IMediaObjectStorage {
 private string? Endpoint => config["MediaStorage:Endpoint"];
 private string? PublicBase => config["MediaStorage:PublicBaseUrl"];
 public bool Available => Uri.TryCreate(Endpoint,UriKind.Absolute,out var e)&&e.Scheme=="https"&&Uri.TryCreate(PublicBase,UriKind.Absolute,out var p)&&p.Scheme=="https"&&!string.IsNullOrWhiteSpace(config["MediaStorage:Token"]);
 public string PublicUrl(string key) { Ensure();return PublicBase!.TrimEnd('/')+"/"+Uri.EscapeDataString(key); }
 private void Ensure(){if(!Available)throw new ApiException(503,"Configura almacenamiento durable de videos antes de subir contenido.");}
 private async Task Send(HttpMethod method,string key,byte[]? bytes,CancellationToken ct){
  Ensure();using var request=new HttpRequestMessage(method,Endpoint!.TrimEnd('/')+"/"+Uri.EscapeDataString(key));
  request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",config["MediaStorage:Token"]);
  if(bytes!=null){request.Content=new ByteArrayContent(bytes);request.Content.Headers.ContentType=new MediaTypeHeaderValue("video/mp4");}
  using var response=await clients.CreateClient().SendAsync(request,ct);if(method==HttpMethod.Delete&&response.StatusCode==System.Net.HttpStatusCode.NotFound)return;response.EnsureSuccessStatusCode();
 }
 public Task Put(string key,byte[] data,CancellationToken ct)=>Send(HttpMethod.Put,key,data,ct);
 public Task Delete(string key,CancellationToken ct)=>Send(HttpMethod.Delete,key,null,ct);
}
