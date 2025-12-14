using Microsoft.AspNetCore.Mvc;

namespace PosApp.Web.Controllers;

public class SalesController : Controller
{
    public IActionResult Quotation()
    {
        ViewData["Title"] = "Quotation / Estimate";
        return View();
    }

    public IActionResult SalesReturn()
    {
        ViewData["Title"] = "Sales Return";
        return View();
    }

    public IActionResult CreditNote()
    {
        ViewData["Title"] = "Credit Note";
        return View();
    }

    public IActionResult DeliveryChallan()
    {
        ViewData["Title"] = "Delivery Challan";
        return View();
    }

    public IActionResult ProformaInvoice()
    {
        ViewData["Title"] = "Proforma Invoice";
        return View();
    }
}
