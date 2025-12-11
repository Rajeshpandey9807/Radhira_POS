using System;
using System.Reflection;
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
            await _registrationTypeService.CreateAsync(new RegistrationTypeInput(
                model.RegistrationTypeName,
                model.IsActive));

            TempData["ToastMessage"] = "Registration type added";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            ModelState.AddModelError(nameof(model.RegistrationTypeName), "Registration type already exists.");
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var details = await _registrationTypeService.GetByIdAsync(id);
        if (details is null)
        {
            return NotFound();
        }

        var model = new RegistrationTypeFormViewModel
        {
            Id = details.Id,
            RegistrationTypeName = details.RegistrationTypeName,
            IsActive = details.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, RegistrationTypeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _registrationTypeService.UpdateAsync(id, new RegistrationTypeInput(
                model.RegistrationTypeName,
                model.IsActive));

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
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _registrationTypeService.DeleteAsync(id);
        if (!deleted)
        {
            return NotFound();
        }

        TempData["ToastMessage"] = "Registration type deleted";
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
}
