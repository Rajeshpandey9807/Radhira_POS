using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.Products;

public sealed record ProductTypeOption(int ProductTypeId, string ProductTypeName);
public sealed record CategoryOption(int CategoryId, string CategoryName);
public sealed record GstRateOption(int GstRateId, string GstRateName, decimal RatePercentage);
public sealed record UnitOption(int UnitId, string UnitName, string? UnitSymbol);

public sealed record ProductListItem(
    Guid ProductId,
    string ItemName,
    string? ItemCode,
    string? CategoryName,
    string? ProductTypeName,
    decimal? SalesPrice,
    decimal? CurrentStock,
    string? UnitSymbol,
    decimal? GstRate,
    bool IsActive);

public class ProductCreateRequest : IValidatableObject
{
    [Required(ErrorMessage = "Product type is required.")]
    [Display(Name = "Product Type")]
    public int? ProductTypeId { get; set; }

    [Required(ErrorMessage = "Category is required.")]
    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Item name is required.")]
    [Display(Name = "Item Name")]
    [StringLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = string.Empty;

    [Display(Name = "Item Code")]
    [StringLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string? ItemCode { get; set; }

    [Display(Name = "HSN Code")]
    [StringLength(20, ErrorMessage = "HSN code cannot exceed 20 characters.")]
    public string? HSNCode { get; set; }

    [Display(Name = "Description")]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [Display(Name = "Is Active")]
    public bool IsActive { get; set; } = true;

    // Pricing Details
    [Required(ErrorMessage = "Sales price is required.")]
    [Display(Name = "Sales Price")]
    [Range(0, 999999999.99, ErrorMessage = "Sales price must be between 0 and 999999999.99")]
    public decimal? SalesPrice { get; set; }

    [Display(Name = "Purchase Price")]
    [Range(0, 999999999.99, ErrorMessage = "Purchase price must be between 0 and 999999999.99")]
    public decimal? PurchasePrice { get; set; }

    [Display(Name = "MRP")]
    [Range(0, 999999999.99, ErrorMessage = "MRP must be between 0 and 999999999.99")]
    public decimal? MRP { get; set; }

    [Required(ErrorMessage = "GST rate is required.")]
    [Display(Name = "GST Rate")]
    public int? GstRateId { get; set; }

    // Stock Details
    [Display(Name = "Opening Stock")]
    [Range(0, 999999999.99, ErrorMessage = "Opening stock must be between 0 and 999999999.99")]
    public decimal? OpeningStock { get; set; }

    [Display(Name = "Current Stock")]
    [Range(0, 999999999.99, ErrorMessage = "Current stock must be between 0 and 999999999.99")]
    public decimal? CurrentStock { get; set; }

    [Required(ErrorMessage = "Unit is required.")]
    [Display(Name = "Unit")]
    public int? UnitId { get; set; }

    [Display(Name = "As Of Date")]
    [DataType(DataType.Date)]
    public DateTime? AsOfDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MRP.HasValue && SalesPrice.HasValue && MRP < SalesPrice)
        {
            yield return new ValidationResult(
                "MRP cannot be less than sales price.",
                new[] { nameof(MRP) });
        }

        if (PurchasePrice.HasValue && SalesPrice.HasValue && PurchasePrice > SalesPrice)
        {
            yield return new ValidationResult(
                "Warning: Purchase price is greater than sales price.",
                new[] { nameof(PurchasePrice) });
        }
    }
}

public sealed class ProductEditRequest : ProductCreateRequest
{
    [Required]
    public Guid ProductId { get; set; }
}
