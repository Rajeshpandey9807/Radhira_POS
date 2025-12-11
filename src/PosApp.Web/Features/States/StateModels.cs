using System;
using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.States;

public sealed class StateListItem
{
    public int StateId { get; set; }
    public string StateName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
}

public sealed class StateDetails
{
    public int StateId { get; set; }
    public string StateName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed record StateInput(string StateName);

public sealed class StateFormViewModel
{
    public int? StateId { get; set; }

    [Required]
    [MaxLength(120)]
    [Display(Name = "State name")]
    public string StateName { get; set; } = string.Empty;
}
