using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PosApp.Web.Features.Parties;

namespace PosApp.Web.Controllers;

public class PartiesController : Controller
{
    private readonly PartyService _partyService;

    public PartiesController(PartyService partyService)
    {
        _partyService = partyService;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "Create party";
        return View(new PartyCreateRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PartyCreateRequest model, string? submitAction, CancellationToken cancellationToken)
    {
        if (model.SameAsBilling)
        {
            model.ShippingAddress = model.BillingAddress;
            ModelState.Remove(nameof(model.ShippingAddress));
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Create party";
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
            var partyId = await _partyService.CreateAsync(model, createdBy: 0, cancellationToken: cancellationToken);

            if (WantsJson())
            {
                return Ok(new
                {
                    ok = true,
                    partyId,
                    message = $"Party {model.PartyName.Trim()} saved."
                });
            }

            TempData["ToastMessage"] = $"Party {model.PartyName.Trim()} saved";
            if (string.Equals(submitAction, "save-new", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Create));
            }

            return RedirectToAction(nameof(Index));
        }
        catch
        {
            if (WantsJson())
            {
                return StatusCode(500, new { ok = false, message = "Unable to save party right now. Please try again." });
            }

            ModelState.AddModelError(string.Empty, "Unable to save party right now. Please try again.");
            ViewData["Title"] = "Create party";
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Lookups(CancellationToken cancellationToken)
    {
        var partyTypes = await _partyService.GetPartyTypesAsync(cancellationToken);
        var partyCategories = await _partyService.GetPartyCategoriesAsync(cancellationToken);
        return Ok(new { partyTypes, partyCategories });
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
}

