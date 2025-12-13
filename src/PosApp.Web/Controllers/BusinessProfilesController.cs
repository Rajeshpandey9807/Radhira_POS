using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PosApp.Web.Features.BusinessProfiles;
using PosApp.Web.Features.BusinessTypes;
using PosApp.Web.Features.IndustryTypes;
using PosApp.Web.Features.RegistrationTypes;
using PosApp.Web.Features.States;

namespace PosApp.Web.Controllers;

public class BusinessProfilesController : Controller
{
    private readonly BusinessTypeService _businessTypeService;
    private readonly IndustryTypeService _industryTypeService;
    private readonly RegistrationTypeService _registrationTypeService;
    private readonly StateService _stateService;

    public BusinessProfilesController(
        BusinessTypeService businessTypeService,
        IndustryTypeService industryTypeService,
        RegistrationTypeService registrationTypeService,
        StateService stateService)
    {
        _businessTypeService = businessTypeService;
        _industryTypeService = industryTypeService;
        _registrationTypeService = registrationTypeService;
        _stateService = stateService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = await PopulateOptionsAsync(new BusinessProfileFormViewModel());
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(BusinessProfileFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model);
            return View(model);
        }

        // UI-only for now: persistence can be added once the BusinessProfile table/entity is defined.
        TempData["ToastMessage"] = "Business profile details captured (UI only).";

        await PopulateOptionsAsync(model);
        return View(model);
    }

    private async Task<BusinessProfileFormViewModel> PopulateOptionsAsync(BusinessProfileFormViewModel model)
    {
        var businessTypes = await _businessTypeService.GetAsync();
        model.BusinessTypes = businessTypes.Where(x => x.IsActive).ToList();

        var industryTypes = await _industryTypeService.GetAsync();
        model.IndustryTypes = industryTypes.Where(x => x.IsActive).ToList();

        var registrationTypes = await _registrationTypeService.GetAsync();
        model.RegistrationTypes = registrationTypes.Where(x => x.IsActive).ToList();

        var states = await _stateService.GetAsync();
        model.States = states.Where(x => x.IsActive).ToList();

        return model;
    }
}

