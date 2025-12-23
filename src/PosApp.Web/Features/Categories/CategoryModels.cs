using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.Categories;

public sealed class CategoryListItem
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool IsActive { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
}

public sealed class CategoryDetails
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool IsActive { get; set; }
}

public sealed record CategoryInput(string CategoryName, string? Color);

public sealed class CategoryFormViewModel
{
    public Guid? CategoryId { get; set; }

    [Required]
    [MaxLength(200)]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(50)]
    [Display(Name = "Color")]
    public string? Color { get; set; }
}

