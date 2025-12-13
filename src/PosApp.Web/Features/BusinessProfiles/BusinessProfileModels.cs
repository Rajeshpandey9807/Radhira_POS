using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using PosApp.Web.Features.BusinessTypes;
using PosApp.Web.Features.IndustryTypes;
using PosApp.Web.Features.RegistrationTypes;
using PosApp.Web.Features.States;

namespace PosApp.Web.Features.BusinessProfiles;

public sealed class BusinessProfileFormViewModel : IValidatableObject
{
    [Required]
    [MaxLength(200)]
    [Display(Name = "Business name")]
    public string BusinessName { get; set; } = string.Empty;

    [MaxLength(30)]
    [Display(Name = "Company phone number")]
    public string? CompanyPhoneNumber { get; set; }

    [EmailAddress]
    [MaxLength(200)]
    [Display(Name = "Company e-mail")]
    public string? CompanyEmail { get; set; }

    [MaxLength(500)]
    [Display(Name = "Billing address")]
    public string? BillingAddress { get; set; }

    [Display(Name = "State")]
    public int? StateId { get; set; }

    [MaxLength(12)]
    [Display(Name = "Pincode")]
    public string? Pincode { get; set; }

    [MaxLength(120)]
    [Display(Name = "City")]
    public string? City { get; set; }

    [Display(Name = "Are you GST registered?")]
    public bool? IsGstRegistered { get; set; }

    [MaxLength(30)]
    [Display(Name = "GST number")]
    public string? GstNumber { get; set; }

    [MaxLength(20)]
    [Display(Name = "PAN number")]
    public string? PanNumber { get; set; }

    [Display(Name = "Business type (select multiple, if applicable)")]
    public List<int> SelectedBusinessTypeIds { get; set; } = new();

    [Display(Name = "Industry type")]
    public int? IndustryTypeId { get; set; }

    [Display(Name = "Business registration type")]
    public int? RegistrationTypeId { get; set; }

    [Display(Name = "Signature")]
    public IFormFile? SignatureFile { get; set; }

    [Display(Name = "Business logo")]
    public IFormFile? BusinessLogoFile { get; set; }

    [MaxLength(50)]
    [Display(Name = "MSME number")]
    public string? MsmeNumber { get; set; }

    [Url]
    [MaxLength(200)]
    [Display(Name = "Website")]
    public string? Website { get; set; }

    [MaxLength(800)]
    [Display(Name = "Additional business information")]
    public string? AdditionalInfo { get; set; }

    public IEnumerable<BusinessTypeListItem> BusinessTypes { get; set; } = Array.Empty<BusinessTypeListItem>();
    public IEnumerable<IndustryTypeListItem> IndustryTypes { get; set; } = Array.Empty<IndustryTypeListItem>();
    public IEnumerable<RegistrationTypeListItem> RegistrationTypes { get; set; } = Array.Empty<RegistrationTypeListItem>();
    public IEnumerable<StateListItem> States { get; set; } = Array.Empty<StateListItem>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsGstRegistered == true && string.IsNullOrWhiteSpace(GstNumber))
        {
            yield return new ValidationResult("GST number is required when GST registration is Yes.", new[] { nameof(GstNumber) });
        }
    }
}

