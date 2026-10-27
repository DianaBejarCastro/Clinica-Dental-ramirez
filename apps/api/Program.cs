using ClinicaRamirez.Api.Application.DentalCharting;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Application.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Infrastructure;
using ClinicaRamirez.Api.Infrastructure.Identity;
using ClinicaRamirez.Api.Middleware;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var password = builder.Configuration["POSTGRES_PASSWORD"];
    if (string.IsNullOrWhiteSpace(password) || password == "CHANGE_ME")
        throw new InvalidOperationException("Configure POSTGRES_PASSWORD o ConnectionStrings__DefaultConnection.");
    connectionString = new NpgsqlConnectionStringBuilder {
        Host = builder.Configuration["POSTGRES_HOST"] ?? "localhost",
        Port = int.Parse(builder.Configuration["POSTGRES_PORT"] ?? "5432"),
        Database = builder.Configuration["POSTGRES_DB"] ?? "clinica_ramirez",
        Username = builder.Configuration["POSTGRES_USER"] ?? "clinica_user",
        Password = password,
        GssEncryptionMode = builder.Environment.IsDevelopment() ? GssEncryptionMode.Disable : GssEncryptionMode.Prefer
    }.ConnectionString;
}
var jwt = JwtSettings.Load(builder.Configuration);
builder.Services.AddSingleton(jwt);
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IdentitySupport>();
builder.Services.AddScoped<ClinicaRamirez.Api.Application.ClinicalCare.ClinicalCareService>();
builder.Services.AddScoped<ClinicaRamirez.Api.Application.ClinicalCare.FinancialPayments>();
builder.Services.AddScoped<ClinicaRamirez.Api.Application.Cashier.CashierService>();
builder.Services.AddScoped<ClinicaRamirez.Api.Application.Inventory.InventoryService>();
builder.Services.AddScoped<ClinicaRamirez.Api.Application.Inventory.InventoryMovementService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<StaffAdministrationService>();
builder.Services.AddScoped<IdentitySeed>();
builder.Services.AddScoped<ClinicAdministrationService>();
builder.Services.AddScoped<ClinicaRamirez.Api.Application.DentalAssistants.DentalAssistantsService>();
builder.Services.AddScoped<ClinicLogoService>();
builder.Services.AddScoped<ClinicSiteImageService>();
builder.Services.AddScoped<UserProfilePhotoService>();
builder.Services.AddScoped<ChartingAccess>();
builder.Services.AddScoped(typeof(ChartExamStore<>));
builder.Services.AddScoped<OdontogramService>();
builder.Services.AddScoped<PeriodontalService>();
builder.Services.AddScoped<ClinicSocialNetworkService>();
builder.Services.AddScoped<ClinicaRamirez.Api.Application.Deletion.DeletionGuardService>();
builder.Services.AddHttpClient("LocationSearch", client => client.Timeout = TimeSpan.FromSeconds(8)).RemoveAllLoggers();
builder.Services.AddSingleton<LocationSearchService>();
builder.Services.AddScoped<ClinicaRamirez.Api.Application.Patients.PatientsService>();
builder.Services.AddScoped<ClinicaRamirez.Api.Application.Patients.PatientAccountService>();
builder.Services.AddScoped<ClinicaRamirez.Api.Application.Patients.PatientRegistrationService>();
builder.Services.AddScoped<ClinicaRamirez.Api.Application.Patients.PatientPortalService>();
builder.Services.AddScoped<ClinicaRamirez.Api.Application.Appointments.AppointmentsService>();
builder.Services.AddScoped<ClinicSeed>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.FromSeconds(10),
        NameClaimType = "email", RoleClaimType = "roles"
    };
    options.Events = new JwtBearerEvents { OnTokenValidated = async context => {
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        if (!Guid.TryParse(context.Principal?.FindFirstValue("sub"), out var userId)
            || !Guid.TryParse(context.Principal?.FindFirstValue("sid"), out var sessionId)
            || !await db.RefreshTokens.AnyAsync(x => x.Id == sessionId && x.UserId == userId
                && x.User.IsActive && x.User.CanLogin && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow))
            context.Fail("Sesión inválida.");
    }};
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Clínica Dental RAMÍREZ · Identidad", Version = "0.3.0" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Access token. Para POST /api/auth/* enviar X-Clinic-Request: 1; refresh usa cookie HttpOnly."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"]);
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy("Web",
    policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(options => {
    options.AddPolicy("hard-delete", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue("sub") ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("HARD_DELETE_RATE_LIMIT", 5), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("patient-registration", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("REGISTRATION_RATE_LIMIT", 10), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("LOGIN_RATE_LIMIT", 30),
            Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseCors("Web");
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/api"))
        context.Response.Headers.CacheControl = "no-store";
    if (context.Request.Method == "POST" && context.Request.Path.StartsWithSegments("/api/auth"))
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (context.Request.Headers["X-Clinic-Request"] != "1" || (origin.Length > 0 && !origins.Contains(origin)))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { title = "Origen o cabecera de seguridad inválidos." });
            return;
        }
    }
    await next();
});
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
// Comando explícito; no se aplican migraciones ni seed al arrancar el servidor.
if (args.Contains("--seed-identity") || args.Contains("--seed-clinic"))
{
    using var scope = app.Services.CreateScope();
    if (args.Contains("--seed-identity")) await scope.ServiceProvider.GetRequiredService<IdentitySeed>().Run();
    if (args.Contains("--seed-clinic")) await scope.ServiceProvider.GetRequiredService<ClinicSeed>().Run();
    return;
}
app.Run();
public partial class Program { }
