using ClinicaRamirez.Api.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;
namespace ClinicaRamirez.Api.Tests;
public class DeploymentConfigurationTests
{
    static IConfiguration Config(string? origin = null, bool crossSite = false) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["Cors:AllowedOrigins:0"] = origin, ["AuthCookies:CrossSite"] = crossSite.ToString()
    }.Where(x => x.Value != null)).Build();
    [Fact] public void DevelopmentDefaultsToLocal() => Assert.Equal(["http://localhost:5173"], DeploymentConfiguration.Origins(Config(), true));
    [Fact] public void ProductionRequiresExplicitOrigins() => Assert.Throws<InvalidOperationException>(() => DeploymentConfiguration.Origins(Config(), false));
    [Theory][InlineData("*")][InlineData("https://*.example.test")][InlineData("http://example.test")][InlineData("https://example.test/path")]
    public void RejectsUnsafeProductionOrigins(string origin) => Assert.Throws<InvalidOperationException>(() => DeploymentConfiguration.Origins(Config(origin), false));
    [Fact] public void ExplicitHttpsOriginAccepted() => Assert.Equal(["https://frontend.example.test"], DeploymentConfiguration.Origins(Config("https://frontend.example.test"), false));
    [Fact] public void CrossSiteCookiesAreOptIn() { Assert.Equal(SameSiteMode.Strict, DeploymentConfiguration.CookieSameSite(Config())); Assert.Equal(SameSiteMode.None, DeploymentConfiguration.CookieSameSite(Config(crossSite:true))); }
}
