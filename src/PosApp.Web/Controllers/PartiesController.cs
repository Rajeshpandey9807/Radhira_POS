using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PosApp.Web.Features.Parties;

namespace PosApp.Web.Controllers;

public class PartiesController : Controller
{
    private readonly PartyService _partyService;

    public PartiesController(PartyService partyService)
    {
        _partyService = partyService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var parties = await _partyService.GetPartiesAsync(cancellationToken);
        return View(parties);
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "Create party";
        return View(new PartyCreateRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PartyCreateRequest model, string? submitAction, CancellationToken cancellationToken)
    {
        if (model.SameAsBilling)
        {
            model.ShippingAddress = model.BillingAddress;
            ModelState.Remove(nameof(model.ShippingAddress));
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Create party";
            if (WantsJson())
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Please fix the highlighted fields.",
                    errors = ToErrorDictionary()
                });
            }

            return View(model);
        }

        try
        {
            var partyId = await _partyService.CreateAsync(model, createdBy: GetActorId(), cancellationToken: cancellationToken);

            if (WantsJson())
            {
                return Ok(new
                {
                    ok = true,
                    partyId,
                    message = $"Party {model.PartyName.Trim()} saved."
                });
            }

            TempData["ToastMessage"] = $"Party {model.PartyName.Trim()} saved";
            if (string.Equals(submitAction, "save-new", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Create));
            }

            return RedirectToAction(nameof(Index));
        }
        catch
        {
            if (WantsJson())
            {
                return StatusCode(500, new { ok = false, message = "Unable to save party right now. Please try again." });
            }

            ModelState.AddModelError(string.Empty, "Unable to save party right now. Please try again.");
            ViewData["Title"] = "Create party";
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var model = await _partyService.GetPartyForEditAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Edit party";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PartyEditRequest model, CancellationToken cancellationToken)
    {
        if (model.SameAsBilling)
        {
            model.ShippingAddress = model.BillingAddress;
            ModelState.Remove(nameof(model.ShippingAddress));
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit party";
            if (WantsJson())
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Please fix the highlighted fields.",
                    errors = ToErrorDictionary()
                });
            }

            return View(model);
        }

        try
        {
            await _partyService.UpdateAsync(id, model, updatedBy: GetActorId(), cancellationToken: cancellationToken);

            if (WantsJson())
            {
                return Ok(new { ok = true, partyId = id, message = $"Party {model.PartyName.Trim()} updated." });
            }

            TempData["ToastMessage"] = $"Party {model.PartyName.Trim()} updated";
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            if (WantsJson())
            {
                return StatusCode(500, new { ok = false, message = "Unable to update party right now. Please try again." });
            }

            ModelState.AddModelError(string.Empty, "Unable to update party right now. Please try again.");
            ViewData["Title"] = "Edit party";
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Lookups(CancellationToken cancellationToken)
    {
        var partyTypes = await _partyService.GetPartyTypesAsync(cancellationToken);
        var partyCategories = await _partyService.GetPartyCategoriesAsync(cancellationToken);
        return Ok(new { partyTypes, partyCategories });
    }

    [HttpGet]
    public IActionResult Import()
    {
        ViewData["Title"] = "Bulk upload parties";
        return View();
    }

    [HttpGet]
    public IActionResult ImportTemplate()
    {
        const string header =
            "PartyName,MobileNumber,Email,OpeningBalance,GSTIN,PANNumber,PartyType,PartyCategory,BillingAddress,ShippingAddress,CreditPeriod,CreditLimit,ContactPersonName,DateOfBirth,AccountNumber,IFSC,BranchName,AccountHolderName,UPI";
        var bytes = System.Text.Encoding.UTF8.GetBytes(header + "\n");
        return File(bytes, "text/csv", "party-upload-template.csv");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(Microsoft.AspNetCore.Http.IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { ok = false, message = "Please choose a CSV file." });
        }

        IReadOnlyList<PartyTypeOption> partyTypes;
        IReadOnlyList<PartyCategoryOption> partyCategories;
        try
        {
            partyTypes = await _partyService.GetPartyTypesAsync(cancellationToken);
            partyCategories = await _partyService.GetPartyCategoriesAsync(cancellationToken);
        }
        catch
        {
            return StatusCode(500, new { ok = false, message = "Unable to load master data. Please try again." });
        }

        var typeLookup = partyTypes.ToDictionary(x => x.TypeName.Trim(), x => x.PartyTypeId, StringComparer.OrdinalIgnoreCase);
        var categoryLookup = partyCategories.ToDictionary(x => x.CategoryName.Trim(), x => x.PartyCategoryId, StringComparer.OrdinalIgnoreCase);

        var results = new System.Collections.Generic.List<object>();
        var created = 0;
        var failed = 0;

        using var stream = file.OpenReadStream();
        using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: false);

        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return BadRequest(new { ok = false, message = "CSV file is empty." });
        }

        var headers = ParseCsvLine(headerLine).Select(h => (h ?? string.Empty).Trim()).ToList();
        var headerIndex = headers
            .Select((name, idx) => new { name, idx })
            .ToDictionary(x => x.name, x => x.idx, StringComparer.OrdinalIgnoreCase);

        string? line;
        var rowNumber = 1; // header row
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cols = ParseCsvLine(line);
            string Get(string name)
            {
                if (!headerIndex.TryGetValue(name, out var idx)) return string.Empty;
                return idx >= 0 && idx < cols.Count ? (cols[idx] ?? string.Empty).Trim() : string.Empty;
            }

            var errors = new System.Collections.Generic.List<string>();

            var partyName = Get("PartyName");
            if (string.IsNullOrWhiteSpace(partyName))
            {
                errors.Add("PartyName is required.");
            }

            var typeName = Get("PartyType");
            if (string.IsNullOrWhiteSpace(typeName) || !typeLookup.TryGetValue(typeName, out var partyTypeId))
            {
                errors.Add("PartyType is invalid.");
                partyTypeId = 0;
            }

            var categoryName = Get("PartyCategory");
            if (string.IsNullOrWhiteSpace(categoryName) || !categoryLookup.TryGetValue(categoryName, out var partyCategoryId))
            {
                errors.Add("PartyCategory is invalid.");
                partyCategoryId = 0;
            }

            static decimal? ParseDecimal(string value)
                => decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
            static int? ParseInt(string value)
                => int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i) ? i : null;
            static System.DateTime? ParseDate(string value)
                => System.DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var dt) ? dt.Date : null;

            var req = new PartyCreateRequest
            {
                PartyName = partyName,
                MobileNumber = Get("MobileNumber"),
                Email = Get("Email"),
                OpeningBalance = ParseDecimal(Get("OpeningBalance")),
                GSTIN = Get("GSTIN"),
                PANNumber = Get("PANNumber"),
                PartyTypeId = partyTypeId == 0 ? null : partyTypeId,
                PartyCategoryId = partyCategoryId == 0 ? null : partyCategoryId,
                BillingAddress = Get("BillingAddress"),
                ShippingAddress = Get("ShippingAddress"),
                CreditPeriod = ParseInt(Get("CreditPeriod")),
                CreditLimit = ParseDecimal(Get("CreditLimit")),
                ContactPersonName = Get("ContactPersonName"),
                DateOfBirth = ParseDate(Get("DateOfBirth")),
                AccountNumber = Get("AccountNumber"),
                ReEnterAccountNumber = Get("AccountNumber"),
                IFSC = Get("IFSC"),
                BranchName = Get("BranchName"),
                AccountHolderName = Get("AccountHolderName"),
                UPI = Get("UPI")
            };

            // Run model validation too (GSTIN length, account match, required fields).
            var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(req);
            var validationResults = new System.Collections.Generic.List<System.ComponentModel.DataAnnotations.ValidationResult>();
            if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(req, ctx, validationResults, validateAllProperties: true))
            {
                errors.AddRange(validationResults.Select(v => v.ErrorMessage ?? "Invalid value."));
            }

            if (errors.Count > 0)
            {
                failed++;
                results.Add(new { row = rowNumber, partyName, ok = false, errors });
                continue;
            }

            try
            {
                var newId = await _partyService.CreateAsync(req, createdBy: GetActorId(), cancellationToken: cancellationToken);
                created++;
                results.Add(new { row = rowNumber, partyName, ok = true, partyId = newId });
            }
            catch
            {
                failed++;
                results.Add(new { row = rowNumber, partyName, ok = false, errors = new[] { "Database insert failed." } });
            }
        }

        return Ok(new
        {
            ok = true,
            message = $"Imported {created} parties. {failed} failed.",
            created,
            failed,
            results
        });
    }

    private static System.Collections.Generic.List<string?> ParseCsvLine(string line)
    {
        // Minimal CSV parsing with quoted fields support.
        var result = new System.Collections.Generic.List<string?>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            sb.Append(c);
        }

        result.Add(sb.ToString());
        return result;
    }

    private bool WantsJson()
    {
        var accept = Request.Headers["Accept"].ToString();
        return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
               || (!string.IsNullOrWhiteSpace(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase));
    }

    private object ToErrorDictionary()
    {
        return ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
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

