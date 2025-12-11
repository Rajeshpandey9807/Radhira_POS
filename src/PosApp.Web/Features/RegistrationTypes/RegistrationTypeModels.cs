using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.RegistrationTypes;

public sealed record RegistrationTypeListItem(
    Guid Id,
    string RegistrationTypeName,
    bool IsActive);

public sealed record RegistrationTypeDetails(
    Guid Id,
    string RegistrationTypeName,
    bool IsActive);

public sealed record RegistrationTypeInput(
    string RegistrationTypeName,
    bool IsActive);

public sealed class RegistrationTypeFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [MaxLength(120)]
    [Display(Name = "Registration type name")]
    public string RegistrationTypeName { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
