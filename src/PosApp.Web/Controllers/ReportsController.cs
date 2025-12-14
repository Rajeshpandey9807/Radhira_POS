using Microsoft.AspNetCore.Mvc;

namespace PosApp.Web.Controllers;

public class ReportsController : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Reports";
        return View();
    }
}

