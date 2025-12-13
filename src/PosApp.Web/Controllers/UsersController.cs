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
                model.FullName.Trim(),
                model.Email.Trim(),
                model.MobileNumber.Trim(),
                model.RoleId,
                model.Password));
            TempData["ToastMessage"] = $"User {model.FullName} created";
            return RedirectToAction(nameof(Index));
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            ModelState.AddModelError(nameof(model.Email), "Email already exists. Choose another one.");
            await PopulateRolesAsync(model);
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        var details = await _userService.GetDetailsAsync(id);
        if (details is null)
        {
            return NotFound();
        }

        var model = await PopulateRolesAsync(new UserFormViewModel
        {
            UserId = details.UserId,
            IsActive = details.IsActive,
            FullName = details.FullName,
            Email = details.Email,
            MobileNumber = details.MobileNumber,
            RoleId = details.RoleId
        });

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UserFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateRolesAsync(model);
            return View(model);
        }

        await _userService.UpdateAsync(id, new UserInput(
            model.FullName,
            model.Email,
            model.MobileNumber,
            model.RoleId,
            model.Password));

        TempData["ToastMessage"] = $"User {model.FullName} updated";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool activate)
    {
        var updated = await _userService.SetStatusAsync(id, activate);
        if (!updated)
        {
            return NotFound();
        }

        TempData["ToastMessage"] = activate ? "User activated" : "User deactivated";
        return RedirectToAction(nameof(Index));
    }

    private async Task<UserFormViewModel> PopulateRolesAsync(UserFormViewModel model)
    {
        var roles = await _userService.GetRoleOptionsAsync();
        model.Roles = roles;
        if (model.RoleId == 0 && roles.Any())
        {
            model.RoleId = roles.First().Id;
        }
        return model;
    }
}
