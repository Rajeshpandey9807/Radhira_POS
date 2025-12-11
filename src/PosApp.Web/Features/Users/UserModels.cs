using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.Users;

public sealed record UserAccount(
    Guid Id,
    string FullName,
    string Email,
    string MobileNumber,
    string RoleName,
    bool IsActive,
    DateTime CreatedOn);

public sealed record RoleOption(Guid Id, string Name);

public sealed record UserDetails(
    Guid Id,
    string FullName,
    string Email,
    string MobileNumber,
    Guid? RoleId,
    bool IsActive);

public sealed record UserInput(
    string FullName,
    string Email,
    string MobileNumber,
    Guid RoleId,
    string? Password);

public sealed class UserFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [Display(Name = "Mobile number")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required]
    public Guid RoleId { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    public IEnumerable<RoleOption> Roles { get; set; } = Array.Empty<RoleOption>();
}
