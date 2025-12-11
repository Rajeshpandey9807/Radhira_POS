using System;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PosApp.Web.Features.IndustryTypes;

namespace PosApp.Web.Controllers;

public class IndustryTypesController : Controller
{
    private readonly IndustryTypeService _industryTypeService;

    public IndustryTypesController(IndustryTypeService industryTypeService)
    {
        _industryTypeService = industryTypeService;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _industryTypeService.GetAsync();
        return View(items);
    }

    public IActionResult Create()
    {
        return View(new IndustryTypeFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IndustryTypeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _industryTypeService.CreateAsync(new IndustryTypeInput(model.IndustryTypeName), GetActorId());
            TempData["ToastMessage"] = "Industry type added";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            ModelState.AddModelError(nameof(model.IndustryTypeName), "Industry type already exists.");
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        var details = await _industryTypeService.GetByIdAsync(id);
        if (details is null)
        {
            return NotFound();
        }

        var model = new IndustryTypeFormViewModel
        {
            IndustryTypeId = details.IndustryTypeId,
            IndustryTypeName = details.IndustryTypeName
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, IndustryTypeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _industryTypeService.UpdateAsync(id, new IndustryTypeInput(model.IndustryTypeName), GetActorId());
            TempData["ToastMessage"] = "Industry type updated";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            ModelState.AddModelError(nameof(model.IndustryTypeName), "Industry type already exists.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool activate)
    {
        var success = await _industryTypeService.SetStatusAsync(id, activate, GetActorId());
        if (!success)
        {
            return NotFound();
        }

        TempData["ToastMessage"] = activate ? "Industry type activated" : "Industry type deactivated";
        return RedirectToAction(nameof(Index));
    }

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        if (exception is SqlException sqlException && (sqlException.Number == 2601 || sqlException.Number == 2627))
        {
            return true;
        }

        var exceptionTypeName = exception.GetType().FullName ?? string.Empty;
        if (exceptionTypeName.Contains("SqliteException", StringComparison.OrdinalIgnoreCase))
        {
            var property = exception.GetType().GetProperty("SqliteErrorCode", BindingFlags.Public | BindingFlags.Instance);
            if (property?.GetValue(exception) is int sqliteError && sqliteError == 19)
            {
                return true;
            }
        }

        return false;
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
