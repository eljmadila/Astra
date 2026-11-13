using System.Security.Claims;
using AspnetCoreMvcFull.Entities;

namespace AspnetCoreMvcFull.Services;

public interface IProjectScopingService
{
    IQueryable<Project> ScopeProjects(IQueryable<Project> query, ClaimsPrincipal user, int? userId = null);
    bool CanUserViewProject(Project project, ClaimsPrincipal user, int? userId = null);
    bool CanUserEditProject(Project project, ClaimsPrincipal user, int? userId = null);
    bool CanUserArchiveProject(Project project, ClaimsPrincipal user, int? userId = null);
    bool CanUserManageTeam(Project project, ClaimsPrincipal user, int? userId = null);
    bool CanUserViewEmployeeProfile(int targetUserId, ClaimsPrincipal user, int? userId = null);
    int? GetUserId(ClaimsPrincipal user);
}

public class ProjectScopingService : IProjectScopingService
{
    public int? GetUserId(ClaimsPrincipal user)
    {
        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out var id) ? id : null;
    }

    public IQueryable<Project> ScopeProjects(IQueryable<Project> query, ClaimsPrincipal user, int? userId = null)
    {
        userId ??= GetUserId(user);

        if (user.IsInRole("Admin") || user.IsInRole("Viewer") || user.IsInRole("Manager"))
        {
            // Admins, Viewers, and Managers have portfolio directory visibility
            return query;
        }

        if (user.IsInRole("Employee"))
        {
            // Spec 4.2 / 4.3: Employees see only projects they are or were assigned to (or lead).
            if (userId.HasValue)
            {
                var uid = userId.Value;
                return query.Where(p => p.LeadId == uid || p.ProjectAssignments.Any(pa => pa.UserId == uid));
            }
            return query.Where(_ => false);
        }

        // Unauthenticated / other roles: deny all
        return query.Where(_ => false);
    }

    public bool CanUserViewProject(Project project, ClaimsPrincipal user, int? userId = null)
    {
        userId ??= GetUserId(user);

        if (user.IsInRole("Admin") || user.IsInRole("Viewer") || user.IsInRole("Manager"))
        {
            return true;
        }

        if (user.IsInRole("Employee"))
        {
            return userId.HasValue &&
                   (project.LeadId == userId.Value ||
                    project.ProjectAssignments.Any(pa => pa.UserId == userId.Value));
        }

        return false;
    }

    public bool CanUserEditProject(Project project, ClaimsPrincipal user, int? userId = null)
    {
        userId ??= GetUserId(user);

        if (user.IsInRole("Admin"))
        {
            return true;
        }

        if (user.IsInRole("Manager"))
        {
            // Spec 4.2: Creator Manager becomes the owner; a Manager may edit and archive only projects they lead
            return userId.HasValue && project.LeadId == userId.Value;
        }

        // Employees and Viewers cannot edit
        return false;
    }

    public bool CanUserArchiveProject(Project project, ClaimsPrincipal user, int? userId = null)
    {
        // Spec 4.2: Same authorization rules as editing
        return CanUserEditProject(project, user, userId);
    }

    public bool CanUserManageTeam(Project project, ClaimsPrincipal user, int? userId = null)
    {
        // Spec 4.3: Admin on all projects; Manager on projects they lead; Employee and Viewer read-only
        return CanUserEditProject(project, user, userId);
    }

    public bool CanUserViewEmployeeProfile(int targetUserId, ClaimsPrincipal user, int? userId = null)
    {
        userId ??= GetUserId(user);

        // Spec 4.3: Admin and Manager see any profile; Viewer read-only; Employee sees their own profile only
        if (user.IsInRole("Admin") || user.IsInRole("Manager") || user.IsInRole("Viewer"))
        {
            return true;
        }

        if (user.IsInRole("Employee"))
        {
            return userId.HasValue && userId.Value == targetUserId;
        }

        return false;
    }
}
