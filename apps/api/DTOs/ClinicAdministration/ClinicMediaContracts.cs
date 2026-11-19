using System.ComponentModel.DataAnnotations;
namespace ClinicaRamirez.Api.DTOs.ClinicAdministration;
public class UpdateMediaItemRequest {
 [MaxLength(200)] public string? Title { get; set; }
 [MaxLength(1000)] public string? Caption { get; set; }
 [MaxLength(300)] public string? AltText { get; set; }
 [Range(0,100000)] public int SortOrder { get; set; }
 public bool IsActive { get; set; } = true;
}
public sealed class CreateMediaItemRequest : UpdateMediaItemRequest {
 [Required] public string MediaType { get; set; } = "IMAGE";
 public IFormFile? File { get; set; }
 [MaxLength(500)] public string? ExternalUrl { get; set; }
}
public sealed record PublicMediaItemDto(Guid Id,string MediaType,string? Title,string? Caption,string? AltText,string? ExternalUrl,int? Width,int? Height);
public sealed record AdminMediaItemDto(Guid Id,string MediaType,string? Title,string? Caption,string? AltText,string? ExternalUrl,int? Width,int? Height,int SortOrder,bool IsActive);
public sealed record MediaPage<T>(T[] Items,int Total,int Page,int PageSize,bool VideoUploadAvailable);
