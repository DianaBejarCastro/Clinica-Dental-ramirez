using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.DTOs.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace ClinicaRamirez.Api.Controllers;

[ApiController, Route("api/auth"), Tags("Auth")]
public sealed class AuthController(AuthService auth, IdentitySupport support, IWebHostEnvironment environment) : ControllerBase
{
    private const string CookieName = "clinic_refresh";
    private CookieOptions Options(DateTime? expiry = null) => new() {
        HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Strict,
        Path = "/api/auth", Expires = expiry, IsEssential = true
    };
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var result = await auth.Login(request);
        Response.Cookies.Append(CookieName, result.Cookie, Options(result.Expiry));
        return Ok(result.Response);
    }
    [HttpPost("refresh"), AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh()
    {
        try {
            var result = await auth.Refresh(Request.Cookies[CookieName]);
            Response.Cookies.Append(CookieName, result.Cookie, Options(result.Expiry));
            return Ok(result.Response);
        } catch (ApiException) {
            Response.Cookies.Delete(CookieName, Options());
            throw;
        }
    }
    [HttpPost("logout"), AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await auth.Logout(Request.Cookies[CookieName]);
        Response.Cookies.Delete(CookieName, Options());
        return NoContent();
    }
    [HttpGet("me"), Authorize]
    public async Task<ActionResult<CurrentUserResponse>> Me() => Ok(await support.Current(support.Actor!.Value));
}
