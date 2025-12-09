using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.Users;

public sealed record UserAccount(
    Guid Id,
    string Username,
    string DisplayName,
    string Email,
    string PhoneNumber,
    Guid RoleId,
    string RoleName,
    bool IsActive,
    DateTime CreatedAt);

public sealed record RoleOption(Guid Id, string Name);

public sealed record UserDetails(
    Guid Id,
    string Username,
    string DisplayName,
    string Email,
    string PhoneNumber,
    Guid RoleId,
    bool IsActive);

public sealed record UserInput(
    string Username,
    string DisplayName,
    string Email,
    string PhoneNumber,
    Guid RoleId,
    string? Password);

public sealed class UserFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [MinLength(3)]
    [MaxLength(32)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [Display(Name = "Phone number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    public Guid RoleId { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    public IEnumerable<RoleOption> Roles { get; set; } = Array.Empty<RoleOption>();
}
