using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.Products;

public sealed record ProductTypeOption(int ProductTypeId, string TypeName);
public sealed record CategoryOption(int CategoryId, string CategoryName);
public sealed record UnitOption(int UnitId, string UnitName);
public sealed record GstRateOption(int GstRateId, string RateName, decimal Rate);

public sealed record ProductListItem(
    Guid ProductId,
    string ItemName,
    string? ItemCode,
    string? CategoryName,
    string? ProductTypeName,
    decimal? SalesPrice,
    decimal? CurrentStock,
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
    [Display(Name = "Sales Price")]
    [Range(0, double.MaxValue, ErrorMessage = "Sales price must be a positive value.")]
    public decimal? SalesPrice { get; set; }

    [Display(Name = "Purchase Price")]
    [Range(0, double.MaxValue, ErrorMessage = "Purchase price must be a positive value.")]
    public decimal? PurchasePrice { get; set; }

    [Display(Name = "MRP")]
    [Range(0, double.MaxValue, ErrorMessage = "MRP must be a positive value.")]
    public decimal? MRP { get; set; }

    [Display(Name = "GST Rate")]
    public int? GstRateId { get; set; }

    // Stock Details
    [Display(Name = "Opening Stock")]
    [Range(0, double.MaxValue, ErrorMessage = "Opening stock must be a positive value.")]
    public decimal? OpeningStock { get; set; }

    [Display(Name = "Unit")]
    public int? UnitId { get; set; }

    [Display(Name = "As Of Date")]
    [DataType(DataType.Date)]
    public DateTime? AsOfDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SalesPrice.HasValue && PurchasePrice.HasValue && SalesPrice < PurchasePrice)
        {
            yield return new ValidationResult(
                "Sales price should typically be greater than or equal to purchase price.",
                new[] { nameof(SalesPrice) });
        }

        if (MRP.HasValue && SalesPrice.HasValue && SalesPrice > MRP)
        {
            yield return new ValidationResult(
                "Sales price cannot exceed MRP.",
                new[] { nameof(SalesPrice) });
        }
    }
}

public sealed class ProductEditRequest : ProductCreateRequest
{
    [Required]
    public Guid ProductId { get; set; }
}
