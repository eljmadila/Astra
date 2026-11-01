using System.Security.Claims;
using AspnetCoreMvcFull.Data;
using AspnetCoreMvcFull.Entities;
using AspnetCoreMvcFull.Services;
using AspnetCoreMvcFull.ViewModels.ProjectRoles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AspnetCoreMvcFull.Controllers;

[Authorize(Policy = "RequireAdminRole")]
public class ProjectRolesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<ProjectRolesController> _logger;

    public ProjectRolesController(
        ApplicationDbContext context,
        IAuditService auditService,
        ILogger<ProjectRolesController> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    // GET: /ProjectRoles
    public async Task<IActionResult> Index()
    {
        var roles = await _context.ProjectRoles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new ProjectRoleItemViewModel
            {
                Id = r.Id,
                Name = r.Name,
                IsActive = r.IsActive
            })
            .ToListAsync();

        var model = new ProjectRoleListViewModel
        {
            Roles = roles
        };

        return View(model);
    }

    // POST: /ProjectRoles/Create
    [HttpPost]
    public async Task<IActionResult> Create(ProjectRoleListViewModel model)
    {
        var roleName = model.NewRoleForm.Name?.Trim();
        if (string.IsNullOrWhiteSpace(roleName))
        {
            TempData["Error"] = "Project role name is required.";
            return RedirectToAction(nameof(Index));
        }

        var exists = await _context.ProjectRoles
            .AnyAsync(r => r.Name.ToLower() == roleName.ToLower());

        if (exists)
        {
            TempData["Error"] = $"A project role named '{roleName}' already exists (case-insensitive duplicate).";
            return RedirectToAction(nameof(Index));
        }

        var newRole = new ProjectRole
        {
            Name = roleName,
            IsActive = true
        };

        _context.ProjectRoles.Add(newRole);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Created",
            entity: "ProjectRole",
            entityId: newRole.Id.ToString(),
            details: new { newRole.Name, newRole.IsActive },
            userId: CurrentUserId
        );

        TempData["Success"] = $"Project role '{newRole.Name}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /ProjectRoles/Rename
    [HttpPost]
    public async Task<IActionResult> Rename(int id, string name)
    {
        var roleName = name?.Trim();
        if (string.IsNullOrWhiteSpace(roleName))
        {
            TempData["Error"] = "Project role name cannot be empty.";
            return RedirectToAction(nameof(Index));
        }

        var role = await _context.ProjectRoles.FindAsync(id);
        if (role is null)
        {
            return NotFound();
        }

        var duplicate = await _context.ProjectRoles
            .AnyAsync(r => r.Id != id && r.Name.ToLower() == roleName.ToLower());

        if (duplicate)
        {
            TempData["Error"] = $"A project role named '{roleName}' already exists.";
            return RedirectToAction(nameof(Index));
        }

        var oldName = role.Name;
        role.Name = roleName;
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Updated",
            entity: "ProjectRole",
            entityId: role.Id.ToString(),
            details: new { From = oldName, To = roleName },
            userId: CurrentUserId
        );

        TempData["Success"] = $"Role renamed from '{oldName}' to '{roleName}'.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /ProjectRoles/ToggleStatus/5
    [HttpPost]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var role = await _context.ProjectRoles.FindAsync(id);
        if (role is null)
        {
            return NotFound();
        }

        role.IsActive = !role.IsActive;
        await _context.SaveChangesAsync();

        var actionName = role.IsActive ? "Activated" : "Deactivated";
        await _auditService.LogAsync(
            action: actionName,
            entity: "ProjectRole",
            entityId: role.Id.ToString(),
            details: new { role.Name, role.IsActive },
            userId: CurrentUserId
        );

        TempData["Success"] = $"Role '{role.Name}' was {actionName.ToLower()} successfully.";
        return RedirectToAction(nameof(Index));
    }
}
