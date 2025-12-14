using Microsoft.AspNetCore.Mvc;

namespace PosApp.Web.Controllers;

public class ItemsController : Controller
{
    public IActionResult Inventory()
    {
        ViewData["Title"] = "Inventory";
        return View();
    }

    public IActionResult Warehouse()
    {
        ViewData["Title"] = "Godown (Warehouse)";
        return View();
    }
}
