using System.ComponentModel.DataAnnotations;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.DentalAssistants;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.DTOs.DentalAssistants;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.DentalAssistants;

public sealed class DentalAssistantsService(AppDbContext db, IdentitySupport support, IPasswordHasher<User> hasher)
{
    private IQueryable<DentalAssistantProfile> Profiles => db.DentalAssistantProfiles.Include(x => x.User);
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void Validate(DentalAssistantUpdateRequest request)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new(request), errors, true))
            throw new ApiException(400, errors[0].ErrorMessage ?? "Datos inválidos.");
    }
    private async Task Mutate(Func<Task> work)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        await work();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
    public async Task<DentalAssistantListResponse> List(int page, int pageSize)
    {
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100) throw new ApiException(400, "Paginación inválida.");
        var items = await Profiles.AsNoTracking().OrderBy(x => x.User.LastName).ThenBy(x => x.User.FirstName).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync();
        return new(items.Select(Dto).ToArray(), await db.DentalAssistantProfiles.CountAsync(), page, pageSize);
    }
    public async Task<DentalAssistantResponse> Get(Guid id) => Dto(await Profiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id)
        ?? throw new ApiException(404, "Asistente dental inexistente."));
    public async Task<DentalAssistantResponse> Create(DentalAssistantCreateRequest request)
    {
        Validate(request);
        var user = new User { CanLogin = true, IsActive = true };
        var profile = new DentalAssistantProfile { User = user, UserId = user.Id };
        await Mutate(async () => {
            await Apply(user, request);
            var email = request.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(x => x.Email == email)) throw new ApiException(409, "El email ya se encuentra registrado.");
            var role = await db.Roles.SingleOrDefaultAsync(x => x.Code == "DENTAL_ASSISTANT" && x.IsActive)
                ?? throw new ApiException(409, "El rol DENTAL_ASSISTANT no está disponible.");
            user.Email = email;
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            db.DentalAssistantProfiles.Add(profile);
            db.UserRoles.Add(new UserRole { User = user, RoleId = role.Id, AssignedByUserId = support.Actor });
            support.Audit("dental_assistant_created", "DentalAssistantProfile", profile.Id);
        });
        return await Get(profile.Id);
    }
    public async Task<DentalAssistantResponse> Update(Guid id, DentalAssistantUpdateRequest request)
    {
        Validate(request);
        await Mutate(async () => {
            var profile = await Profiles.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Asistente dental inexistente.");
            await Apply(profile.User, request);
            // Preserve profile activity and credentials; this operation edits personal data only.
            profile.UpdatedAt = DateTime.UtcNow;
            support.Audit("dental_assistant_updated", "DentalAssistantProfile", id);
        });
        return await Get(id);
    }
    private async Task Apply(User user, DentalAssistantUpdateRequest request)
    {
        var ci = request.Ci.Trim().ToUpperInvariant();
        if (await db.Users.AnyAsync(x => x.Id != user.Id && x.Ci == ci)) throw new ApiException(409, "El CI ya se encuentra registrado.");
        user.FirstName = request.FirstName.Trim(); user.LastName = request.LastName.Trim(); user.Ci = ci;
        user.BirthDate = request.BirthDate; user.Gender = Optional(request.Gender);
        user.Phone = request.Phone.Trim(); user.Address = Optional(request.Address);
    }
    private static DentalAssistantResponse Dto(DentalAssistantProfile p) => new(p.Id, p.UserId,
        p.User.FirstName, p.User.LastName, p.User.Ci, p.User.BirthDate, p.User.Gender, p.User.Phone,
        p.User.Address, p.User.Email, p.IsActive, p.User.IsActive, p.User.CanLogin, p.CreatedAt, p.UpdatedAt);
}
