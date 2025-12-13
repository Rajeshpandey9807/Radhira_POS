using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.Users;

public sealed record UserAccount(
    int Id,
    string Username,
    string DisplayName,
    string Email,
    string PhoneNumber,
    int RoleId,
    string RoleName,
    bool IsActive,
    DateTime CreatedAt);

public sealed record RoleOption(int Id, string Name);

public sealed record UserDetails(
    int Id,
    string Username,
    string DisplayName,
    string Email,
    string PhoneNumber,
    int RoleId,
    bool IsActive);

public sealed record UserInput(
    string Username,
    string DisplayName,
    string Email,
    string PhoneNumber,
    int RoleId,
    string? Password);

public sealed class UserFormViewModel
{
    public int? Id { get; set; }

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
    [Range(1, int.MaxValue, ErrorMessage = "Role is required.")]
    public int RoleId { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    public IEnumerable<RoleOption> Roles { get; set; } = Array.Empty<RoleOption>();
}
