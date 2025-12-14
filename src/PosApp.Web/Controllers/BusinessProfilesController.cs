using System.Linq;
using System.Security.Claims;
using System.Threading;
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
    private readonly BusinessProfileService _businessProfileService;
    private readonly BusinessTypeService _businessTypeService;
    private readonly IndustryTypeService _industryTypeService;
    private readonly RegistrationTypeService _registrationTypeService;
    private readonly StateService _stateService;

    public BusinessProfilesController(
        BusinessProfileService businessProfileService,
        BusinessTypeService businessTypeService,
        IndustryTypeService industryTypeService,
        RegistrationTypeService registrationTypeService,
        StateService stateService)
    {
        _businessProfileService = businessProfileService;
        _businessTypeService = businessTypeService;
        _industryTypeService = industryTypeService;
        _registrationTypeService = registrationTypeService;
        _stateService = stateService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = await _businessProfileService.GetLatestAsync() ?? new BusinessProfileFormViewModel();
        await PopulateOptionsAsync(model);
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

        var savedBusinessId = await _businessProfileService.SaveAsync(model, GetActorId());
        model.BusinessId = savedBusinessId;
        TempData["ToastMessage"] = "Business profile saved.";

        await PopulateOptionsAsync(model);
        return View(model);
    }

    [HttpGet]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Logo(int businessId, CancellationToken cancellationToken)
    {
        var payload = await _businessProfileService.GetBusinessLogoAsync(businessId, cancellationToken);
        if (payload?.Data is null || payload.Data.Length == 0)
        {
            return NotFound();
        }

        return File(payload.Data, payload.ContentType ?? "application/octet-stream");
    }

    [HttpGet]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Signature(int businessId, CancellationToken cancellationToken)
    {
        var payload = await _businessProfileService.GetSignatureAsync(businessId, cancellationToken);
        if (payload?.Data is null || payload.Data.Length == 0)
        {
            return NotFound();
        }

        return File(payload.Data, payload.ContentType ?? "application/octet-stream");
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

