using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Features.Roles;

public sealed record RoleListItem(
    Guid Id,
    string Name,
    string Permissions,
    int AssignedUsers);

public sealed record RoleDetails(
    Guid Id,
    string Name,
    string Permissions);

public sealed record RoleInput(
    string Name,
    string Permissions);

public sealed class RoleFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Permissions (comma separated)")]
    [MaxLength(256)]
    public string Permissions { get; set; } = string.Empty;
}
