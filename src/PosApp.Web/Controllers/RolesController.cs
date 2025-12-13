using Microsoft.AspNetCore.Mvc;
using PosApp.Web.Features.Roles;

namespace PosApp.Web.Controllers;

public class RolesController : Controller
{
    private readonly RoleMasterService _roleService;

    public RolesController(RoleMasterService roleService)
    {
        _roleService = roleService;
    }

    public async Task<IActionResult> Index()
    {
        var roles = await _roleService.GetAsync();
        return View(roles);
    }

    public IActionResult Create()
    {
        return View(new RoleFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RoleFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _roleService.CreateAsync(new RoleInput(
            model.Name.Trim(),
            model.Permissions?.Trim() ?? string.Empty));

        TempData["ToastMessage"] = $"Role {model.Name} created";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var role = await _roleService.GetByIdAsync(id);
        if (role is null)
        {
            return NotFound();
        }

        var model = new RoleFormViewModel
        {
            Id = role.Id,
            Name = role.Name,
            Permissions = role.Permissions
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RoleFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _roleService.UpdateAsync(id, new RoleInput(
            model.Name.Trim(),
            model.Permissions?.Trim() ?? string.Empty));

        TempData["ToastMessage"] = $"Role {model.Name} updated";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _roleService.DeleteAsync(id);
        switch (result)
        {
            case RoleDeleteResult.Success:
                TempData["ToastMessage"] = "Role deleted";
                break;
            case RoleDeleteResult.InUse:
                TempData["ToastMessage"] = "Role is assigned to users and cannot be deleted.";
                break;
            default:
                TempData["ToastMessage"] = "Role not found.";
                break;
        }

        return RedirectToAction(nameof(Index));
    }
}
