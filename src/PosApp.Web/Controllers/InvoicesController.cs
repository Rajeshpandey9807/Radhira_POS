using Microsoft.AspNetCore.Mvc;

namespace PosApp.Web.Controllers;

public class InvoicesController : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Invoices";
        return View();
    }
}

