using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClinicaRamirez.Api.Application.Identity;

namespace ClinicaRamirez.Api.Application.ClinicAdministration;

public sealed record LocationSearchResult(string DisplayName, double Latitude, double Longitude);

// Singleton: un límite compartido para todos los usuarios de esta instancia de la API.
public sealed class LocationSearchService(IHttpClientFactory clients, IConfiguration configuration) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, (DateTimeOffset Expires, LocationSearchResult[] Results)> cache = [];
    private DateTimeOffset nextRequest;

    public async Task<LocationSearchResult[]> Search(string? query, CancellationToken ct)
    {
        var q = query?.Trim() ?? "";
        if (q.Length is < 3 or > 250 || q.Any(char.IsControl)) throw new ApiException(400, "Escribe entre 3 y 250 caracteres para buscar una ubicación.");
        if (!await gate.WaitAsync(0, ct)) throw new ApiException(429, "Espera un momento antes de volver a buscar.");
        try
        {
            var now = DateTimeOffset.UtcNow;
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(q)));
            foreach (var expired in cache.Where(x => x.Value.Expires <= now).Select(x => x.Key).ToArray()) cache.Remove(expired);
            if (cache.TryGetValue(key, out var cached)) return cached.Results;
            if (now < nextRequest) throw new ApiException(429, "Espera un momento antes de volver a buscar.");
            nextRequest = now.AddSeconds(1);
            var endpoint = configuration["LocationSearch:Endpoint"] ?? "https://nominatim.openstreetmap.org/search";
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri) || baseUri.Scheme != "https")
                throw new ApiException(503, "El servicio de ubicación no está disponible.");
            var separator = string.IsNullOrEmpty(baseUri.Query) ? "?" : "&";
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint + separator + "q=" + Uri.EscapeDataString(q) + "&format=json&limit=5");
            request.Headers.UserAgent.ParseAdd("ClinicaDentalRamirez-BranchLocation/1.0");
            request.Headers.AcceptLanguage.ParseAdd("es");
            using var client = clients.CreateClient("LocationSearch");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(8));
            try
            {
                using var response = await client.SendAsync(request, timeout.Token);
                if (!response.IsSuccessStatusCode) throw new ApiException(502, "No se pudo consultar el servicio de ubicación. Inténtalo más tarde.");
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                if (json.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException();
                var results = new List<LocationSearchResult>();
                foreach (var item in json.RootElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("display_name", out var name) || name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString())
                        || !item.TryGetProperty("lat", out var lat) || !item.TryGetProperty("lon", out var lon)
                        || !double.TryParse(lat.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
                        || !double.TryParse(lon.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
                        || !double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180) continue;
                    results.Add(new(name.GetString()!, latitude, longitude)); if (results.Count == 5) break;
                }
                if (cache.Count >= 100) cache.Remove(cache.Keys.First());
                var value = results.ToArray(); cache[key] = (DateTimeOffset.UtcNow.AddMinutes(5), value); return value;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException(504, "La búsqueda tardó demasiado. Inténtalo nuevamente."); }
            catch (HttpRequestException) { throw new ApiException(502, "No se pudo conectar con el servicio de ubicación."); }
            catch (JsonException) { throw new ApiException(502, "El servicio de ubicación devolvió una respuesta inválida."); }
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
