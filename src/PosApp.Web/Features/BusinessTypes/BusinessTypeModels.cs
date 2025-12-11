using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.BusinessTypes;

public sealed record BusinessTypeListItem(
    Guid Id,
    string IndustryTypeName,
    bool IsActive);

public sealed record BusinessTypeDetails(
    Guid Id,
    string IndustryTypeName,
    bool IsActive);

public sealed record BusinessTypeInput(
    string IndustryTypeName,
    bool IsActive);

public sealed class BusinessTypeFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [MaxLength(120)]
    [Display(Name = "Industry type name")]
    public string IndustryTypeName { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
