using System.Net;
using System.Text.Json;
using ClinicaRamirez.Api.Application.ClinicAdministration;
using ClinicaRamirez.Api.Application.Identity;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ClinicaRamirez.Api.Tests;
public sealed class LocationSearchTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct); }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(handler, false); }
    private static LocationSearchService Service(HttpMessageHandler handler) => new(new Factory(handler), new ConfigurationBuilder().Build());
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };
    [Theory][InlineData(null)][InlineData("")][InlineData("ab")]
    public async Task ConsultaRequeridaYMinimo(string? query)
    {
        using var service = Service(new Handler((_, _) => throw new Exception("No debe consultar")));
        Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => service.Search(query, default))).Status);
    }
    [Fact] public async Task LongitudMaxima()
    {
        using var service = Service(new Handler((_, _) => throw new Exception("No debe consultar")));
        Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => service.Search(new string('x', 251), default))).Status);
    }
    [Fact] public async Task RespuestaMaximoCincoIdentificacionParametrosYCache()
    {
        var calls = 0;
        using var handler = new Handler((request, _) => {
            calls++; Assert.Contains("format=json&limit=5", request.RequestUri!.Query);
            Assert.Contains("q=Llallagua", request.RequestUri.Query);
            Assert.Contains("ClinicaDentalRamirez", request.Headers.UserAgent.ToString());
            return Task.FromResult(Json(JsonSerializer.Serialize(Enumerable.Range(0, 8).Select(x => new { display_name = "Lugar " + x, lat = "-18.42", lon = "-66.58" }))));
        });
        using var service = Service(handler); var results = await service.Search("Llallagua", default);
        Assert.Equal(5, results.Length); Assert.Equal(-18.42, results[0].Latitude); Assert.Equal("Lugar 0", results[0].DisplayName);
        Assert.Equal(results, await service.Search(" Llallagua ", default)); Assert.Equal(1, calls);
        Assert.Equal(429, (await Assert.ThrowsAsync<ApiException>(() => service.Search("Potosí", default))).Status);
    }
    [Theory][InlineData(429)][InlineData(500)]
    public async Task ErrorExternoSeguro(int status)
    {
        using var service = Service(new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))));
        var error = await Assert.ThrowsAsync<ApiException>(() => service.Search("Consulta sensible", default));
        Assert.Equal(502, error.Status); Assert.DoesNotContain("sensible", error.Message);
    }
    [Fact] public async Task TimeoutExterno()
    {
        using var service = Service(new Handler((_, _) => throw new TaskCanceledException()));
        Assert.Equal(504, (await Assert.ThrowsAsync<ApiException>(() => service.Search("Llallagua", default))).Status);
    }
    [Fact] public async Task JsonInvalido()
    {
        using var service = Service(new Handler((_, _) => Task.FromResult(Json("not json"))));
        Assert.Equal(502, (await Assert.ThrowsAsync<ApiException>(() => service.Search("Llallagua", default))).Status);
    }
    [Fact] public async Task OmiteCoordenadasInvalidas()
    {
        using var service = Service(new Handler((_, _) => Task.FromResult(Json("""[{"display_name":"Lugar","lat":"91","lon":"0"}]"""))));
        Assert.Empty(await service.Search("Llallagua", default));
    }
    [Fact] public async Task SerializaPeticionesConcurrentes()
    {
        var completion = new TaskCompletionSource<HttpResponseMessage>();
        using var service = Service(new Handler((_, _) => completion.Task));
        var first = service.Search("Llallagua", default);
        Assert.Equal(429, (await Assert.ThrowsAsync<ApiException>(() => service.Search("Potosí", default))).Status);
        completion.SetResult(Json("[]")); Assert.Empty(await first);
    }
}

public sealed class LocationSearchEndpointTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    [Fact] public async Task EndpointValidaConsultaYPermisoSinConsultarProveedor()
    {
        using var client = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/location/search")).StatusCode);
        var login = await f.Login(client); client.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/location/search")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/location/search?q=ab")).StatusCode);
        login = await f.Login(client, await f.CreateUser()); client.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/location/search?q=Llallagua")).StatusCode);
    }
}
