using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PosApp.Web.Features.Inventory;

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
        var products = await _productService.GetInventoryAsync(cancellationToken);
        return View(products);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Add New Product";
        var model = await _productService.GetCreatePageAsync(cancellationToken: cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] ProductCreateRequest form, string? submitAction, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Add New Product";
            var model = await _productService.GetCreatePageAsync(form, cancellationToken);
            return View(model);
        }

        try
        {
            var productId = await _productService.CreateAsync(form, cancellationToken);
            TempData["ToastMessage"] = $"Product {form.ItemName.Trim()} saved";

            if (string.Equals(submitAction, "save-new", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Create));
            }

            return RedirectToAction(nameof(Inventory));
        }
        catch
        {
            ModelState.AddModelError(string.Empty, "Unable to save product right now. Please try again.");
            ViewData["Title"] = "Add New Product";
            var model = await _productService.GetCreatePageAsync(form, cancellationToken);
            return View(model);
        }
    }

    public IActionResult Warehouse()
    {
        ViewData["Title"] = "Godown (Warehouse)";
        return View();
    }
}
