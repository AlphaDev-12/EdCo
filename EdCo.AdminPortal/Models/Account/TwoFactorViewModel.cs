using System.ComponentModel.DataAnnotations;

namespace EdCo.AdminPortal.Models.Account
{
    public class TwoFactorViewModel
    {
        [Required(ErrorMessage = "OTP Code is required")]
        [StringLength(10, MinimumLength = 4, ErrorMessage = "Invalid OTP code length")]
        [Display(Name = "Security OTP Code")]
        public string Code { get; set; } = string.Empty;

        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
