using Microsoft.AspNetCore.Mvc;

namespace PosApp.Web.Controllers;

public class PaymentsController : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Payments";
        return View();
    }
}

