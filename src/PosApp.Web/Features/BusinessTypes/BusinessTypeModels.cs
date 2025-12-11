using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.BusinessTypes;

public sealed record BusinessTypeListItem(
    Guid BusinessTypeId,
    string BusinessTypeName,
    bool IsActive,
    string CreatedBy,
    DateTime CreatedOn,
    string? UpdatedBy,
    DateTime? UpdatedOn);

public sealed record BusinessTypeDetails(
    Guid BusinessTypeId,
    string BusinessTypeName,
    bool IsActive);

public sealed record BusinessTypeInput(
    string BusinessTypeName);

public sealed class BusinessTypeFormViewModel
{
    public Guid? BusinessTypeId { get; set; }

    [Required]
    [MaxLength(120)]
    [Display(Name = "Business type name")]
    public string BusinessTypeName { get; set; } = string.Empty;
}
