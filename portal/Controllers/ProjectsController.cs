using System.Security.Claims;
using AspnetCoreMvcFull.Data;
using AspnetCoreMvcFull.Entities;
using AspnetCoreMvcFull.Services;
using AspnetCoreMvcFull.ViewModels.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AspnetCoreMvcFull.Controllers;

[Authorize]
public class ProjectsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IProjectScopingService _scopingService;
    private readonly IProjectCodeGenerator _codeGenerator;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        ApplicationDbContext context,
        IProjectScopingService scopingService,
        IProjectCodeGenerator codeGenerator,
        IAuditService auditService,
        UserManager<ApplicationUser> userManager,
        ILogger<ProjectsController> logger)
    {
        _context = context;
        _scopingService = scopingService;
        _codeGenerator = codeGenerator;
        _auditService = auditService;
        _userManager = userManager;
        _logger = logger;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    // GET: /Projects
    public async Task<IActionResult> Index(
        string? search,
        ProjectStatus? status,
        int? clientId,
        int? leadId,
        ProjectPriority? priority,
        DateOnly? startDateFrom,
        DateOnly? plannedEndBefore,
        string? archiveFilter = "active",
        string? sortBy = "code",
        string? sortOrder = "asc",
        int page = 1,
        int pageSize = 25)
    {
        var baseQuery = _context.Projects
            .Include(p => p.Lead)
            .Include(p => p.ProjectClients)
                .ThenInclude(pc => pc.Client)
            .AsNoTracking()
            .AsQueryable();

        // 1. Apply Permission-Based Scope
        var scopedQuery = _scopingService.ScopeProjects(baseQuery, User, CurrentUserId);

        // 2. Archived Filter (Active only by default)
        if (archiveFilter == "active")
        {
            scopedQuery = scopedQuery.Where(p => !p.IsArchived);
        }
        else if (archiveFilter == "archived")
        {
            scopedQuery = scopedQuery.Where(p => p.IsArchived);
        }
        // "all" displays both active and archived

        // 3. Search across Code, Name, Description
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            scopedQuery = scopedQuery.Where(p =>
                p.Code.Contains(s) ||
                p.Name.Contains(s) ||
                (p.Description != null && p.Description.Contains(s)));
        }

        // 4. Filters
        if (status.HasValue)
        {
            scopedQuery = scopedQuery.Where(p => p.Status == status.Value);
        }

        if (clientId.HasValue)
        {
            scopedQuery = scopedQuery.Where(p => p.ProjectClients.Any(pc => pc.ClientId == clientId.Value));
        }

        if (leadId.HasValue)
        {
            scopedQuery = scopedQuery.Where(p => p.LeadId == leadId.Value);
        }

        if (priority.HasValue)
        {
            scopedQuery = scopedQuery.Where(p => p.Priority == priority.Value);
        }

        if (startDateFrom.HasValue)
        {
            scopedQuery = scopedQuery.Where(p => p.StartDate >= startDateFrom.Value);
        }

        if (plannedEndBefore.HasValue)
        {
            scopedQuery = scopedQuery.Where(p => p.PlannedEndDate <= plannedEndBefore.Value);
        }

        // 5. Sorting
        scopedQuery = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
        {
            ("code", "desc") => scopedQuery.OrderByDescending(p => p.Code),
            ("code", _) => scopedQuery.OrderBy(p => p.Code),
            ("name", "desc") => scopedQuery.OrderByDescending(p => p.Name),
            ("name", _) => scopedQuery.OrderBy(p => p.Name),
            ("lead", "desc") => scopedQuery.OrderByDescending(p => p.Lead != null ? p.Lead.Name : ""),
            ("lead", _) => scopedQuery.OrderBy(p => p.Lead != null ? p.Lead.Name : ""),
            ("status", "desc") => scopedQuery.OrderByDescending(p => p.Status),
            ("status", _) => scopedQuery.OrderBy(p => p.Status),
            ("priority", "desc") => scopedQuery.OrderByDescending(p => p.Priority),
            ("priority", _) => scopedQuery.OrderBy(p => p.Priority),
            ("planned_end", "desc") => scopedQuery.OrderByDescending(p => p.PlannedEndDate),
            ("planned_end", _) => scopedQuery.OrderBy(p => p.PlannedEndDate),
            ("progress", "desc") => scopedQuery.OrderByDescending(p => p.ProgressPercent),
            ("progress", _) => scopedQuery.OrderBy(p => p.ProgressPercent),
            _ => scopedQuery.OrderBy(p => p.Code)
        };

        var totalItems = await scopedQuery.CountAsync();

        // 6. Server-Side Pagination
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 5, 100);

        var projectsRaw = await scopedQuery
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var projectItems = projectsRaw.Select(p =>
        {
            var isOverdue = p.Status != ProjectStatus.Completed &&
                            p.Status != ProjectStatus.Cancelled &&
                            p.PlannedEndDate < today;

            return new ProjectListItemViewModel
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                ClientNames = p.ProjectClients.Select(pc => pc.Client.Name).ToList(),
                LeadId = p.LeadId,
                LeadName = p.Lead?.Name ?? "Unassigned",
                LeadEmail = p.Lead?.Email ?? "",
                Status = p.Status,
                Priority = p.Priority,
                ProgressMode = p.ProgressMode,
                ProgressPercent = p.ProgressPercent,
                StartDate = p.StartDate,
                PlannedEndDate = p.PlannedEndDate,
                ActualEndDate = p.ActualEndDate,
                IsOverdue = isOverdue,
                DaysOverdue = isOverdue ? (today.DayNumber - p.PlannedEndDate.DayNumber) : 0,
                IsArchived = p.IsArchived,
                CanEdit = _scopingService.CanUserEditProject(p, User, CurrentUserId),
                CanArchive = _scopingService.CanUserArchiveProject(p, User, CurrentUserId)
            };
        }).ToList();

        // Dropdown Filter Options
        var clientOptions = await _context.Clients
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();

        var leadOptions = await GetEligibleLeadsAsync();

        var model = new ProjectListViewModel
        {
            Projects = projectItems,
            Search = search,
            Status = status,
            ClientId = clientId,
            LeadId = leadId,
            Priority = priority,
            StartDateFrom = startDateFrom,
            PlannedEndBefore = plannedEndBefore,
            ArchiveFilter = archiveFilter,
            SortBy = sortBy,
            SortOrder = sortOrder,
            Page = safePage,
            PageSize = safePageSize,
            TotalItems = totalItems,
            ClientOptions = clientOptions,
            LeadOptions = leadOptions
        };

        return View(model);
    }

    // GET: /Projects/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var project = await _context.Projects
            .Include(p => p.Lead)
            .Include(p => p.CreatedBy)
            .Include(p => p.ProjectClients)
                .ThenInclude(pc => pc.Client)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project is null)
        {
            return NotFound();
        }

        // Spec Requirement: Return 404 for projects a user may not see (IDOR prevention)
        if (!_scopingService.CanUserViewProject(project, User, CurrentUserId))
        {
            return NotFound();
        }

        // Retrieve Recent Audit Logs for this project
        var auditLogs = await _context.AuditLogs
            .Include(a => a.User)
            .Where(a => a.Entity == "Project" && a.EntityId == id.ToString())
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .ToListAsync();

        var model = new ProjectDetailsViewModel
        {
            Project = project,
            CanEdit = _scopingService.CanUserEditProject(project, User, CurrentUserId),
            CanArchive = _scopingService.CanUserArchiveProject(project, User, CurrentUserId),
            RecentAuditLogs = auditLogs
        };

        return View(model);
    }

    // GET: /Projects/Create
    [Authorize(Policy = "RequireManagerRole")]
    public async Task<IActionResult> Create()
    {
        var autoCode = await _codeGenerator.GenerateNextCodeAsync();
        var isAdmin = User.IsInRole("Admin");

        var model = new ProjectFormViewModel
        {
            Code = autoCode,
            IsCodeEditable = isAdmin,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PlannedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
            Status = ProjectStatus.NotStarted,
            Priority = ProjectPriority.Medium,
            ProgressMode = ProgressMode.Manual,
            ProgressPercent = 0,
            LeadId = CurrentUserId ?? 0,
            ClientOptions = await GetClientSelectListAsync(),
            LeadOptions = await GetEligibleLeadsAsync()
        };

        return View(model);
    }

    // POST: /Projects/Create
    [HttpPost]
    [Authorize(Policy = "RequireManagerRole")]
    public async Task<IActionResult> Create(ProjectFormViewModel model)
    {
        var isAdmin = User.IsInRole("Admin");
        model.IsCodeEditable = isAdmin;

        // If non-admin manager, force suggested code if empty or unprivileged
        if (!isAdmin && string.IsNullOrWhiteSpace(model.Code))
        {
            model.Code = await _codeGenerator.GenerateNextCodeAsync();
        }

        // Validate unique code
        if (!string.IsNullOrWhiteSpace(model.Code))
        {
            var codeExists = await _context.Projects.AnyAsync(p => p.Code == model.Code.Trim());
            if (codeExists)
            {
                ModelState.AddModelError("Code", $"Project code '{model.Code}' is already taken. Please choose or generate another code.");
            }
        }

        // Validate lead eligibility (must be active Admin or Manager)
        var leadUser = await _context.Users.FindAsync(model.LeadId);
        if (leadUser is null || !leadUser.IsActive)
        {
            ModelState.AddModelError("LeadId", "The assigned project lead must be an active user.");
        }
        else
        {
            var isLeadEligible = await _userManager.IsInRoleAsync(leadUser, "Admin") ||
                                 await _userManager.IsInRoleAsync(leadUser, "Manager");
            if (!isLeadEligible)
            {
                ModelState.AddModelError("LeadId", "Only users with the Admin or Manager role may be assigned as project leads.");
            }
        }

        // Spec Requirement: Completed status implies 100% progress
        if (model.Status == ProjectStatus.Completed && model.ProgressPercent < 100)
        {
            model.ProgressPercent = 100;
        }

        // Spec Requirement: Completed or Cancelled requires Actual End Date
        if (model.Status is ProjectStatus.Completed or ProjectStatus.Cancelled && !model.ActualEndDate.HasValue)
        {
            model.ActualEndDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        if (!ModelState.IsValid)
        {
            model.ClientOptions = await GetClientSelectListAsync();
            model.LeadOptions = await GetEligibleLeadsAsync();
            return View(model);
        }

        var currentUserId = CurrentUserId ?? model.LeadId;

        var project = new Project
        {
            Code = model.Code.Trim(),
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            LeadId = model.LeadId,
            Status = model.Status,
            Priority = model.Priority,
            ProgressMode = model.ProgressMode,
            ProgressPercent = model.ProgressPercent,
            StartDate = model.StartDate,
            PlannedEndDate = model.PlannedEndDate,
            ActualEndDate = model.ActualEndDate,
            IsArchived = false,
            CreatedById = currentUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (model.SelectedClientIds.Count > 0)
        {
            foreach (var clientId in model.SelectedClientIds.Distinct())
            {
                project.ProjectClients.Add(new ProjectClient
                {
                    ClientId = clientId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Created",
            entity: "Project",
            entityId: project.Id.ToString(),
            details: new
            {
                project.Code,
                project.Name,
                project.LeadId,
                project.Status,
                project.Priority,
                Clients = model.SelectedClientIds
            },
            userId: currentUserId
        );

        TempData["Success"] = $"Project '{project.Code} — {project.Name}' created successfully.";
        return RedirectToAction(nameof(Details), new { id = project.Id });
    }

    // GET: /Projects/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var project = await _context.Projects
            .Include(p => p.ProjectClients)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project is null)
        {
            return NotFound();
        }

        // 1. Check Visibility (404 if not visible)
        if (!_scopingService.CanUserViewProject(project, User, CurrentUserId))
        {
            return NotFound();
        }

        // 2. Check Mutability (403 if visible but forbidden)
        if (!_scopingService.CanUserEditProject(project, User, CurrentUserId))
        {
            return Forbid();
        }

        var isAdmin = User.IsInRole("Admin");

        var model = new ProjectFormViewModel
        {
            Id = project.Id,
            Code = project.Code,
            IsCodeEditable = isAdmin,
            Name = project.Name,
            Description = project.Description,
            LeadId = project.LeadId,
            SelectedClientIds = project.ProjectClients.Select(pc => pc.ClientId).ToList(),
            Status = project.Status,
            Priority = project.Priority,
            ProgressMode = project.ProgressMode,
            ProgressPercent = project.ProgressPercent,
            StartDate = project.StartDate,
            PlannedEndDate = project.PlannedEndDate,
            ActualEndDate = project.ActualEndDate,
            RowVersion = Convert.ToBase64String(project.RowVersion),
            ClientOptions = await GetClientSelectListAsync(),
            LeadOptions = await GetEligibleLeadsAsync()
        };

        return View(model);
    }

    // POST: /Projects/Edit/5
    [HttpPost]
    public async Task<IActionResult> Edit(int id, ProjectFormViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var project = await _context.Projects
            .Include(p => p.ProjectClients)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project is null)
        {
            return NotFound();
        }

        if (!_scopingService.CanUserViewProject(project, User, CurrentUserId))
        {
            return NotFound();
        }

        if (!_scopingService.CanUserEditProject(project, User, CurrentUserId))
        {
            return Forbid();
        }

        var isAdmin = User.IsInRole("Admin");
        model.IsCodeEditable = isAdmin;

        // If admin altered code, validate unique
        if (isAdmin && model.Code != project.Code)
        {
            var codeExists = await _context.Projects.AnyAsync(p => p.Id != id && p.Code == model.Code.Trim());
            if (codeExists)
            {
                ModelState.AddModelError("Code", $"Project code '{model.Code}' is already in use.");
            }
        }

        // Validate lead eligibility
        var leadUser = await _context.Users.FindAsync(model.LeadId);
        if (leadUser is null || !leadUser.IsActive)
        {
            ModelState.AddModelError("LeadId", "The assigned project lead must be an active user.");
        }
        else
        {
            var isLeadEligible = await _userManager.IsInRoleAsync(leadUser, "Admin") ||
                                 await _userManager.IsInRoleAsync(leadUser, "Manager");
            if (!isLeadEligible)
            {
                ModelState.AddModelError("LeadId", "Only Admins and Managers may be assigned as project leads.");
            }
        }

        // Spec Requirement: Reopening a Completed project clears actual end date and logs it
        var wasCompleted = project.Status == ProjectStatus.Completed;
        var isNowReopened = wasCompleted && model.Status != ProjectStatus.Completed && model.Status != ProjectStatus.Cancelled;

        if (isNowReopened)
        {
            model.ActualEndDate = null;
        }

        // Completed offers to set 100%
        if (model.Status == ProjectStatus.Completed && model.ProgressPercent < 100)
        {
            model.ProgressPercent = 100;
        }

        // Completed or Cancelled requires Actual End Date
        if (model.Status is ProjectStatus.Completed or ProjectStatus.Cancelled && !model.ActualEndDate.HasValue)
        {
            model.ActualEndDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        if (!ModelState.IsValid)
        {
            model.ClientOptions = await GetClientSelectListAsync();
            model.LeadOptions = await GetEligibleLeadsAsync();
            return View(model);
        }

        try
        {
            if (!string.IsNullOrEmpty(model.RowVersion))
            {
                _context.Entry(project).Property(p => p.RowVersion).OriginalValue = Convert.FromBase64String(model.RowVersion);
            }

            if (isAdmin)
            {
                project.Code = model.Code.Trim();
            }

            project.Name = model.Name.Trim();
            project.Description = model.Description?.Trim();
            project.LeadId = model.LeadId;
            project.Status = model.Status;
            project.Priority = model.Priority;
            project.ProgressMode = model.ProgressMode;
            project.ProgressPercent = model.ProgressPercent;
            project.StartDate = model.StartDate;
            project.PlannedEndDate = model.PlannedEndDate;
            project.ActualEndDate = model.ActualEndDate;
            project.UpdatedAt = DateTime.UtcNow;

            // Sync Client Linkages
            var currentClientIds = project.ProjectClients.Select(pc => pc.ClientId).ToHashSet();
            var newClientIds = (model.SelectedClientIds ?? []).Distinct().ToHashSet();

            // Remove unselected
            var toRemove = project.ProjectClients.Where(pc => !newClientIds.Contains(pc.ClientId)).ToList();
            foreach (var pc in toRemove)
            {
                project.ProjectClients.Remove(pc);
            }

            // Add newly selected
            foreach (var cid in newClientIds.Where(cid => !currentClientIds.Contains(cid)))
            {
                project.ProjectClients.Add(new ProjectClient
                {
                    ProjectId = project.Id,
                    ClientId = cid,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            // Log special audit if reopened
            if (isNowReopened)
            {
                await _auditService.LogAsync(
                    action: "Reopened",
                    entity: "Project",
                    entityId: project.Id.ToString(),
                    details: new { Message = "Project reopened from Completed. Actual end date cleared.", NewStatus = project.Status },
                    userId: CurrentUserId
                );
            }

            await _auditService.LogAsync(
                action: "Updated",
                entity: "Project",
                entityId: project.Id.ToString(),
                details: new
                {
                    project.Code,
                    project.Name,
                    project.LeadId,
                    project.Status,
                    project.Priority,
                    project.ProgressPercent,
                    Clients = newClientIds
                },
                userId: CurrentUserId
            );

            TempData["Success"] = $"Project '{project.Code}' updated successfully.";
            return RedirectToAction(nameof(Details), new { id = project.Id });
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrency conflict detected while editing Project Id {ProjectId}", id);

            // Fetch current database state to display side-by-side friendly conflict resolution
            var currentDbProject = await _context.Projects
                .Include(p => p.Lead)
                .Include(p => p.ProjectClients)
                    .ThenInclude(pc => pc.Client)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            var conflictVm = new ProjectConflictViewModel
            {
                ProjectId = id,
                ProjectCode = project.Code,
                ProjectName = project.Name,
                SubmittedModel = model,
                CurrentDatabaseProject = currentDbProject ?? project
            };

            return View("ConcurrencyConflict", conflictVm);
        }
    }

    // POST: /Projects/Archive/5
    [HttpPost]
    public async Task<IActionResult> Archive(int id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project is null)
        {
            return NotFound();
        }

        if (!_scopingService.CanUserViewProject(project, User, CurrentUserId))
        {
            return NotFound();
        }

        if (!_scopingService.CanUserArchiveProject(project, User, CurrentUserId))
        {
            return Forbid();
        }

        project.IsArchived = true;
        project.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Archived",
            entity: "Project",
            entityId: project.Id.ToString(),
            details: new { project.Code, project.Name, IsArchived = true },
            userId: CurrentUserId
        );

        TempData["Success"] = $"Project '{project.Code}' archived successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Projects/Restore/5
    [HttpPost]
    public async Task<IActionResult> Restore(int id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project is null)
        {
            return NotFound();
        }

        if (!_scopingService.CanUserViewProject(project, User, CurrentUserId))
        {
            return NotFound();
        }

        if (!_scopingService.CanUserArchiveProject(project, User, CurrentUserId))
        {
            return Forbid();
        }

        project.IsArchived = false;
        project.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Restored",
            entity: "Project",
            entityId: project.Id.ToString(),
            details: new { project.Code, project.Name, IsArchived = false },
            userId: CurrentUserId
        );

        TempData["Success"] = $"Project '{project.Code}' restored from archive.";
        return RedirectToAction(nameof(Details), new { id = project.Id });
    }

    #region Private Helpers

    private async Task<List<SelectListItem>> GetClientSelectListAsync()
    {
        return await _context.Clients
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetEligibleLeadsAsync()
    {
        // Eligible leads: active users in Manager or Admin roles
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        var managerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Manager");

        var eligibleUserIds = new HashSet<int>();
        if (adminRole != null)
        {
            var adminIds = await _context.UserRoles
                .Where(ur => ur.RoleId == adminRole.Id)
                .Select(ur => ur.UserId)
                .ToListAsync();
            eligibleUserIds.UnionWith(adminIds);
        }

        if (managerRole != null)
        {
            var managerIds = await _context.UserRoles
                .Where(ur => ur.RoleId == managerRole.Id)
                .Select(ur => ur.UserId)
                .ToListAsync();
            eligibleUserIds.UnionWith(managerIds);
        }

        return await _context.Users
            .Where(u => u.IsActive && eligibleUserIds.Contains(u.Id))
            .OrderBy(u => u.Name)
            .Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = $"{u.Name} ({u.Department})"
            })
            .ToListAsync();
    }

    #endregion
}
