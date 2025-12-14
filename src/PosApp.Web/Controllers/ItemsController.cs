using Microsoft.AspNetCore.Mvc;
using PosApp.Web.Features.Items;

namespace PosApp.Web.Controllers;

public class ItemsController : Controller
{
    [HttpGet]
    public IActionResult Create()
    {
        return View(new ItemCreateRequest());
    }

    [HttpPost]
    public IActionResult Create(ItemCreateRequest model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        
        // TODO: Save item
        
        return RedirectToAction("Index", "Home");
    }
}
