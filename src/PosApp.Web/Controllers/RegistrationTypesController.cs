using System;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PosApp.Web.Features.RegistrationTypes;

namespace PosApp.Web.Controllers;

public class RegistrationTypesController : Controller
{
    private readonly RegistrationTypeService _registrationTypeService;

    public RegistrationTypesController(RegistrationTypeService registrationTypeService)
    {
        _registrationTypeService = registrationTypeService;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _registrationTypeService.GetAsync();
        return View(items);
    }

    public IActionResult Create()
    {
        return View(new RegistrationTypeFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RegistrationTypeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _registrationTypeService.CreateAsync(new RegistrationTypeInput(model.RegistrationTypeName), GetActorId());

            TempData["ToastMessage"] = "Registration type added";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            ModelState.AddModelError(nameof(model.RegistrationTypeName), "Registration type already exists.");
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        var details = await _registrationTypeService.GetByIdAsync(id);
        if (details is null)
        {
            return NotFound();
        }

        var model = new RegistrationTypeFormViewModel
        {
            RegistrationTypeId = details.RegistrationTypeId,
            RegistrationTypeName = details.RegistrationTypeName
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RegistrationTypeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _registrationTypeService.UpdateAsync(id, new RegistrationTypeInput(model.RegistrationTypeName), GetActorId());

            TempData["ToastMessage"] = "Registration type updated";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            ModelState.AddModelError(nameof(model.RegistrationTypeName), "Registration type already exists.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool activate)
    {
        var success = await _registrationTypeService.SetStatusAsync(id, activate, GetActorId());
        if (!success)
        {
            return NotFound();
        }

        TempData["ToastMessage"] = activate ? "Registration type activated" : "Registration type deactivated";
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
