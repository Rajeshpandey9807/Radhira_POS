using Microsoft.AspNetCore.Mvc;
using PosApp.Web.Features.Parties;

namespace PosApp.Web.Controllers;

public class PartiesController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "Create party";
        return View(new PartyFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(PartyFormViewModel model, string? submitAction)
    {
        if (model.SameAsBilling)
        {
            model.ShippingAddress = model.BillingAddress;
            ModelState.Remove(nameof(model.ShippingAddress));
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Create party";
            return View(model);
        }

        // UI-only flow: persist will be wired up later.
        TempData["ToastMessage"] = $"Party {model.PartyName.Trim()} saved";

        if (string.Equals(submitAction, "save-new", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Create));
        }

        return RedirectToAction(nameof(Index));
    }
}

