using Microsoft.AspNetCore.Mvc;
using PosApp.Web.Features.Products;

namespace PosApp.Web.Controllers;

public class ItemsController : Controller
{
    private readonly ProductService _productService;

    public ItemsController(ProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Inventory(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Inventory";
        var products = await _productService.GetProductsAsync(cancellationToken);
        return View(products);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Add New Product";
        await LoadDropdownsAsync(cancellationToken);
        return View(new ProductCreateRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductCreateRequest request, string? saveAction, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync(cancellationToken);
            return View(request);
        }

        try
        {
            await _productService.CreateAsync(request, cancellationToken);
            TempData["ToastMessage"] = "Product created successfully.";

            // If "Save & New" was clicked, redirect back to Create
            if (!string.IsNullOrEmpty(saveAction) && saveAction.Equals("SaveAndNew", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Create));
            }

            return RedirectToAction(nameof(Inventory));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Error creating product: {ex.Message}");
            await LoadDropdownsAsync(cancellationToken);
            return View(request);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GenerateItemCode(CancellationToken cancellationToken)
    {
        var itemCode = await _productService.GenerateItemCodeAsync(cancellationToken);
        return Json(new { itemCode });
    }

    public IActionResult Warehouse()
    {
        ViewData["Title"] = "Godown (Warehouse)";
        return View();
    }

    private async Task LoadDropdownsAsync(CancellationToken cancellationToken)
    {
        ViewBag.ProductTypes = await _productService.GetProductTypesAsync(cancellationToken);
        ViewBag.Categories = await _productService.GetCategoriesAsync(cancellationToken);
        ViewBag.GstRates = await _productService.GetGstRatesAsync(cancellationToken);
        ViewBag.Units = await _productService.GetUnitsAsync(cancellationToken);
    }
}
