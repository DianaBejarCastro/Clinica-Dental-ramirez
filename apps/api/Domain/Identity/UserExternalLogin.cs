namespace ClinicaRamirez.Api.Domain.Identity;

public sealed class UserExternalLogin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Provider { get; set; } = "GOOGLE";
    public string ProviderSubject { get; set; } = "";
    public string ProviderEmail { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
