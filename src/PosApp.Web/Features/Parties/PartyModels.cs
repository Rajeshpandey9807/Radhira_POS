using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.Parties;

public sealed record PartyTypeOption(int PartyTypeId, string TypeName);
public sealed record PartyCategoryOption(int PartyCategoryId, string CategoryName);

public sealed class PartyCreateRequest : IValidatableObject
{
    [Required(ErrorMessage = "Party name is required.")]
    [Display(Name = "Party Name")]
    public string PartyName { get; set; } = string.Empty;

    [Display(Name = "Mobile Number")]
    [RegularExpression(@"^\d*$", ErrorMessage = "Mobile number must contain digits only.")]
    public string? MobileNumber { get; set; }

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }

    [Display(Name = "Opening Balance")]
    public decimal? OpeningBalance { get; set; }

    [Display(Name = "GSTIN")]
    [StringLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? GSTIN { get; set; }

    [Display(Name = "PAN Number")]
    public string? PANNumber { get; set; }

    [Display(Name = "Party Type")]
    [Required(ErrorMessage = "Party type is required.")]
    public int? PartyTypeId { get; set; }

    [Display(Name = "Party Category")]
    [Required(ErrorMessage = "Party category is required.")]
    public int? PartyCategoryId { get; set; }

    [Display(Name = "Billing Address")]
    public string? BillingAddress { get; set; }

    [Display(Name = "Shipping Address")]
    public string? ShippingAddress { get; set; }

    [Display(Name = "Same as Billing")]
    public bool SameAsBilling { get; set; }

    [Display(Name = "Credit Period (days)")]
    [Range(0, 3650, ErrorMessage = "Credit period must be 0 or more.")]
    public int? CreditPeriod { get; set; }

    [Display(Name = "Credit Limit")]
    public decimal? CreditLimit { get; set; }

    [Display(Name = "Contact Person Name")]
    public string? ContactPersonName { get; set; }

    [Display(Name = "Date of Birth")]
    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Bank Account Number")]
    public string? AccountNumber { get; set; }

    [Display(Name = "Re-enter Account Number")]
    public string? ReEnterAccountNumber { get; set; }

    [Display(Name = "IFSC Code")]
    public string? IFSC { get; set; }

    [Display(Name = "Branch Name")]
    public string? BranchName { get; set; }

    [Display(Name = "Account Holder Name")]
    public string? AccountHolderName { get; set; }

    [Display(Name = "UPI ID")]
    public string? UPI { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(GSTIN) && GSTIN.Trim().Length != 15)
        {
            yield return new ValidationResult("GSTIN must be exactly 15 characters.", new[] { nameof(GSTIN) });
        }

        var account = AccountNumber?.Trim();
        var reenter = ReEnterAccountNumber?.Trim();

        if (!string.IsNullOrWhiteSpace(account))
        {
            if (string.IsNullOrWhiteSpace(reenter))
            {
                yield return new ValidationResult("Please re-enter the account number.", new[] { nameof(ReEnterAccountNumber) });
            }
            else if (!string.Equals(account, reenter, StringComparison.Ordinal))
            {
                yield return new ValidationResult("Account numbers do not match.", new[] { nameof(ReEnterAccountNumber) });
            }
        }
        else if (!string.IsNullOrWhiteSpace(reenter))
        {
            yield return new ValidationResult("Enter the account number first.", new[] { nameof(AccountNumber) });
        }
    }
}

