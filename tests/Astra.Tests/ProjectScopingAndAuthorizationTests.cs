using System.Security.Claims;
using AspnetCoreMvcFull.Entities;
using AspnetCoreMvcFull.Services;
using Xunit;

namespace Astra.Tests;

public class ProjectScopingAndAuthorizationTests
{
    private readonly ProjectScopingService _scopingService = new();

    private ClaimsPrincipal CreatePrincipal(int userId, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void Admin_CanViewEditAndArchiveAnyProject()
    {
        var adminPrincipal = CreatePrincipal(1, "Admin");
        var project = new Project { Id = 10, LeadId = 99 }; // Owned by someone else

        Assert.True(_scopingService.CanUserViewProject(project, adminPrincipal));
        Assert.True(_scopingService.CanUserEditProject(project, adminPrincipal));
        Assert.True(_scopingService.CanUserArchiveProject(project, adminPrincipal));
    }

    [Fact]
    public void Manager_WhoIsLead_CanEditAndArchiveProject()
    {
        var managerPrincipal = CreatePrincipal(5, "Manager");
        var project = new Project { Id = 10, LeadId = 5 }; // Led by this manager

        Assert.True(_scopingService.CanUserViewProject(project, managerPrincipal));
        Assert.True(_scopingService.CanUserEditProject(project, managerPrincipal));
        Assert.True(_scopingService.CanUserArchiveProject(project, managerPrincipal));
    }

    [Fact]
    public void Manager_WhoIsNotLead_CanView_ButCannotEditOrArchive()
    {
        var managerPrincipal = CreatePrincipal(5, "Manager");
        var otherProject = new Project { Id = 11, LeadId = 6 }; // Led by another manager

        Assert.True(_scopingService.CanUserViewProject(otherProject, managerPrincipal));
        Assert.False(_scopingService.CanUserEditProject(otherProject, managerPrincipal));
        Assert.False(_scopingService.CanUserArchiveProject(otherProject, managerPrincipal));
    }

    [Fact]
    public void Employee_WhoIsNotAssigned_CannotViewEditOrArchive()
    {
        var employeePrincipal = CreatePrincipal(20, "Employee");
        var unassignedProject = new Project { Id = 12, LeadId = 5 };

        // Spec: Returns false so controller returns 404 Not Found (IDOR prevention)
        Assert.False(_scopingService.CanUserViewProject(unassignedProject, employeePrincipal));
        Assert.False(_scopingService.CanUserEditProject(unassignedProject, employeePrincipal));
        Assert.False(_scopingService.CanUserArchiveProject(unassignedProject, employeePrincipal));
    }

    [Fact]
    public void Viewer_CanView_ButCannotEditOrArchive()
    {
        var viewerPrincipal = CreatePrincipal(30, "Viewer");
        var project = new Project { Id = 15, LeadId = 5 };

        Assert.True(_scopingService.CanUserViewProject(project, viewerPrincipal));
        Assert.False(_scopingService.CanUserEditProject(project, viewerPrincipal));
        Assert.False(_scopingService.CanUserArchiveProject(project, viewerPrincipal));
    }

    [Fact]
    public void ScopeProjects_ForEmployee_FiltersOutUnassignedProjects_AndIncludesAssignedProjects()
    {
        var employeePrincipal = CreatePrincipal(20, "Employee");
        var projects = new List<Project>
        {
            new() { Id = 1, LeadId = 20, Code = "PRJ-01", Name = "Led By Employee" },
            new()
            {
                Id = 2,
                LeadId = 99,
                Code = "PRJ-02",
                Name = "Assigned To Employee",
                ProjectAssignments = [new ProjectAssignment { ProjectId = 2, UserId = 20, StartDate = new DateOnly(2026, 1, 1) }]
            },
            new() { Id = 3, LeadId = 99, Code = "PRJ-03", Name = "Not Assigned" }
        }.AsQueryable();

        var scoped = _scopingService.ScopeProjects(projects, employeePrincipal).ToList();

        Assert.Equal(2, scoped.Count);
        Assert.Contains(scoped, p => p.Code == "PRJ-01");
        Assert.Contains(scoped, p => p.Code == "PRJ-02");
    }

    [Fact]
    public void EmployeeProfileScoping_EmployeeSeesOwnOnly_StaffSeesAll()
    {
        var employeePrincipal = CreatePrincipal(20, "Employee");
        var managerPrincipal = CreatePrincipal(5, "Manager");
        var adminPrincipal = CreatePrincipal(1, "Admin");
        var viewerPrincipal = CreatePrincipal(30, "Viewer");

        // Employee can view own profile (20), but receives false (404 IDOR defense) for another employee (21)
        Assert.True(_scopingService.CanUserViewEmployeeProfile(20, employeePrincipal));
        Assert.False(_scopingService.CanUserViewEmployeeProfile(21, employeePrincipal));

        // Admin, Manager, and Viewer can view any profile
        Assert.True(_scopingService.CanUserViewEmployeeProfile(21, adminPrincipal));
        Assert.True(_scopingService.CanUserViewEmployeeProfile(21, managerPrincipal));
        Assert.True(_scopingService.CanUserViewEmployeeProfile(21, viewerPrincipal));
    }
}
