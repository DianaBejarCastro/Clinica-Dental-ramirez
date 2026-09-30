using ClinicaRamirez.Api.Application.Identity;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace ClinicaRamirez.Api.Middleware;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
    {
        var (status, title) = error switch {
            ApiException ex => (ex.Status, ex.Message),
            DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } => (409, "El valor ya existe."),
            DbUpdateException { InnerException: PostgresException { SqlState: "23503" or "23514" or "23502" } } => (400, "Datos o referencias inválidos."),
            DbUpdateConcurrencyException => (409, "El registro cambió. Vuelva a intentarlo."),
            _ => (500, "No se pudo completar la operación.")
        };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title }, ct);
        return true;
    }
}
