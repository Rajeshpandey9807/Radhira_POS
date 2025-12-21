using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.Products;

public sealed record ProductTypeOption(int ProductTypeId, string ProductTypeName);
public sealed record CategoryOption(int CategoryId, string CategoryName);
public sealed record UnitOption(int UnitId, string UnitName, string? UnitCode);
public sealed record GstRateOption(int GstRateId, decimal Rate, string? Description);

public sealed record CreateProductRequest
{
    [Required]
    [Display(Name = "Product Type")]
    public int ProductTypeId { get; set; }

    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Required]
    [Display(Name = "Item Name")]
    [StringLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [Display(Name = "Item Code")]
    [StringLength(100)]
    public string? ItemCode { get; set; }

    [Display(Name = "HSN Code")]
    [StringLength(50)]
    public string? HSNCode { get; set; }

    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Is Active")]
    public bool IsActive { get; set; } = true;

    // Pricing Details
    [Display(Name = "Sales Price")]
    public decimal? SalesPrice { get; set; }

    [Display(Name = "Purchase Price")]
    public decimal? PurchasePrice { get; set; }

    [Display(Name = "MRP")]
    public decimal? MRP { get; set; }

    [Display(Name = "GST Rate")]
    public int? GstRateId { get; set; }

    // Stock Details
    [Display(Name = "Opening Stock")]
    public decimal? OpeningStock { get; set; }

    [Display(Name = "Current Stock")]
    public decimal? CurrentStock { get; set; }

    [Display(Name = "Unit")]
    public int? UnitId { get; set; }

    [Display(Name = "As Of Date")]
    public DateTime? AsOfDate { get; set; }
}

public sealed record ProductViewModel
{
    public Guid ProductId { get; set; }
    public int ProductTypeId { get; set; }
    public string ProductTypeName { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? ItemCode { get; set; }
    public string? HSNCode { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public decimal? SalesPrice { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? MRP { get; set; }
    public int? GstRateId { get; set; }
    public decimal? Rate { get; set; }
    public decimal? OpeningStock { get; set; }
    public decimal? CurrentStock { get; set; }
    public int? UnitId { get; set; }
    public string? UnitName { get; set; }
    public DateTime? AsOfDate { get; set; }
    public DateTime CreatedOn { get; set; }
}
