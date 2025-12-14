using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.Parties;

public enum PartyType
{
    Customer = 1,
    Vendor = 2,
    Both = 3
}

public enum PartyCategory
{
    Retail = 1,
    Wholesale = 2,
    Distributor = 3,
    Other = 4
}

public class PartyFormViewModel : IValidatableObject
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
    public string? Gstin { get; set; }

    [Display(Name = "PAN Number")]
    public string? PanNumber { get; set; }

    [Display(Name = "Party Type")]
    public PartyType PartyType { get; set; } = PartyType.Customer;

    [Display(Name = "Party Category")]
    public PartyCategory PartyCategory { get; set; } = PartyCategory.Retail;

    [Display(Name = "Billing Address")]
    public string? BillingAddress { get; set; }

    [Display(Name = "Shipping Address")]
    public string? ShippingAddress { get; set; }

    [Display(Name = "Same as Billing")]
    public bool SameAsBilling { get; set; }

    [Display(Name = "Credit Period (days)")]
    [Range(0, 3650, ErrorMessage = "Credit period must be 0 or more.")]
    public int? CreditPeriodDays { get; set; }

    [Display(Name = "Credit Limit")]
    public decimal? CreditLimit { get; set; }

    [Display(Name = "Contact Person Name")]
    public string? ContactPersonName { get; set; }

    [Display(Name = "Date of Birth")]
    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Bank Account Number")]
    public string? BankAccountNumber { get; set; }

    [Display(Name = "Re-enter Account Number")]
    public string? ReEnterAccountNumber { get; set; }

    [Display(Name = "IFSC Code")]
    public string? IfscCode { get; set; }

    [Display(Name = "Branch Name")]
    public string? BranchName { get; set; }

    [Display(Name = "Account Holder Name")]
    public string? AccountHolderName { get; set; }

    [Display(Name = "UPI ID")]
    public string? UpiId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Gstin) && Gstin.Trim().Length != 15)
        {
            yield return new ValidationResult("GSTIN must be exactly 15 characters.", new[] { nameof(Gstin) });
        }

        var account = BankAccountNumber?.Trim();
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
            yield return new ValidationResult("Enter the account number first.", new[] { nameof(BankAccountNumber) });
        }
    }
}

