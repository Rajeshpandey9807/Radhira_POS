using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PosApp.Web.Features.Users;

namespace PosApp.Web.Controllers;

public class UsersController : Controller
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userService.GetUsersAsync();
        return View(users);
    }

    public async Task<IActionResult> Create()
    {
        var model = await PopulateRolesAsync(new UserFormViewModel());
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Password is required.");
        }
        else if (model.Password.Length < 6)
        {
            ModelState.AddModelError(nameof(model.Password), "Password must be at least 6 characters.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateRolesAsync(model);
            return View(model);
        }

        try
        {
            await _userService.CreateAsync(new UserInput(
                model.Username.Trim(),
                model.DisplayName.Trim(),
                model.Email.Trim(),
                model.PhoneNumber.Trim(),
                model.RoleId,
                model.Password));
            TempData["ToastMessage"] = $"User {model.DisplayName} invited";
            return RedirectToAction(nameof(Index));
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            ModelState.AddModelError(nameof(model.Username), "Username already exists. Choose another one.");
            await PopulateRolesAsync(model);
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var details = await _userService.GetDetailsAsync(id);
        if (details is null)
        {
            return NotFound();
        }

        var model = await PopulateRolesAsync(new UserFormViewModel
        {
            Id = details.Id,
            Username = details.Username,
            DisplayName = details.DisplayName,
            Email = details.Email,
            PhoneNumber = details.PhoneNumber,
            RoleId = details.RoleId
        });

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, UserFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateRolesAsync(model);
            return View(model);
        }

        await _userService.UpdateAsync(id, new UserInput(
            model.Username,
            model.DisplayName,
            model.Email,
            model.PhoneNumber,
            model.RoleId,
            model.Password));

        TempData["ToastMessage"] = $"User {model.DisplayName} updated";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id)
    {
        await _userService.ToggleStatusAsync(id);
        TempData["ToastMessage"] = "User status updated";
        return RedirectToAction(nameof(Index));
    }

    private async Task<UserFormViewModel> PopulateRolesAsync(UserFormViewModel model)
    {
        var roles = await _userService.GetRoleOptionsAsync();
        model.Roles = roles;
        if (model.RoleId == Guid.Empty && roles.Any())
        {
            model.RoleId = roles.First().Id;
        }
        return model;
    }
}
