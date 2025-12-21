using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
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

    public IActionResult Create()
    {
        ViewData["Title"] = "Add New Product";
        return View(new ProductCreateRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductCreateRequest model, string? submitAction, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Add New Product";
            if (WantsJson())
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Please fix the highlighted fields.",
                    errors = ToErrorDictionary()
                });
            }

            return View(model);
        }

        try
        {
            var productId = await _productService.CreateAsync(model, createdBy: GetActorId(), cancellationToken: cancellationToken);

            if (WantsJson())
            {
                return Ok(new
                {
                    ok = true,
                    productId,
                    message = $"Product {model.ItemName.Trim()} saved."
                });
            }

            TempData["ToastMessage"] = $"Product {model.ItemName.Trim()} saved";
            if (string.Equals(submitAction, "save-new", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Create));
            }

            return RedirectToAction(nameof(Inventory));
        }
        catch (Exception ex)
        {
            if (WantsJson())
            {
                return StatusCode(500, new { ok = false, message = "Unable to save product right now. Please try again.", error = ex.Message });
            }

            ModelState.AddModelError(string.Empty, "Unable to save product right now. Please try again.");
            ViewData["Title"] = "Add New Product";
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var model = await _productService.GetProductForEditAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Edit Product";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ProductEditRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit Product";
            if (WantsJson())
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Please fix the highlighted fields.",
                    errors = ToErrorDictionary()
                });
            }

            return View(model);
        }

        try
        {
            await _productService.UpdateAsync(id, model, updatedBy: GetActorId(), cancellationToken: cancellationToken);

            if (WantsJson())
            {
                return Ok(new { ok = true, productId = id, message = $"Product {model.ItemName.Trim()} updated." });
            }

            TempData["ToastMessage"] = $"Product {model.ItemName.Trim()} updated";
            return RedirectToAction(nameof(Inventory));
        }
        catch
        {
            if (WantsJson())
            {
                return StatusCode(500, new { ok = false, message = "Unable to update product right now. Please try again." });
            }

            ModelState.AddModelError(string.Empty, "Unable to update product right now. Please try again.");
            ViewData["Title"] = "Edit Product";
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Lookups(CancellationToken cancellationToken)
    {
        var productTypes = await _productService.GetProductTypesAsync(cancellationToken);
        var categories = await _productService.GetCategoriesAsync(cancellationToken);
        var units = await _productService.GetUnitsAsync(cancellationToken);
        var gstRates = await _productService.GetGstRatesAsync(cancellationToken);
        return Ok(new { productTypes, categories, units, gstRates });
    }

    public IActionResult Warehouse()
    {
        ViewData["Title"] = "Godown (Warehouse)";
        return View();
    }

    private bool WantsJson()
    {
        var accept = Request.Headers["Accept"].ToString();
        return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
               || (!string.IsNullOrWhiteSpace(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase));
    }

    private object ToErrorDictionary()
    {
        return ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
    }

    private int GetActorId()
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(idValue, out var actorId))
            {
                return actorId;
            }
        }

        return 0;
    }
}
