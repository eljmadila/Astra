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
    private readonly ITeamAssignmentService _teamAssignmentService;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        ApplicationDbContext context,
        IProjectScopingService scopingService,
        IProjectCodeGenerator codeGenerator,
        ITeamAssignmentService teamAssignmentService,
        IAuditService auditService,
        UserManager<ApplicationUser> userManager,
        ILogger<ProjectsController> logger)
    {
        _context = context;
        _scopingService = scopingService;
        _codeGenerator = codeGenerator;
        _teamAssignmentService = teamAssignmentService;
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
            .Include(p => p.ProjectAssignments)
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
    public async Task<IActionResult> Details(
        int id,
        string? tab = "overview",
        bool showPast = false,
        DateOnly? asOfDate = null)
    {
        var project = await _context.Projects
            .Include(p => p.Lead)
            .Include(p => p.CreatedBy)
            .Include(p => p.ProjectClients)
                .ThenInclude(pc => pc.Client)
            .Include(p => p.ProjectAssignments)
                .ThenInclude(pa => pa.User)
            .Include(p => p.ProjectAssignments)
                .ThenInclude(pa => pa.RoleOnProject)
            .Include(p => p.ProjectAssignments)
                .ThenInclude(pa => pa.CreatedBy)
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

        var activeTab = (!string.IsNullOrWhiteSpace(tab) && tab.Equals("team", StringComparison.OrdinalIgnoreCase)) ||
                        showPast ||
                        asOfDate.HasValue
            ? "team"
            : "overview";

        var currentTeam = project.ProjectAssignments
            .Where(pa => pa.EndDate == null)
            .OrderByDescending(pa => pa.UserId == project.LeadId)
            .ThenBy(pa => pa.StartDate)
            .ThenBy(pa => pa.User.Name)
            .ToList();

        var pastMembers = project.ProjectAssignments
            .Where(pa => pa.EndDate != null)
            .OrderByDescending(pa => pa.EndDate)
            .ThenBy(pa => pa.User.Name)
            .ToList();

        List<ProjectAssignment>? pointInTimeTeam = null;
        if (asOfDate.HasValue)
        {
            pointInTimeTeam = await _teamAssignmentService.GetTeamOnAsync(id, asOfDate.Value);
        }

        var activeUserIds = currentTeam.Select(pa => pa.UserId).ToHashSet();

        // Only active users can be assigned; deactivated users hidden from pickers by default
        var availableEmployees = await _context.Users
            .AsNoTracking()
            .Where(u => u.IsActive && !activeUserIds.Contains(u.Id))
            .OrderBy(u => u.Name)
            .Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = $"{u.Name} ({u.Department})"
            })
            .ToListAsync();

        var activeProjectRoles = await _context.ProjectRoles
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name == "Lead" ? 0 : 1)
            .ThenBy(r => r.Name)
            .Select(r => new SelectListItem
            {
                Value = r.Id.ToString(),
                Text = r.Name
            })
            .ToListAsync();

        var auditLogs = await _context.AuditLogs
            .Include(a => a.User)
            .Where(a => a.Entity == "Project" && a.EntityId == id.ToString())
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .ToListAsync();

        var requiresOverride = project.IsArchived ||
                               project.Status == ProjectStatus.Completed ||
                               project.Status == ProjectStatus.Cancelled;

        var model = new ProjectDetailsViewModel
        {
            Project = project,
            CanEdit = _scopingService.CanUserEditProject(project, User, CurrentUserId),
            CanArchive = _scopingService.CanUserArchiveProject(project, User, CurrentUserId),
            CanManageTeam = _scopingService.CanUserManageTeam(project, User, CurrentUserId),
            IsAdmin = User.IsInRole("Admin"),
            CanViewEmployeeProfiles = User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Viewer"),
            CurrentUserId = CurrentUserId,
            ActiveTab = activeTab,
            ShowPastMembers = showPast,
            PointInTimeDate = asOfDate,
            CurrentTeam = currentTeam,
            PastMembers = pastMembers,
            PointInTimeTeam = pointInTimeTeam,
            AvailableEmployees = availableEmployees,
            ActiveProjectRoles = activeProjectRoles,
            RequiresAdminOverrideForAssign = requiresOverride,
            RecentAuditLogs = auditLogs
        };

        return View(model);
    }

    // POST: /Projects/AssignTeamMembers
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignTeamMembers(AssignTeamMembersInputModel model)
    {
        var project = await _context.Projects
            .Include(p => p.ProjectAssignments)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == model.ProjectId);

        if (project is null)
        {
            return NotFound();
        }

        if (!_scopingService.CanUserViewProject(project, User, CurrentUserId))
        {
            return NotFound();
        }

        if (!_scopingService.CanUserManageTeam(project, User, CurrentUserId))
        {
            return Forbid();
        }

        var requests = new List<MemberAssignmentRequest>();
        var count = Math.Min(model.UserIds.Count, Math.Min(model.RoleOnProjectIds.Count, model.StartDates.Count));

        for (var i = 0; i < count; i++)
        {
            if (model.UserIds[i] > 0 && model.RoleOnProjectIds[i] > 0)
            {
                requests.Add(new MemberAssignmentRequest(
                    model.UserIds[i],
                    model.RoleOnProjectIds[i],
                    model.StartDates[i]));
            }
        }

        var result = await _teamAssignmentService.AssignMembersAsync(
            model.ProjectId,
            requests,
            CurrentUserId ?? project.LeadId,
            User.IsInRole("Admin"),
            model.AdminOverrideReason);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Details), new { id = model.ProjectId, tab = "team" });
        }

        TempData["SuccessMessage"] = $"{requests.Count} team member(s) assigned to '{project.Code}' successfully.";
        return RedirectToAction(nameof(Details), new { id = model.ProjectId, tab = "team" });
    }

    // POST: /Projects/RemoveTeamMember
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTeamMember(RemoveTeamMemberInputModel model)
    {
        var project = await _context.Projects
            .Include(p => p.ProjectAssignments)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == model.ProjectId);

        if (project is null)
        {
            return NotFound();
        }

        if (!_scopingService.CanUserViewProject(project, User, CurrentUserId))
        {
            return NotFound();
        }

        if (!_scopingService.CanUserManageTeam(project, User, CurrentUserId))
        {
            return Forbid();
        }

        var result = await _teamAssignmentService.RemoveMemberAsync(
            model.ProjectId,
            model.AssignmentId,
            model.EffectiveEndDate,
            model.Reason,
            CurrentUserId ?? project.LeadId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Details), new { id = model.ProjectId, tab = "team" });
        }

        TempData["SuccessMessage"] = "Team member removed and archived to historical project roster.";
        return RedirectToAction(nameof(Details), new { id = model.ProjectId, tab = "team", showPast = true });
    }

    // POST: /Projects/ChangeTeamMemberRole
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeTeamMemberRole(ChangeTeamMemberRoleInputModel model)
    {
        var project = await _context.Projects
            .Include(p => p.ProjectAssignments)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == model.ProjectId);

        if (project is null)
        {
            return NotFound();
        }

        if (!_scopingService.CanUserViewProject(project, User, CurrentUserId))
        {
            return NotFound();
        }

        if (!_scopingService.CanUserManageTeam(project, User, CurrentUserId))
        {
            return Forbid();
        }

        var result = await _teamAssignmentService.ChangeRoleOnProjectAsync(
            model.ProjectId,
            model.AssignmentId,
            model.NewRoleOnProjectId,
            CurrentUserId ?? project.LeadId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Details), new { id = model.ProjectId, tab = "team" });
        }

        TempData["SuccessMessage"] = "Team member role updated successfully.";
        return RedirectToAction(nameof(Details), new { id = model.ProjectId, tab = "team" });
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

        if (!isAdmin && string.IsNullOrWhiteSpace(model.Code))
        {
            model.Code = await _codeGenerator.GenerateNextCodeAsync();
        }

        if (!string.IsNullOrWhiteSpace(model.Code))
        {
            var codeExists = await _context.Projects.AnyAsync(p => p.Code == model.Code.Trim());
            if (codeExists)
            {
                ModelState.AddModelError("Code", $"Project code '{model.Code}' is already taken. Please choose or generate another code.");
            }
        }

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

        if (model.Status == ProjectStatus.Completed && model.ProgressPercent < 100)
        {
            model.ProgressPercent = 100;
        }

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

        // Spec 4.3 / Week 3 Task 3: The project lead appears on the team as a member with the Lead role
        await _teamAssignmentService.EnsureLeadOnTeamAsync(
            project.Id,
            project.LeadId,
            project.StartDate,
            currentUserId);

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

        TempData["SuccessMessage"] = $"Project '{project.Code} — {project.Name}' created successfully.";
        return RedirectToAction(nameof(Details), new { id = project.Id });
    }

    // GET: /Projects/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var project = await _context.Projects
            .Include(p => p.ProjectClients)
            .Include(p => p.ProjectAssignments)
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
            KeepPreviousLeadOnTeam = false,
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
            .Include(p => p.ProjectAssignments)
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

        if (isAdmin && model.Code != project.Code)
        {
            var codeExists = await _context.Projects.AnyAsync(p => p.Id != id && p.Code == model.Code.Trim());
            if (codeExists)
            {
                ModelState.AddModelError("Code", $"Project code '{model.Code}' is already in use.");
            }
        }

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

        var wasCompleted = project.Status == ProjectStatus.Completed;
        var isNowReopened = wasCompleted && model.Status != ProjectStatus.Completed && model.Status != ProjectStatus.Cancelled;

        if (isNowReopened)
        {
            model.ActualEndDate = null;
        }

        if (model.Status == ProjectStatus.Completed && model.ProgressPercent < 100)
        {
            model.ProgressPercent = 100;
        }

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

        var oldLeadId = project.LeadId;

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

            var currentClientIds = project.ProjectClients.Select(pc => pc.ClientId).ToHashSet();
            var newClientIds = (model.SelectedClientIds ?? []).Distinct().ToHashSet();

            var toRemove = project.ProjectClients.Where(pc => !newClientIds.Contains(pc.ClientId)).ToList();
            foreach (var pc in toRemove)
            {
                project.ProjectClients.Remove(pc);
            }

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

            var operatorId = CurrentUserId ?? project.LeadId;

            // Spec 4.3 / Week 3 Task 3: Changing project lead is logged; old lead stays on team only if explicitly kept
            if (oldLeadId != project.LeadId)
            {
                await _teamAssignmentService.HandleLeadChangeAsync(
                    project.Id,
                    oldLeadId,
                    project.LeadId,
                    model.KeepPreviousLeadOnTeam,
                    operatorId);
            }
            else
            {
                await _teamAssignmentService.EnsureLeadOnTeamAsync(
                    project.Id,
                    project.LeadId,
                    project.StartDate,
                    operatorId);
            }

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

            TempData["SuccessMessage"] = $"Project '{project.Code}' updated successfully.";
            return RedirectToAction(nameof(Details), new { id = project.Id });
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrency conflict detected while editing Project Id {ProjectId}", id);

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
        var project = await _context.Projects
            .Include(p => p.ProjectAssignments)
            .FirstOrDefaultAsync(p => p.Id == id);

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

        TempData["SuccessMessage"] = $"Project '{project.Code}' archived successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Projects/Restore/5
    [HttpPost]
    public async Task<IActionResult> Restore(int id)
    {
        var project = await _context.Projects
            .Include(p => p.ProjectAssignments)
            .FirstOrDefaultAsync(p => p.Id == id);

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

        TempData["SuccessMessage"] = $"Project '{project.Code}' restored from archive.";
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
