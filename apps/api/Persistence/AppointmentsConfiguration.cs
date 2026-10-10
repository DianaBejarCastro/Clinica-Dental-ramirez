using ClinicaRamirez.Api.Domain.Appointments;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Persistence;

public static class AppointmentsConfiguration
{
    public static void ConfigureAppointments(this ModelBuilder b) => b.Entity<Appointment>(e => {
        e.HasKey(x => x.Id);
        e.Property(x => x.Status).HasMaxLength(20).IsRequired();
        e.Property(x => x.Reason).HasMaxLength(1000);
        e.Property(x => x.Notes).HasMaxLength(4000);
        e.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<DoctorProfile>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Center>().WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Specialty>().WithMany().HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.DoctorId, x.AppointmentDate, x.StartTime });
        e.HasIndex(x => new { x.PatientId, x.AppointmentDate });
        e.HasIndex(x => new { x.CenterId, x.AppointmentDate });
        e.HasIndex(x => x.Status);
        e.ToTable("Appointments", t => {
            t.HasCheckConstraint("CK_Appointments_Time", "\"StartTime\" < \"EndTime\"");
            t.HasCheckConstraint("CK_Appointments_Status", "\"Status\" IN ('SCHEDULED','CONFIRMED','IN_PROGRESS','COMPLETED','CANCELLED','NO_SHOW')");
        });
    });
}
