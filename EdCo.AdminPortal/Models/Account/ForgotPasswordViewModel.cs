using System.ComponentModel.DataAnnotations;

namespace EdCo.AdminPortal.Models.Account
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid email address format")]
        public string Email { get; set; } = string.Empty;
    }
}
