using AspnetCoreMvcFull.Entities;
using AspnetCoreMvcFull.Services;
using AspnetCoreMvcFull.ViewModels.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AspnetCoreMvcFull.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IAuditService _auditService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IAuditService auditService,
        IEmailSender emailSender,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        string? roleFilter = null,
        bool? activeFilter = null,
        int pageNumber = 1,
        int pageSize = 15)
    {
        var query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(u => u.Name.ToLower().Contains(searchLower)
                                  || u.Email!.ToLower().Contains(searchLower)
                                  || u.Department.ToLower().Contains(searchLower));
        }

        if (activeFilter.HasValue)
        {
            query = query.Where(u => u.IsActive == activeFilter.Value);
        }

        var totalCount = await query.CountAsync();

        var usersList = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userItems = new List<UserListItemViewModel>();

        foreach (var user in usersList)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userItems.Add(new UserListItemViewModel
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email ?? string.Empty,
                Department = user.Department,
                Role = roles.FirstOrDefault() ?? "None",
                IsActive = user.IsActive,
                MustChangePassword = user.MustChangePassword,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            });
        }

        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            userItems = userItems.Where(u => u.Role.Equals(roleFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var availableRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

        var viewModel = new UserIndexViewModel
        {
            Users = userItems,
            Search = search,
            RoleFilter = roleFilter,
            ActiveFilter = activeFilter,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            AvailableRoles = availableRoles
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "A corporate account with this email address already exists.");
            return View(model);
        }

        var tempPassword = !string.IsNullOrWhiteSpace(model.InitialPassword)
            ? model.InitialPassword
            : $"Astra@{Guid.NewGuid().ToString("N")[..8]}!Aa1";

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            Name = model.Name,
            Department = model.Department,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            model.Role = "Employee";
        }
        await _userManager.AddToRoleAsync(user, model.Role);

        // Send Set-Password invitation email
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var setupUrl = Url.Action("ResetPassword", "Auth", new { token = resetToken, email = user.Email }, Request.Scheme);

        await _emailSender.SendEmailAsync(
            user.Email,
            "Welcome to Astra — Set Up Your Account",
            $"Hello {user.Name},<br/><br/>An Astra corporate account has been created for you.<br/>Department: {user.Department}<br/>Role: {model.Role}<br/><br/>Click the link below to set your password and access the portal:<br/><a href='{setupUrl}'>Set My Password</a>"
        );

        await _auditService.LogAsync("UserCreated", "ApplicationUser", user.Id.ToString(), new
        {
            Email = user.Email,
            Name = user.Name,
            Department = user.Department,
            Role = model.Role
        });

        TempData["SuccessMessage"] = $"User {user.Name} ({user.Email}) was created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        var roles = await _userManager.GetRolesAsync(user);
        ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

        return View(new EditUserViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Name = user.Name,
            Department = user.Department,
            Role = roles.FirstOrDefault() ?? "Employee",
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.Id.ToString());
        if (user is null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var currentRole = currentRoles.FirstOrDefault() ?? "Employee";

        // Prevent removing the last active Admin
        if (currentRole == "Admin" && model.Role != "Admin")
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var activeAdmins = admins.Count(a => a.IsActive && a.Id != user.Id);
            if (activeAdmins == 0)
            {
                ModelState.AddModelError(string.Empty, "Cannot reassign the role: This user is the only active System Administrator.");
                return View(model);
            }
        }

        user.Name = model.Name;
        user.Department = model.Department;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        if (currentRole != model.Role)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, model.Role);
            await _userManager.UpdateSecurityStampAsync(user); // Force session re-evaluation

            await _auditService.LogAsync("RoleChanged", "ApplicationUser", user.Id.ToString(), new
            {
                Email = user.Email,
                OldRole = currentRole,
                NewRole = model.Role
            });
        }

        TempData["SuccessMessage"] = $"User {user.Name} details updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        // Prevent deactivating the last active Admin
        if (user.IsActive && await _userManager.IsInRoleAsync(user, "Admin"))
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var activeAdmins = admins.Count(a => a.IsActive && a.Id != user.Id);
            if (activeAdmins == 0)
            {
                TempData["ErrorMessage"] = "Action Blocked: You cannot deactivate the only active Administrator account.";
                return RedirectToAction(nameof(Index));
            }
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateSecurityStampAsync(user); // Invalidate any existing active session immediately
        await _userManager.UpdateAsync(user);

        var actionText = user.IsActive ? "UserReactivated" : "UserDeactivated";
        await _auditService.LogAsync(actionText, "ApplicationUser", user.Id.ToString(), new
        {
            Email = user.Email,
            IsActive = user.IsActive
        });

        TempData["SuccessMessage"] = $"User {user.Name} is now {(user.IsActive ? "Active" : "Deactivated")}.";
        return RedirectToAction(nameof(Index));
    }
}
