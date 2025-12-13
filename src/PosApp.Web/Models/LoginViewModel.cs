using System.ComponentModel.DataAnnotations;

namespace PosApp.Web.Models;

public sealed class LoginViewModel
{
    [Required]
    [Display(Name = "Email or username")]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}

