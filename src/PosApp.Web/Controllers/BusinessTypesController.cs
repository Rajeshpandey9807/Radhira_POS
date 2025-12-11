using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PosApp.Web.Features.BusinessTypes;

namespace PosApp.Web.Controllers;

public class BusinessTypesController : Controller
{
    private readonly BusinessTypeService _businessTypeService;

    public BusinessTypesController(BusinessTypeService businessTypeService)
    {
        _businessTypeService = businessTypeService;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _businessTypeService.GetAsync();
        return View(items);
    }

    public IActionResult Create()
    {
        return View(new BusinessTypeFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BusinessTypeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _businessTypeService.CreateAsync(new BusinessTypeInput(
                model.IndustryTypeName,
                model.IsActive));

            TempData["ToastMessage"] = "Business type added";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            ModelState.AddModelError(nameof(model.IndustryTypeName), "Industry type already exists.");
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var details = await _businessTypeService.GetByIdAsync(id);
        if (details is null)
        {
            return NotFound();
        }

        var model = new BusinessTypeFormViewModel
        {
            BusinessTypeId = details.Id,
            IndustryTypeName = details.IndustryTypeName,
            IsActive = details.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, BusinessTypeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _businessTypeService.UpdateAsync(id, new BusinessTypeInput(
                model.IndustryTypeName,
                model.IsActive));

            TempData["ToastMessage"] = "Business type updated";
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
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _businessTypeService.DeleteAsync(id);
        if (!deleted)
        {
            return NotFound();
        }

        TempData["ToastMessage"] = "Business type deleted";
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
