using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Models;

public class ProductViewModel
{
    [Required]
    public string ProductType { get; set; } = default!;

    [Required]
    public string Category { get; set; } = default!;

    [Required]
    public string ItemName { get; set; } = default!;

    [Required]
    public decimal SalesPrice { get; set; }

    [Required]
    public decimal GstRate { get; set; }

    [Required]
    public string Unit { get; set; } = default!;

    public decimal? OpeningStock { get; set; }

    public string? ItemCode { get; set; }
    public string? HSNCode { get; set; }

    public string? UnitAdvanced { get; set; }
    public DateTime? AsOfDate { get; set; }
    public string? Description { get; set; }

    public decimal? SalesPriceAdvanced { get; set; }
    public decimal? Mrp { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? GstRateAdvanced { get; set; }
}
