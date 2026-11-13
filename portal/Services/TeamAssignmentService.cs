using AspnetCoreMvcFull.Data;
using AspnetCoreMvcFull.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspnetCoreMvcFull.Services;

public record MemberAssignmentRequest(int UserId, int RoleOnProjectId, DateOnly StartDate);

public record TeamOperationResult(bool Succeeded, string? ErrorMessage = null, List<int>? CreatedAssignmentIds = null);

public record DeactivationBlockerReport(
    bool IsBlocked,
    List<string> LedProjects,
    List<string> ActiveAssignedProjects)
{
    public string BuildResolutionMessage(string userName)
    {
        if (!IsBlocked) return string.Empty;

        var parts = new List<string>();
        if (LedProjects.Count > 0)
        {
            parts.Add($"leads active project(s): {string.Join(", ", LedProjects)}");
        }
        if (ActiveAssignedProjects.Count > 0)
        {
            parts.Add($"holds active team assignment(s) on: {string.Join(", ", ActiveAssignedProjects)}");
        }

        return $"Cannot deactivate {userName} because this user currently {string.Join(" and ", parts)}. Please assign a new project lead and/or end active project assignments first.";
    }
}

public interface ITeamAssignmentService
{
    /// <summary>
    /// Spec Section 4.3 / Week 3 Task 5:
    /// Active on date D means start_date &lt;= D AND (end_date IS NULL OR end_date &gt; D).
    /// </summary>
    bool IsActiveOnDate(ProjectAssignment assignment, DateOnly date);

    List<ProjectAssignment> FilterTeamOnDate(IEnumerable<ProjectAssignment> assignments, DateOnly date);

    Task<List<ProjectAssignment>> GetTeamOnAsync(int projectId, DateOnly date, CancellationToken cancellationToken = default);

    Task<TeamOperationResult> AssignMembersAsync(
        int projectId,
        IReadOnlyList<MemberAssignmentRequest> requests,
        int performedByUserId,
        bool isAdmin,
        string? adminOverrideReason = null,
        CancellationToken cancellationToken = default);

    Task<TeamOperationResult> RemoveMemberAsync(
        int projectId,
        int assignmentId,
        DateOnly effectiveEndDate,
        string? reason,
        int performedByUserId,
        CancellationToken cancellationToken = default);

    Task<TeamOperationResult> ChangeRoleOnProjectAsync(
        int projectId,
        int assignmentId,
        int newRoleOnProjectId,
        int performedByUserId,
        CancellationToken cancellationToken = default);

    Task EnsureLeadOnTeamAsync(
        int projectId,
        int leadUserId,
        DateOnly startDate,
        int performedByUserId,
        CancellationToken cancellationToken = default);

    Task HandleLeadChangeAsync(
        int projectId,
        int oldLeadUserId,
        int newLeadUserId,
        bool keepOldLeadOnTeam,
        int performedByUserId,
        CancellationToken cancellationToken = default);

    Task<DeactivationBlockerReport> GetDeactivationBlockersAsync(int userId, CancellationToken cancellationToken = default);
}

