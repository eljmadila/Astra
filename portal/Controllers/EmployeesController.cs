using System.Security.Claims;
using AspnetCoreMvcFull.Data;
using AspnetCoreMvcFull.Entities;
using AspnetCoreMvcFull.Services;
using AspnetCoreMvcFull.ViewModels.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AspnetCoreMvcFull.Controllers;

[Authorize]
public class EmployeesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IProjectScopingService _scopingService;

    public EmployeesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IProjectScopingService scopingService)
    {
        _context = context;
        _userManager = userManager;
        _scopingService = scopingService;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    // GET: /Employees
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        string? department = null,
        string? statusFilter = "active",
        string? roleFilter = null,
        string? sortBy = "name",
        string? sortOrder = "asc",
        int page = 1,
        int pageSize = 15)
    {
        var isEmployeeOnly = User.IsInRole("Employee") &&
                             !User.IsInRole("Admin") &&
                             !User.IsInRole("Manager") &&
                             !User.IsInRole("Viewer");

        var usersQuery = _context.Users.AsNoTracking().AsQueryable();

        // Spec 4.3: Employee sees their own profile only
        if (isEmployeeOnly)
        {
            if (!CurrentUserId.HasValue)
            {
                return NotFound();
            }
            usersQuery = usersQuery.Where(u => u.Id == CurrentUserId.Value);
        }

        // Status filter (Active by default, Inactive or All)
        if (!isEmployeeOnly)
        {
            if (statusFilter == "active")
            {
                usersQuery = usersQuery.Where(u => u.IsActive);
            }
            else if (statusFilter == "inactive")
            {
                usersQuery = usersQuery.Where(u => !u.IsActive);
            }
        }

        // Department filter
        if (!string.IsNullOrWhiteSpace(department))
        {
            usersQuery = usersQuery.Where(u => u.Department == department);
        }

        // Search across Name, Email, Department
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            usersQuery = usersQuery.Where(u =>
                u.Name.ToLower().Contains(s) ||
                (u.Email != null && u.Email.ToLower().Contains(s)) ||
                u.Department.ToLower().Contains(s));
        }

        // Role filter
        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            var roleEntity = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == roleFilter);
            if (roleEntity is not null)
            {
                var roleUserIds = _context.UserRoles
                    .Where(ur => ur.RoleId == roleEntity.Id)
                    .Select(ur => ur.UserId);
                usersQuery = usersQuery.Where(u => roleUserIds.Contains(u.Id));
            }
        }

        // Load users with role and assignment counts
        var matchingUsers = await usersQuery.ToListAsync();
        var userIds = matchingUsers.Select(u => u.Id).ToList();

        var userRolesMap = await (from ur in _context.UserRoles
                                  join r in _context.Roles on ur.RoleId equals r.Id
                                  where userIds.Contains(ur.UserId)
                                  select new { ur.UserId, RoleName = r.Name ?? "Employee" })
                                 .ToListAsync();

        var roleLookup = userRolesMap
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().RoleName);

        var assignments = await _context.ProjectAssignments
            .Include(pa => pa.Project)
            .AsNoTracking()
            .Where(pa => userIds.Contains(pa.UserId))
            .ToListAsync();

        var assignmentsByUser = assignments
            .GroupBy(pa => pa.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = matchingUsers.Select(u =>
        {
            var userAssignments = assignmentsByUser.TryGetValue(u.Id, out var list) ? list : [];
            var activeAssignments = userAssignments.Where(a => a.EndDate == null && !a.Project.IsArchived).ToList();
            var pastAssignments = userAssignments.Where(a => a.EndDate != null).ToList();

            return new EmployeeListItemViewModel
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email ?? string.Empty,
                Department = u.Department,
                SystemRole = roleLookup.TryGetValue(u.Id, out var rName) ? rName : "Employee",
                IsActive = u.IsActive,
                ActiveProjectsCount = activeAssignments.Count,
                PastProjectsCount = pastAssignments.Count,
                CurrentProjectCodes = activeAssignments
                    .Select(a => a.Project.Code)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList()
            };
        }).ToList();

        // Sorting
        items = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
        {
            ("name", "desc") => items.OrderByDescending(i => i.Name).ToList(),
            ("name", _) => items.OrderBy(i => i.Name).ToList(),
            ("department", "desc") => items.OrderByDescending(i => i.Department).ThenBy(i => i.Name).ToList(),
            ("department", _) => items.OrderBy(i => i.Department).ThenBy(i => i.Name).ToList(),
            ("role", "desc") => items.OrderByDescending(i => i.SystemRole).ThenBy(i => i.Name).ToList(),
            ("role", _) => items.OrderBy(i => i.SystemRole).ThenBy(i => i.Name).ToList(),
            ("projects", "desc") => items.OrderByDescending(i => i.ActiveProjectsCount).ThenBy(i => i.Name).ToList(),
            ("projects", _) => items.OrderBy(i => i.ActiveProjectsCount).ThenBy(i => i.Name).ToList(),
            _ => items.OrderBy(i => i.Name).ToList()
        };

        var totalItems = items.Count;
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 5, 100);

        var pagedItems = items
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToList();

        var departments = await _context.Users
            .AsNoTracking()
            .Select(u => u.Department)
            .Distinct()
            .OrderBy(d => d)
            .Select(d => new SelectListItem { Value = d, Text = d })
            .ToListAsync();

        var roles = await _context.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToListAsync();

        var viewModel = new EmployeeListViewModel
        {
            Employees = pagedItems,
            Search = search,
            Department = department,
            StatusFilter = statusFilter,
            RoleFilter = roleFilter,
            SortBy = sortBy,
            SortOrder = sortOrder,
            Page = safePage,
            PageSize = safePageSize,
            TotalItems = totalItems,
            DepartmentOptions = departments,
            RoleOptions = roles,
            IsEmployeeScoped = isEmployeeOnly
        };

        return View(viewModel);
    }

    // GET: /Employees/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        // Spec 4.3: Employee sees their own profile only; return 404 for others (IDOR defense)
        if (!_scopingService.CanUserViewEmployeeProfile(id, User, CurrentUserId))
        {
            return NotFound();
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var systemRole = roles.FirstOrDefault() ?? "Employee";

        var assignments = await _context.ProjectAssignments
            .Include(pa => pa.Project)
                .ThenInclude(p => p.ProjectClients)
                    .ThenInclude(pc => pc.Client)
            .Include(pa => pa.RoleOnProject)
            .AsNoTracking()
            .Where(pa => pa.UserId == id)
            .ToListAsync();

        var currentProjects = assignments
            .Where(pa => pa.EndDate == null)
            .OrderByDescending(pa => pa.Project.LeadId == id)
            .ThenBy(pa => pa.StartDate)
            .Select(pa => new EmployeeProjectEngagementViewModel
            {
                AssignmentId = pa.Id,
                ProjectId = pa.ProjectId,
                ProjectCode = pa.Project.Code,
                ProjectName = pa.Project.Name,
                ProjectStatus = pa.Project.Status,
                IsProjectArchived = pa.Project.IsArchived,
                ClientNames = pa.Project.ProjectClients.Select(pc => pc.Client.Name).ToList(),
                RoleOnProject = pa.RoleOnProject.Name,
                IsProjectLead = pa.Project.LeadId == id,
                StartDate = pa.StartDate,
                EndDate = null
            })
            .ToList();

        var pastProjects = assignments
            .Where(pa => pa.EndDate != null)
            .OrderByDescending(pa => pa.EndDate)
            .ThenByDescending(pa => pa.StartDate)
            .Select(pa => new EmployeeProjectEngagementViewModel
            {
                AssignmentId = pa.Id,
                ProjectId = pa.ProjectId,
                ProjectCode = pa.Project.Code,
                ProjectName = pa.Project.Name,
                ProjectStatus = pa.Project.Status,
                IsProjectArchived = pa.Project.IsArchived,
                ClientNames = pa.Project.ProjectClients.Select(pc => pc.Client.Name).ToList(),
                RoleOnProject = pa.RoleOnProject.Name,
                IsProjectLead = pa.Project.LeadId == id,
                StartDate = pa.StartDate,
                EndDate = pa.EndDate,
                RemovalReason = pa.RemovalReason
            })
            .ToList();

        var model = new EmployeeProfileViewModel
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email ?? string.Empty,
            Department = user.Department,
            SystemRole = systemRole,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            CurrentProjects = currentProjects,
            PastProjects = pastProjects,
            IsAdmin = User.IsInRole("Admin"),
            IsSelf = CurrentUserId == user.Id
        };

        return View(model);
    }
}

