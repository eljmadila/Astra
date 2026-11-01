using System.Security.Claims;
using AspnetCoreMvcFull.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspnetCoreMvcFull.Services;

public interface IProjectScopingService
{
    IQueryable<Project> ScopeProjects(IQueryable<Project> query, ClaimsPrincipal user, int? userId = null);
    bool CanUserViewProject(Project project, ClaimsPrincipal user, int? userId = null);
    bool CanUserEditProject(Project project, ClaimsPrincipal user, int? userId = null);
    bool CanUserArchiveProject(Project project, ClaimsPrincipal user, int? userId = null);
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
            // Spec 4.2 / 4.4: Employees see only projects they are or were assigned to.
            // Extension point for Week 3 assignments: for now, projects where they are Lead or explicitly assigned.
            if (userId.HasValue)
            {
                return query.Where(p => p.LeadId == userId.Value);
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
            return userId.HasValue && project.LeadId == userId.Value;
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
}
