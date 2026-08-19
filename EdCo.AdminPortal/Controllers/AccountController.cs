using EdCo.AdminPortal.Models.Account;
using EdCo.Core.Entities;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EdCo.AdminPortal.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IEmailSenderService _emailSender;
        private readonly ILogger<AccountController> _logger;
        private readonly IAuditLogService _auditLogService;

        public AccountController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IEmailSenderService emailSender,
            ILogger<AccountController> logger,
            IAuditLogService auditLogService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = logger;
            _auditLogService = auditLogService;
        }

        private string GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            // Check password
            var result = await _signInManager.PasswordSignInAsync(user.UserName ?? model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {Email} logged in successfully.", model.Email);

                var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");
                await _auditLogService.LogAdminActionAsync(
                    action: "AdminLogin",
                    entityName: "AppUser",
                    entityId: user.Id,
                    details: $"Admin '{user.Email}' logged in successfully",
                    userId: user.Id,
                    userName: user.Email,
                    userRole: isSuperAdmin ? "SuperAdmin" : "Admin",
                    ipAddress: GetClientIp());

                return RedirectToLocal(returnUrl);
            }

            if (result.RequiresTwoFactor)
            {
                // Generate and send Email OTP token
                var provider = "Email";
                var code = await _userManager.GenerateTwoFactorTokenAsync(user, provider);
                await _emailSender.SendEmailAsync(
                    user.Email!,
                    "Your EdCo Security Code",
                    $"<div style='font-family:sans-serif;padding:20px;'><h2 style='color:#6366f1;'>EdCo Security Code</h2><p>Your OTP code for signing in to EdCo Admin Portal is: <strong style='font-size:24px;color:#1e1b4b;'>{code}</strong></p><p>This code will expire shortly.</p></div>"
                );

                return RedirectToAction(nameof(VerifyEmailOtp), new { RememberMe = model.RememberMe, ReturnUrl = returnUrl });
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account {Email} locked out.", model.Email);
                ModelState.AddModelError(string.Empty, "User account locked out. Please try again later.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // GET: /Account/VerifyEmailOtp
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmailOtp(bool rememberMe, string? returnUrl = null)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            return View(new TwoFactorViewModel { RememberMe = rememberMe, ReturnUrl = returnUrl });
        }

        // POST: /Account/VerifyEmailOtp
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmailOtp(TwoFactorViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var result = await _signInManager.TwoFactorSignInAsync("Email", model.Code, model.RememberMe, rememberClient: false);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {UserId} logged in with Email 2FA OTP.", user.Id);

                var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");
                await _auditLogService.LogAdminActionAsync(
                    action: "AdminLogin2FA",
                    entityName: "AppUser",
                    entityId: user.Id,
                    details: $"Admin '{user.Email}' completed 2FA login",
                    userId: user.Id,
                    userName: user.Email,
                    userRole: isSuperAdmin ? "SuperAdmin" : "Admin",
                    ipAddress: GetClientIp());

                return RedirectToLocal(model.ReturnUrl);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "User account locked out due to multiple invalid OTP attempts.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Invalid security code. Please check your email and try again.");
            return View(model);
        }

        // POST: /Account/ResendEmailOtp
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailOtp()
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return Json(new { success = false, message = "Session expired. Please log in again." });
            }

            var code = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");
            await _emailSender.SendEmailAsync(
                user.Email!,
                "Your EdCo Security Code",
                $"<div style='font-family:sans-serif;padding:20px;'><h2 style='color:#6366f1;'>EdCo Security Code</h2><p>Your new OTP code for signing in to EdCo Admin Portal is: <strong style='font-size:24px;color:#1e1b4b;'>{code}</strong></p></div>"
            );

            return Json(new { success = true, message = "A new security code has been sent to your email." });
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userId = _userManager.GetUserId(User);
            var userName = User.Identity?.Name ?? "Admin";

            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");

            await _auditLogService.LogAdminActionAsync(
                action: "AdminLogout",
                entityName: "AppUser",
                entityId: userId,
                details: $"Admin '{userName}' logged out",
                userId: userId,
                userName: userName,
                userRole: User.IsInRole("SuperAdmin") ? "SuperAdmin" : "Admin",
                ipAddress: GetClientIp());

            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // GET: /Account/ForgotPassword
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                // Don't reveal that the user does not exist
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action(nameof(ResetPassword), "Account", new { token, email = user.Email }, Request.Scheme);

            await _emailSender.SendEmailAsync(
                model.Email,
                "Reset Password - EdCo Admin",
                $"Please reset your password by <a href='{callbackUrl}'>clicking here</a>."
            );

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        // GET: /Account/ForgotPasswordConfirmation
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        // GET: /Account/ResetPassword
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string token, string email)
        {
            if (token == null || email == null)
            {
                return BadRequest("A token and email must be provided for password reset.");
            }

            return View(new ResetPasswordViewModel { Token = token, Email = email });
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // GET: /Account/ResetPasswordConfirmation
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Dashboard");
        }
    }
}
