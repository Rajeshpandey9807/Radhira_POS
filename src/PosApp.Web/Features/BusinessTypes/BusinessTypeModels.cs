using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.BusinessTypes;

public sealed class BusinessTypeListItem
{
    public int BusinessTypeId { get; set; }
    public string BusinessTypeName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
}

public sealed class BusinessTypeDetails
{
    public int BusinessTypeId { get; set; }
    public string BusinessTypeName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed record BusinessTypeInput(string BusinessTypeName);

public sealed class BusinessTypeFormViewModel
{
    public int? BusinessTypeId { get; set; }

    [Required]
    [MaxLength(120)]
    [Display(Name = "Business type name")]
    public string BusinessTypeName { get; set; } = string.Empty;
}