public class TeamAssignmentService : ITeamAssignmentService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public TeamAssignmentService(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public bool IsActiveOnDate(ProjectAssignment assignment, DateOnly date)
    {
        return assignment.StartDate <= date && (!assignment.EndDate.HasValue || assignment.EndDate.Value > date);
    }

    public List<ProjectAssignment> FilterTeamOnDate(IEnumerable<ProjectAssignment> assignments, DateOnly date)
    {
        return assignments
            .Where(a => IsActiveOnDate(a, date))
            .OrderBy(a => a.StartDate)
            .ThenBy(a => a.User?.Name ?? string.Empty)
            .ToList();
    }

    public async Task<List<ProjectAssignment>> GetTeamOnAsync(int projectId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var assignments = await _context.ProjectAssignments
            .Include(pa => pa.User)
            .Include(pa => pa.RoleOnProject)
            .Include(pa => pa.CreatedBy)
            .AsNoTracking()
            .Where(pa => pa.ProjectId == projectId &&
                         pa.StartDate <= date &&
                         (pa.EndDate == null || pa.EndDate > date))
            .OrderBy(pa => pa.StartDate)
            .ThenBy(pa => pa.User.Name)
            .ToListAsync(cancellationToken);

        return assignments;
    }

    public async Task<TeamOperationResult> AssignMembersAsync(
        int projectId,
        IReadOnlyList<MemberAssignmentRequest> requests,
        int performedByUserId,
        bool isAdmin,
        string? adminOverrideReason = null,
        CancellationToken cancellationToken = default)
    {
        if (requests is null || requests.Count == 0)
        {
            return new TeamOperationResult(false, "Please select at least one employee to assign.");
        }

        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project is null)
        {
            return new TeamOperationResult(false, "Project not found.");
        }

        // Spec 4.3: Block assigning to Completed, Cancelled or archived projects (Admin override with a reason)
        var requiresOverride = project.IsArchived ||
                               project.Status == ProjectStatus.Completed ||
                               project.Status == ProjectStatus.Cancelled;

        if (requiresOverride)
        {
            if (!isAdmin)
            {
                return new TeamOperationResult(
                    false,
                    $"Cannot assign team members to project '{project.Code}' because it is {(project.IsArchived ? "Archived" : project.Status.ToString())}. Only an Administrator may override this restriction with a documented reason.");
            }

            if (string.IsNullOrWhiteSpace(adminOverrideReason) || adminOverrideReason.Trim().Length < 5)
            {
                return new TeamOperationResult(
                    false,
                    $"Project '{project.Code}' is {(project.IsArchived ? "Archived" : project.Status.ToString())}. An Administrator override reason (at least 5 characters) is required.");
            }
        }

        // Check duplicate users within the batch request itself
        var distinctUserIds = requests.Select(r => r.UserId).Distinct().ToList();
        if (distinctUserIds.Count != requests.Count)
        {
            return new TeamOperationResult(false, "The same employee cannot be selected multiple times in a single batch.");
        }

        // Load users and roles
        var users = await _context.Users
            .Where(u => distinctUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var roleIds = requests.Select(r => r.RoleOnProjectId).Distinct().ToList();
        var roles = await _context.ProjectRoles
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        // Load currently active assignments on this project
        var activeUserIdsOnProject = await _context.ProjectAssignments
            .Where(pa => pa.ProjectId == projectId && pa.EndDate == null && distinctUserIds.Contains(pa.UserId))
            .Select(pa => pa.UserId)
            .ToListAsync(cancellationToken);

        var createdAssignments = new List<ProjectAssignment>();

        foreach (var req in requests)
        {
            if (!users.TryGetValue(req.UserId, out var user))
            {
                return new TeamOperationResult(false, $"Selected user (ID {req.UserId}) does not exist.");
            }

            if (!user.IsActive)
            {
                return new TeamOperationResult(false, $"Cannot assign '{user.Name}' because their account is deactivated. Only active users can be assigned.");
            }

            if (!roles.TryGetValue(req.RoleOnProjectId, out var role) || !role.IsActive)
            {
                return new TeamOperationResult(false, "Please select a valid, active project role.");
            }

            if (activeUserIdsOnProject.Contains(req.UserId))
            {
                return new TeamOperationResult(false, $"'{user.Name}' already holds an active assignment on project '{project.Code}'.");
            }

            var assignment = new ProjectAssignment
            {
                ProjectId = projectId,
                UserId = req.UserId,
                RoleOnProjectId = req.RoleOnProjectId,
                StartDate = req.StartDate,
                EndDate = null,
                CreatedById = performedByUserId,
                CreatedAt = DateTime.UtcNow
            };

            createdAssignments.Add(assignment);
        }

        _context.ProjectAssignments.AddRange(createdAssignments);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new TeamOperationResult(false, "A concurrent assignment conflict occurred: one or more selected users already have an active assignment on this project.");
        }

        foreach (var assignment in createdAssignments)
        {
            var user = users[assignment.UserId];
            var role = roles[assignment.RoleOnProjectId];

            await _auditService.LogAsync(
                action: "AssignedToTeam",
                entity: "Project",
                entityId: projectId.ToString(),
                details: new
                {
                    AssignmentId = assignment.Id,
                    ProjectCode = project.Code,
                    UserId = user.Id,
                    UserName = user.Name,
                    RoleOnProject = role.Name,
                    StartDate = assignment.StartDate.ToString("yyyy-MM-dd"),
                    AdminOverrideReason = requiresOverride ? adminOverrideReason?.Trim() : null
                },
                userId: performedByUserId,
                cancellationToken: cancellationToken);
        }

        return new TeamOperationResult(true, CreatedAssignmentIds: createdAssignments.Select(a => a.Id).ToList());
    }

    public async Task<TeamOperationResult> RemoveMemberAsync(
        int projectId,
        int assignmentId,
        DateOnly effectiveEndDate,
        string? reason,
        int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _context.ProjectAssignments
            .Include(pa => pa.User)
            .Include(pa => pa.RoleOnProject)
            .Include(pa => pa.Project)
            .FirstOrDefaultAsync(pa => pa.Id == assignmentId && pa.ProjectId == projectId, cancellationToken);

        if (assignment is null)
        {
            return new TeamOperationResult(false, "Team assignment record not found.");
        }

        if (assignment.EndDate.HasValue)
        {
            return new TeamOperationResult(false, $"'{assignment.User.Name}' has already been removed from this team (ended on {assignment.EndDate.Value:MMM dd, yyyy}).");
        }

        // Spec 4.3 / Week 3 Task 3: Block removing the current lead from the team until a new lead is chosen
        if (assignment.Project.LeadId == assignment.UserId)
        {
            return new TeamOperationResult(
                false,
                $"Cannot remove '{assignment.User.Name}' from the team because they are the current Project Lead for '{assignment.Project.Code}'. Please select a new Project Lead first.");
        }

        // Spec 4.3: CHECK end_date >= start_date
        if (effectiveEndDate < assignment.StartDate)
        {
            return new TeamOperationResult(
                false,
                $"Effective removal date ({effectiveEndDate:MMM dd, yyyy}) cannot be earlier than the assignment start date ({assignment.StartDate:MMM dd, yyyy}).");
        }

        // Soft-close assignment (NEVER hard delete)
        assignment.EndDate = effectiveEndDate;
        assignment.RemovalReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "RemovedFromTeam",
            entity: "Project",
            entityId: projectId.ToString(),
            details: new
            {
                AssignmentId = assignment.Id,
                ProjectCode = assignment.Project.Code,
                UserId = assignment.UserId,
                UserName = assignment.User.Name,
                RoleOnProject = assignment.RoleOnProject.Name,
                StartDate = assignment.StartDate.ToString("yyyy-MM-dd"),
                EndDate = effectiveEndDate.ToString("yyyy-MM-dd"),
                Reason = assignment.RemovalReason
            },
            userId: performedByUserId,
            cancellationToken: cancellationToken);

        return new TeamOperationResult(true);
    }

    public async Task<TeamOperationResult> ChangeRoleOnProjectAsync(
        int projectId,
        int assignmentId,
        int newRoleOnProjectId,
        int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _context.ProjectAssignments
            .Include(pa => pa.User)
            .Include(pa => pa.RoleOnProject)
            .Include(pa => pa.Project)
            .FirstOrDefaultAsync(pa => pa.Id == assignmentId && pa.ProjectId == projectId, cancellationToken);

        if (assignment is null || assignment.EndDate.HasValue)
        {
            return new TeamOperationResult(false, "Active team assignment not found.");
        }

        var newRole = await _context.ProjectRoles
            .FirstOrDefaultAsync(r => r.Id == newRoleOnProjectId && r.IsActive, cancellationToken);

        if (newRole is null)
        {
            return new TeamOperationResult(false, "Selected project role is invalid or inactive.");
        }

        if (assignment.Project.LeadId == assignment.UserId && !newRole.Name.Equals("Lead", StringComparison.OrdinalIgnoreCase))
        {
            return new TeamOperationResult(
                false,
                $"'{assignment.User.Name}' is the designated Project Lead and must retain the 'Lead' role until a new Project Lead is assigned.");
        }

        var oldRoleName = assignment.RoleOnProject.Name;
        if (assignment.RoleOnProjectId == newRoleOnProjectId)
        {
            return new TeamOperationResult(true);
        }

        assignment.RoleOnProjectId = newRoleOnProjectId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "RoleOnProjectChanged",
            entity: "Project",
            entityId: projectId.ToString(),
            details: new
            {
                AssignmentId = assignment.Id,
                ProjectCode = assignment.Project.Code,
                UserId = assignment.UserId,
                UserName = assignment.User.Name,
                OldRole = oldRoleName,
                NewRole = newRole.Name
            },
            userId: performedByUserId,
            cancellationToken: cancellationToken);

        return new TeamOperationResult(true);
    }

    public async Task EnsureLeadOnTeamAsync(
        int projectId,
        int leadUserId,
        DateOnly startDate,
        int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var leadRole = await GetOrCreateLeadRoleAsync(cancellationToken);

        var existingActive = await _context.ProjectAssignments
            .FirstOrDefaultAsync(pa => pa.ProjectId == projectId && pa.UserId == leadUserId && pa.EndDate == null, cancellationToken);

        if (existingActive is not null)
        {
            if (existingActive.RoleOnProjectId != leadRole.Id)
            {
                existingActive.RoleOnProjectId = leadRole.Id;
                await _context.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var assignment = new ProjectAssignment
        {
            ProjectId = projectId,
            UserId = leadUserId,
            RoleOnProjectId = leadRole.Id,
            StartDate = startDate,
            EndDate = null,
            CreatedById = performedByUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProjectAssignments.Add(assignment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task HandleLeadChangeAsync(
        int projectId,
        int oldLeadUserId,
        int newLeadUserId,
        bool keepOldLeadOnTeam,
        int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (oldLeadUserId == newLeadUserId)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var leadRole = await GetOrCreateLeadRoleAsync(cancellationToken);

        // 1. Handle old lead's active assignment
        var oldLeadAssignment = await _context.ProjectAssignments
            .FirstOrDefaultAsync(pa => pa.ProjectId == projectId && pa.UserId == oldLeadUserId && pa.EndDate == null, cancellationToken);

        if (oldLeadAssignment is not null)
        {
            if (!keepOldLeadOnTeam)
            {
                var closeDate = today < oldLeadAssignment.StartDate ? oldLeadAssignment.StartDate : today;
                oldLeadAssignment.EndDate = closeDate;
                oldLeadAssignment.RemovalReason = "Project Lead transferred to another user.";
            }
            else
            {
                // Keep on team: if their role was Lead, transition to another active role (e.g. Support/Developer/Other)
                if (oldLeadAssignment.RoleOnProjectId == leadRole.Id)
                {
                    var fallbackRole = await _context.ProjectRoles
                        .FirstOrDefaultAsync(r => r.IsActive && r.Name != "Lead", cancellationToken);
                    if (fallbackRole is not null)
                    {
                        oldLeadAssignment.RoleOnProjectId = fallbackRole.Id;
                    }
                }
            }
        }

        // 2. Ensure new lead has an active Lead assignment
        var newLeadAssignment = await _context.ProjectAssignments
            .FirstOrDefaultAsync(pa => pa.ProjectId == projectId && pa.UserId == newLeadUserId && pa.EndDate == null, cancellationToken);

        if (newLeadAssignment is not null)
        {
            newLeadAssignment.RoleOnProjectId = leadRole.Id;
        }
        else
        {
            _context.ProjectAssignments.Add(new ProjectAssignment
            {
                ProjectId = projectId,
                UserId = newLeadUserId,
                RoleOnProjectId = leadRole.Id,
                StartDate = today,
                EndDate = null,
                CreatedById = performedByUserId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        var oldLeadUser = await _context.Users.FindAsync([oldLeadUserId], cancellationToken);
        var newLeadUser = await _context.Users.FindAsync([newLeadUserId], cancellationToken);

        await _auditService.LogAsync(
            action: "LeadChanged",
            entity: "Project",
            entityId: projectId.ToString(),
            details: new
            {
                OldLeadId = oldLeadUserId,
                OldLeadName = oldLeadUser?.Name,
                NewLeadId = newLeadUserId,
                NewLeadName = newLeadUser?.Name,
                OldLeadKeptOnTeam = keepOldLeadOnTeam
            },
            userId: performedByUserId,
            cancellationToken: cancellationToken);
    }

    public async Task<DeactivationBlockerReport> GetDeactivationBlockersAsync(int userId, CancellationToken cancellationToken = default)
    {
        var ledProjects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.LeadId == userId &&
                        !p.IsArchived &&
                        p.Status != ProjectStatus.Completed &&
                        p.Status != ProjectStatus.Cancelled)
            .OrderBy(p => p.Code)
            .Select(p => $"{p.Code} ({p.Name})")
            .ToListAsync(cancellationToken);

        var activeAssignments = await _context.ProjectAssignments
            .Include(pa => pa.Project)
            .Include(pa => pa.RoleOnProject)
            .AsNoTracking()
            .Where(pa => pa.UserId == userId && pa.EndDate == null)
            .OrderBy(pa => pa.Project.Code)
            .Select(pa => $"{pa.Project.Code} as {pa.RoleOnProject.Name}")
            .ToListAsync(cancellationToken);

        var isBlocked = ledProjects.Count > 0 || activeAssignments.Count > 0;
        return new DeactivationBlockerReport(isBlocked, ledProjects, activeAssignments);
    }

    private async Task<ProjectRole> GetOrCreateLeadRoleAsync(CancellationToken cancellationToken)
    {
        var leadRole = await _context.ProjectRoles
            .FirstOrDefaultAsync(r => r.Name == "Lead", cancellationToken);

        if (leadRole is null)
        {
            leadRole = new ProjectRole { Name = "Lead", IsActive = true };
            _context.ProjectRoles.Add(leadRole);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return leadRole;
    }
}

