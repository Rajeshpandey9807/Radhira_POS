using System;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PosApp.Web.Features.States;

namespace PosApp.Web.Controllers;

public class StatesController : Controller
{
    private readonly StateService _stateService;

    public StatesController(StateService stateService)
    {
        _stateService = stateService;
    }

    public async Task<IActionResult> Index()
    {
        var states = await _stateService.GetAsync();
        return View(states);
    }

    public IActionResult Create()
    {
        return View(new StateFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StateFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _stateService.CreateAsync(new StateInput(model.StateName), GetActorId());
            TempData["ToastMessage"] = "State added";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            ModelState.AddModelError(nameof(model.StateName), "State already exists.");
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        var details = await _stateService.GetByIdAsync(id);
        if (details is null)
        {
            return NotFound();
        }

        var model = new StateFormViewModel
        {
            StateId = details.StateId,
            StateName = details.StateName
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StateFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _stateService.UpdateAsync(id, new StateInput(model.StateName), GetActorId());
            TempData["ToastMessage"] = "State updated";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            ModelState.AddModelError(nameof(model.StateName), "State already exists.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool activate)
    {
        var success = await _stateService.SetStatusAsync(id, activate, GetActorId());
        if (!success)
        {
            return NotFound();
        }

        TempData["ToastMessage"] = activate ? "State activated" : "State deactivated";
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
