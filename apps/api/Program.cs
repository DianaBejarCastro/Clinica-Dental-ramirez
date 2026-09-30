using ClinicaRamirez.Api.Infrastructure;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var password = builder.Configuration["POSTGRES_PASSWORD"];
    if (string.IsNullOrWhiteSpace(password) || password == "CHANGE_ME")
        throw new InvalidOperationException("Configure POSTGRES_PASSWORD o ConnectionStrings__DefaultConnection antes de iniciar la API.");

    connectionString = new NpgsqlConnectionStringBuilder
    {
        Host = builder.Configuration["POSTGRES_HOST"] ?? "localhost",
        Port = int.Parse(builder.Configuration["POSTGRES_PORT"] ?? "5432"),
        Database = builder.Configuration["POSTGRES_DB"] ?? "clinica_ramirez",
        Username = builder.Configuration["POSTGRES_USER"] ?? "clinica_user",
        Password = password
    }.ConnectionString;
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"]);
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy("Web",
    policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("Web");
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();
