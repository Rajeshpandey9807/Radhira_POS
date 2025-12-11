using System;
using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.RegistrationTypes;

public sealed class RegistrationTypeListItem
{
    public int RegistrationTypeId { get; set; }
    public string RegistrationTypeName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
}

public sealed class RegistrationTypeDetails
{
    public int RegistrationTypeId { get; set; }
    public string RegistrationTypeName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed record RegistrationTypeInput(string RegistrationTypeName);

public sealed class RegistrationTypeFormViewModel
{
    public int? RegistrationTypeId { get; set; }

    [Required]
    [MaxLength(120)]
    [Display(Name = "Registration type name")]
    public string RegistrationTypeName { get; set; } = string.Empty;
}
