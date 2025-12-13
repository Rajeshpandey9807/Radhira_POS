using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.Users;

public sealed record UserAccount(
    int UserId,
    string FullName,
    string Email,
    string MobileNumber,
    int RoleId,
    string RoleName,
    bool IsActive,
    DateTime CreatedOn);

public sealed record RoleOption(int Id, string Name);

public sealed record UserDetails(
    int UserId,
    string FullName,
    string Email,
    string MobileNumber,
    int RoleId,
    bool IsActive);

public sealed record UserInput(
    string FullName,
    string Email,
    string MobileNumber,
    int RoleId,
    string? Password);

public sealed class UserFormViewModel
{
    public int? UserId { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    [Display(Name = "Full name")]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [Display(Name = "Mobile number")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Role is required.")]
    public int RoleId { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    public IEnumerable<RoleOption> Roles { get; set; } = Array.Empty<RoleOption>();
}
