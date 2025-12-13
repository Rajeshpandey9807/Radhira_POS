using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PosApp.Web.Data;
using PosApp.Web.Models;
using PosApp.Web.Security;

namespace PosApp.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AccountController(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var identifier = model.Identifier.Trim();
        var password = model.Password;

        var user = await TryFindUserAsync(identifier);
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Invalid username/email or password.");
            return View(model);
        }

        if (!PasswordUtility.VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
        {
            ModelState.AddModelError(string.Empty, "Invalid username/email or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email)
        };

        if (!string.IsNullOrWhiteSpace(user.RoleName))
        {
            claims.Add(new Claim(ClaimTypes.Role, user.RoleName));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(12)
            });

        TempData["ToastMessage"] = $"Welcome back, {user.FullName}.";
        return RedirectToLocal(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["ToastMessage"] = "Signed out.";
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    private sealed record LoginUserRow(
        string UserId,
        string FullName,
        string Email,
        bool IsActive,
        string RoleName,
        string PasswordHash,
        string PasswordSalt);

    private async Task<LoginUserRow?> TryFindUserAsync(string identifier)
    {
        System.Data.IDbConnection connection;
        try
        {
            connection = await _connectionFactory.CreateConnectionAsync();
        }
        catch
        {
            return null;
        }

        using (connection)
        {
            // SQL Server schema (int identity Users.UserId).
            const string sqlServerSql = @"
SELECT TOP 1
    CAST(u.UserId AS nvarchar(50)) AS UserId,
    u.FullName AS FullName,
    u.Email AS Email,
    u.IsActive AS IsActive,
    COALESCE(r.Name, '') AS RoleName,
    a.PasswordHash AS PasswordHash,
    a.PasswordSalt AS PasswordSalt
FROM Users u
INNER JOIN UserAuth a ON a.UserId = u.UserId
LEFT JOIN UserRoles ur ON ur.UserId = u.UserId
LEFT JOIN Roles r ON r.Id = ur.RoleId
WHERE (u.Email = @Identifier OR u.MobileNumber = @Identifier);";

            try
            {
                var sqlServerRow = await connection.QuerySingleOrDefaultAsync<LoginUserRow>(sqlServerSql, new { Identifier = identifier });
                if (sqlServerRow is not null)
                {
                    return sqlServerRow;
                }
            }
            catch
            {
                // Fall back to SQLite schema below.
            }

            // SQLite schema (TEXT ids Users.Id + Users.Username).
            const string sqliteSql = @"
SELECT
    u.Id AS UserId,
    u.DisplayName AS FullName,
    u.Email AS Email,
    u.IsActive AS IsActive,
    COALESCE(r.Name, '') AS RoleName,
    a.PasswordHash AS PasswordHash,
    a.PasswordSalt AS PasswordSalt
FROM Users u
INNER JOIN UserAuth a ON a.UserId = u.Id
LEFT JOIN UserRoles ur ON ur.UserId = u.Id
LEFT JOIN Roles r ON r.Id = ur.RoleId
WHERE (u.Username = @Identifier OR u.Email = @Identifier)
LIMIT 1;";

            try
            {
                return await connection.QuerySingleOrDefaultAsync<LoginUserRow>(sqliteSql, new { Identifier = identifier });
            }
            catch
            {
                return null;
            }
        }
    }
}

