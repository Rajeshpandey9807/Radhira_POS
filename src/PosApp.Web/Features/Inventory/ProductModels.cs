using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.Inventory;

public sealed record LookupOption(int Id, string Name);

public sealed record ProductListItem(
    Guid ProductId,
    string ItemName,
    string? ItemCode,
    string ProductTypeName,
    string CategoryName,
    decimal? SalesPrice,
    decimal? CurrentStock,
    int? GstRateId,
    bool IsActive);

public sealed class ProductCreateRequest
{
    [Required(ErrorMessage = "Product type is required.")]
    [Display(Name = "Product Type")]
    public int? ProductTypeId { get; set; }

    [Required(ErrorMessage = "Category is required.")]
    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Item name is required.")]
    [StringLength(200, ErrorMessage = "Item name must be 200 characters or less.")]
    [Display(Name = "Item Name")]
    public string ItemName { get; set; } = string.Empty;

    [StringLength(80, ErrorMessage = "Item code must be 80 characters or less.")]
    [Display(Name = "Item Code")]
    public string? ItemCode { get; set; }

    [StringLength(50, ErrorMessage = "HSN code must be 50 characters or less.")]
    [Display(Name = "HSN Code")]
    public string? HSNCode { get; set; }

    [StringLength(800, ErrorMessage = "Description must be 800 characters or less.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "Sales price is required.")]
    [Range(0, 9999999999999999.99, ErrorMessage = "Sales price must be 0 or more.")]
    [Display(Name = "Sales Price")]
    public decimal? SalesPrice { get; set; }

    [Range(0, 9999999999999999.99, ErrorMessage = "Purchase price must be 0 or more.")]
    [Display(Name = "Purchase Price")]
    public decimal? PurchasePrice { get; set; }

    [Range(0, 9999999999999999.99, ErrorMessage = "MRP must be 0 or more.")]
    [Display(Name = "MRP")]
    public decimal? MRP { get; set; }

    [Required(ErrorMessage = "GST rate is required.")]
    [Range(0, 100, ErrorMessage = "GST rate must be between 0 and 100.")]
    [Display(Name = "GST Tax Rate (%)")]
    public int? GstRateId { get; set; }

    [Range(0, 9999999999999999.99, ErrorMessage = "Opening stock must be 0 or more.")]
    [Display(Name = "Opening Stock")]
    public decimal? OpeningStock { get; set; }

    [Display(Name = "Measuring Unit")]
    public int? UnitId { get; set; }

    [Display(Name = "As Of Date")]
    [DataType(DataType.Date)]
    public DateTime? AsOfDate { get; set; }
}

public sealed class ProductCreatePageModel
{
    public ProductCreateRequest Form { get; set; } = new();
    public IReadOnlyList<LookupOption> ProductTypes { get; set; } = Array.Empty<LookupOption>();
    public IReadOnlyList<LookupOption> Categories { get; set; } = Array.Empty<LookupOption>();
    public IReadOnlyList<LookupOption> Units { get; set; } = Array.Empty<LookupOption>();
}

