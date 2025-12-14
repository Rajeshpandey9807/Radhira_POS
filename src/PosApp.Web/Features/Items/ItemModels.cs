using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.Items;

public enum ItemType
{
    Product = 1,
    Service = 2
}

public class ItemCreateRequest
{
    [Required]
    [Display(Name = "Item Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "SKU")]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Unit Price")]
    public decimal UnitPrice { get; set; }

    [Required]
    [Display(Name = "Item Type")]
    public ItemType ItemType { get; set; } = ItemType.Product;
}
