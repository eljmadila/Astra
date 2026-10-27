using AspnetCoreMvcFull.Entities;
using AspnetCoreMvcFull.Services;
using AspnetCoreMvcFull.ViewModels.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AspnetCoreMvcFull.Controllers;

public class AuthController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        IEmailSender emailSender,
        ILogger<AuthController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditService = auditService;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View("~/Views/Auth/LoginBasic.cshtml", new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View("~/Views/Auth/LoginBasic.cshtml", model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            await _auditService.LogAsync("LoginFailed", "ApplicationUser", null, new { Reason = "UserNotFound", Email = model.Email });
            ModelState.AddModelError(string.Empty, "Invalid login credentials.");
            return View("~/Views/Auth/LoginBasic.cshtml", model);
        }

        if (!user.IsActive)
        {
            await _auditService.LogAsync("LoginBlocked", "ApplicationUser", user.Id.ToString(), new { Reason = "DeactivatedAccount", Email = model.Email }, user.Id);
            ModelState.AddModelError(string.Empty, "This corporate account has been deactivated. Please contact your portal administrator.");
            return View("~/Views/Auth/LoginBasic.cshtml", model);
        }

        var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            await _auditService.LogAsync("LoginSuccess", "ApplicationUser", user.Id.ToString(), new { Email = user.Email }, user.Id);

            if (user.MustChangePassword)
            {
                TempData["WarningMessage"] = "You must change your temporary password before accessing the portal.";
                return RedirectToAction(nameof(ChangePassword), new { forced = true, returnUrl });
            }

            TempData["SuccessMessage"] = $"Welcome back, {user.Name}!";
            return RedirectToLocal(returnUrl);
        }

        if (result.IsLockedOut)
        {
            await _auditService.LogAsync("AccountLockedOut", "ApplicationUser", user.Id.ToString(), new { Email = user.Email }, user.Id);
            ModelState.AddModelError(string.Empty, "Account locked due to 5 consecutive failed attempts. Please wait 15 minutes or contact support.");
            return View("~/Views/Auth/LoginBasic.cshtml", model);
        }

        await _auditService.LogAsync("LoginFailed", "ApplicationUser", user.Id.ToString(), new { Reason = "InvalidPassword", Email = model.Email }, user.Id);
        ModelState.AddModelError(string.Empty, "Invalid login credentials.");
        return View("~/Views/Auth/LoginBasic.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = _userManager.GetUserId(User);
        await _signInManager.SignOutAsync();

        if (int.TryParse(userId, out var id))
        {
            await _auditService.LogAsync("Logout", "ApplicationUser", userId, new { Note = "User logged out" }, id);
        }

        TempData["InfoMessage"] = "You have been logged out securely.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ChangePassword(bool forced = false, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View("~/Views/Auth/ChangePassword.cshtml", new ChangePasswordViewModel { IsForced = forced });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Auth/ChangePassword.cshtml", model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View("~/Views/Auth/ChangePassword.cshtml", model);
        }

        user.MustChangePassword = false;
        await _userManager.UpdateSecurityStampAsync(user);
        await _userManager.UpdateAsync(user);

        await _signInManager.RefreshSignInAsync(user);
        await _auditService.LogAsync("PasswordChanged", "ApplicationUser", user.Id.ToString(), new { Email = user.Email }, user.Id);

        TempData["SuccessMessage"] = "Your password has been updated successfully.";
        return RedirectToLocal(returnUrl);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View("~/Views/Auth/ForgotPasswordBasic.cshtml", new ForgotPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Auth/ForgotPasswordBasic.cshtml", model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is not null && user.IsActive)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action(nameof(ResetPassword), "Auth", new { token, email = user.Email }, Request.Scheme);

            await _emailSender.SendEmailAsync(
                user.Email!,
                "Reset your Astra Password",
                $"Hello {user.Name},<br/><br/>You requested a password reset. Click the link below to set a new password:<br/><br/><a href='{callbackUrl}'>Reset Password</a><br/><br/>This link will expire in 2 hours."
            );

            await _auditService.LogAsync("PasswordResetRequested", "ApplicationUser", user.Id.ToString(), new { Email = user.Email }, user.Id);
        }

        // Generic message regardless of whether user was found (prevent enumeration)
        TempData["SuccessMessage"] = "If an active account exists with that email, a password reset link has been dispatched.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? token = null, string? email = null)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
        {
            TempData["ErrorMessage"] = "Invalid password reset token.";
            return RedirectToAction(nameof(Login));
        }

        return View("~/Views/Auth/ResetPassword.cshtml", new ResetPasswordViewModel { Token = token, Email = email });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Auth/ResetPassword.cshtml", model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            TempData["SuccessMessage"] = "Password has been reset successfully. Please log in.";
            return RedirectToAction(nameof(Login));
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (result.Succeeded)
        {
            user.MustChangePassword = false;
            await _userManager.UpdateSecurityStampAsync(user);
            await _userManager.UpdateAsync(user);

            await _auditService.LogAsync("PasswordResetCompleted", "ApplicationUser", user.Id.ToString(), new { Email = user.Email }, user.Id);

            TempData["SuccessMessage"] = "Password has been reset successfully. Please sign in with your new credentials.";
            return RedirectToAction(nameof(Login));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
        return View("~/Views/Auth/ResetPassword.cshtml", model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View("~/Views/Shared/MiscError.cshtml");
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Dashboards");
    }
}
