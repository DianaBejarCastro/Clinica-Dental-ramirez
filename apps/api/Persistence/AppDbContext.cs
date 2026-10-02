using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<Center> Centers => Set<Center>();
    public DbSet<DoctorProfile> DoctorProfiles => Set<DoctorProfile>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<DoctorSpecialty> DoctorSpecialties => Set<DoctorSpecialty>();
    public DbSet<DoctorCenter> DoctorCenters => Set<DoctorCenter>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ConfigureClinicAdministration();
        b.ConfigurePatients();
        b.Entity<User>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(1024).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.HasIndex(x => x.Email).IsUnique();
            e.ToTable("Users", t => {
                t.HasCheckConstraint("CK_Users_EmailNormalized", "\"Email\" = lower(btrim(\"Email\")) AND length(\"Email\") > 3");
                t.HasCheckConstraint("CK_Users_Names", "length(btrim(\"FirstName\")) > 0 AND length(btrim(\"LastName\")) > 0");
                t.HasCheckConstraint("CK_Users_PasswordHash", "length(\"PasswordHash\") > 0");
            });
        });
        b.Entity<Role>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(80).IsRequired();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Name).IsUnique();
            e.ToTable("Roles", t => t.HasCheckConstraint("CK_Roles_Code", "\"Code\" ~ '^[A-Z][A-Z0-9_]*$'"));
        });
        b.Entity<Permission>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(100).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Module).HasMaxLength(80).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Module);
        });
        b.Entity<UserRole>(e => {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RolePermission>(e => {
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RefreshToken>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.CreatedByIp).HasMaxLength(64);
            e.Property(x => x.RevokedByIp).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.ExpiresAt);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ReplacedByTokenId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("RefreshTokens", t => {
                t.HasCheckConstraint("CK_RefreshTokens_Expiry", "\"ExpiresAt\" > \"CreatedAt\"");
                t.HasCheckConstraint("CK_RefreshTokens_Hash", "length(\"TokenHash\") = 64");
            });
        });
        b.Entity<AuditLog>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(80).IsRequired();
            e.Property(x => x.Entity).HasMaxLength(80).IsRequired();
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.UserAgent).HasMaxLength(512);
            e.Property(x => x.MetadataJson).HasColumnType("jsonb");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.Entity, x.EntityId });
            e.HasIndex(x => x.Action);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PreparePatients();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAdministrativeRecord record)
            {
                if (entry.State == EntityState.Deleted)
                    throw new InvalidOperationException("Utilice desactivación lógica.");
                if (entry.State is EntityState.Added or EntityState.Modified)
                    record.UpdatedAt = DateTime.UtcNow;
            }
            if (entry.Entity is AuditLog && entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("La auditoría es inmutable.");
            if (entry.State == EntityState.Deleted && (entry.Entity is User || entry.Entity is Role))
                throw new InvalidOperationException("Utilice desactivación lógica.");
            if (entry.Entity is User user && entry.State is EntityState.Added or EntityState.Modified)
            {
                user.Email = user.Email.Trim().ToLowerInvariant();
                user.UpdatedAt = DateTime.UtcNow;
            }
            if (entry.Entity is Role role && entry.State is EntityState.Added or EntityState.Modified)
                role.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    private void PreparePatients()
    {
        foreach (var entry in ChangeTracker.Entries<PatientProfile>())
        {
            if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Utilice desactivación lógica de pacientes.");
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = DateTime.UtcNow;
            if (entry.State is EntityState.Added or EntityState.Modified) entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PreparePatients();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PreparePatients();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
