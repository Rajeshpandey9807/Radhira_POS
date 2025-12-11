using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.IndustryTypes;

public sealed class IndustryTypeListItem
{
    public int IndustryTypeId { get; set; }
    public string IndustryTypeName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
}

public sealed class IndustryTypeDetails
{
    public int IndustryTypeId { get; set; }
    public string IndustryTypeName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed record IndustryTypeInput(string IndustryTypeName);

public sealed class IndustryTypeFormViewModel
{
    public int? IndustryTypeId { get; set; }

    [Required]
    [MaxLength(120)]
    [Display(Name = "Industry type name")]
    public string IndustryTypeName { get; set; } = string.Empty;
}
