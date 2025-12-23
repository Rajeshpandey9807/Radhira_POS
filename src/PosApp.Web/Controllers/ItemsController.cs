using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PosApp.Web.Features.Products;

namespace PosApp.Web.Controllers;

public class ItemsController : Controller
{
    private readonly ProductService _productService;
    private readonly ILogger<ItemsController> _logger;

    public ItemsController(ProductService productService, ILogger<ItemsController> logger)
    {
        _productService = productService;
        _logger = logger;
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
        try
        {
            _logger.LogInformation("Loading lookup data for dropdowns");
            var productTypes = await _productService.GetProductTypesAsync(cancellationToken);
            var categories = await _productService.GetCategoriesAsync(cancellationToken);
            var units = await _productService.GetUnitsAsync(cancellationToken);
            var gstRates = await _productService.GetGstRatesAsync(cancellationToken);
            
            _logger.LogInformation("Lookup data loaded successfully: {ProductTypeCount} types, {CategoryCount} categories, {UnitCount} units, {GstRateCount} rates", 
                productTypes.Count, categories.Count, units.Count, gstRates.Count);
            
            return Ok(new { productTypes, categories, units, gstRates });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading lookup data: {Message}", ex.Message);
            
            // Return detailed error for debugging (in production, you might want to hide some details)
            return StatusCode(500, new { 
                ok = false, 
                message = "Unable to load dropdown data. Please ensure the database is properly initialized.",
                error = ex.Message,
                innerException = ex.InnerException?.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GenerateBarcode(CancellationToken cancellationToken)
    {
        var barcode = await _productService.GenerateBarcodeAsync(cancellationToken);
        return Ok(new { barcode });
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
