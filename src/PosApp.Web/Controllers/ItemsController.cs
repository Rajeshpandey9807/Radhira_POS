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
        var products = await _productService.GetAllProductsAsync(cancellationToken);
        return View(products);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Add New Product";
        
        var productTypes = await _productService.GetProductTypesAsync(cancellationToken);
        var categories = await _productService.GetCategoriesAsync(cancellationToken);
        var units = await _productService.GetUnitsAsync(cancellationToken);
        var gstRates = await _productService.GetGstRatesAsync(cancellationToken);

        ViewData["ProductTypes"] = productTypes;
        ViewData["Categories"] = categories;
        ViewData["Units"] = units;
        ViewData["GstRates"] = gstRates;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProductRequest request, string? action, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var productTypes = await _productService.GetProductTypesAsync(cancellationToken);
            var categories = await _productService.GetCategoriesAsync(cancellationToken);
            var units = await _productService.GetUnitsAsync(cancellationToken);
            var gstRates = await _productService.GetGstRatesAsync(cancellationToken);

            ViewData["ProductTypes"] = productTypes;
            ViewData["Categories"] = categories;
            ViewData["Units"] = units;
            ViewData["GstRates"] = gstRates;
            return View(request);
        }

        try
        {
            await _productService.CreateProductAsync(request, cancellationToken);
            TempData["ToastMessage"] = $"Product '{request.ItemName}' created successfully.";
            
            if (action == "saveAndNew")
            {
                return RedirectToAction(nameof(Create));
            }
            
            return RedirectToAction(nameof(Inventory));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Error creating product: {ex.Message}");
            var productTypes = await _productService.GetProductTypesAsync(cancellationToken);
            var categories = await _productService.GetCategoriesAsync(cancellationToken);
            var units = await _productService.GetUnitsAsync(cancellationToken);
            var gstRates = await _productService.GetGstRatesAsync(cancellationToken);

            ViewData["ProductTypes"] = productTypes;
            ViewData["Categories"] = categories;
            ViewData["Units"] = units;
            ViewData["GstRates"] = gstRates;
            return View(request);
        }
    }

    public IActionResult Warehouse()
    {
        ViewData["Title"] = "Godown (Warehouse)";
        return View();
    }
}
