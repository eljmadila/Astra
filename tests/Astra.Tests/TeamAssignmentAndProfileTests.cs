using System.Reflection;
using AspnetCoreMvcFull.Data;
using AspnetCoreMvcFull.Entities;
using AspnetCoreMvcFull.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Astra.Tests;

public class TeamAssignmentAndProfileTests
{
    private static (ApplicationDbContext Context, TeamAssignmentService Service) CreateTestEnvironment()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AstraWeek3TestDb_{Guid.NewGuid()}")
            .Options;

        var context = new ApplicationDbContext(options);
        var httpContextAccessor = new HttpContextAccessor();
        var auditService = new AuditService(context, httpContextAccessor);
        var teamService = new TeamAssignmentService(context, auditService);

        return (context, teamService);
    }

    private static async Task SeedBaselineDataAsync(ApplicationDbContext context)
    {
        context.Users.AddRange(
            new ApplicationUser { Id = 1, UserName = "admin@astra.test", Email = "admin@astra.test", Name = "Admin One", Department = "Governance", IsActive = true },
            new ApplicationUser { Id = 2, UserName = "lead@astra.test", Email = "lead@astra.test", Name = "Lead Manager", Department = "Delivery", IsActive = true },
            new ApplicationUser { Id = 3, UserName = "emp1@astra.test", Email = "emp1@astra.test", Name = "Alice Dev", Department = "Engineering", IsActive = true },
            new ApplicationUser { Id = 4, UserName = "emp2@astra.test", Email = "emp2@astra.test", Name = "Bob QA", Department = "QA", IsActive = true },
            new ApplicationUser { Id = 5, UserName = "inactive@astra.test", Email = "inactive@astra.test", Name = "Inactive User", Department = "Legacy", IsActive = false }
        );

        context.ProjectRoles.AddRange(
            new ProjectRole { Id = 1, Name = "Lead", IsActive = true },
            new ProjectRole { Id = 2, Name = "Developer", IsActive = true },
            new ProjectRole { Id = 3, Name = "Tester", IsActive = true },
            new ProjectRole { Id = 4, Name = "Analyst", IsActive = true }
        );

        context.Projects.AddRange(
            new Project
            {
                Id = 100,
                Code = "PRJ-2026-100",
                Name = "Active Project",
                LeadId = 2,
                CreatedById = 2,
                Status = ProjectStatus.InProgress,
                Priority = ProjectPriority.High,
                StartDate = new DateOnly(2026, 1, 1),
                PlannedEndDate = new DateOnly(2026, 12, 31)
            },
            new Project
            {
                Id = 101,
                Code = "PRJ-2026-101",
                Name = "Completed Project",
                LeadId = 2,
                CreatedById = 2,
                Status = ProjectStatus.Completed,
                Priority = ProjectPriority.Medium,
                ProgressPercent = 100,
                StartDate = new DateOnly(2026, 1, 1),
                PlannedEndDate = new DateOnly(2026, 6, 1),
                ActualEndDate = new DateOnly(2026, 6, 1)
            }
        );

        await context.SaveChangesAsync();
    }

    [Fact]
    public void PointInTimeQuery_BoundaryDates_StartDay_EndDay_SameDayStartAndEnd()
    {
        var (_, service) = CreateTestEnvironment();

        // Assignment from Jan 10 to Jan 20
        var standardAssignment = new ProjectAssignment
        {
            ProjectId = 100,
            UserId = 3,
            RoleOnProjectId = 2,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 1, 20)
        };

        // Spec 4.3 / Week 3 Task 5: active on date D means start_date <= D AND (end_date IS NULL OR end_date > D)
        Assert.False(service.IsActiveOnDate(standardAssignment, new DateOnly(2026, 1, 9)));  // Day before start
        Assert.True(service.IsActiveOnDate(standardAssignment, new DateOnly(2026, 1, 10)));  // On start day (inclusive)
        Assert.True(service.IsActiveOnDate(standardAssignment, new DateOnly(2026, 1, 15)));  // Middle of assignment
        Assert.False(service.IsActiveOnDate(standardAssignment, new DateOnly(2026, 1, 20))); // On end day (end_date > D is false)

        // Same-day start and end (StartDate == EndDate)
        var sameDayAssignment = new ProjectAssignment
        {
            ProjectId = 100,
            UserId = 4,
            RoleOnProjectId = 3,
            StartDate = new DateOnly(2026, 2, 1),
            EndDate = new DateOnly(2026, 2, 1)
        };

        Assert.False(service.IsActiveOnDate(sameDayAssignment, new DateOnly(2026, 2, 1)));

        // Open-ended active assignment (EndDate == null)
        var openAssignment = new ProjectAssignment
        {
            ProjectId = 100,
            UserId = 3,
            RoleOnProjectId = 2,
            StartDate = new DateOnly(2026, 3, 1),
            EndDate = null
        };

        Assert.False(service.IsActiveOnDate(openAssignment, new DateOnly(2026, 2, 28)));
        Assert.True(service.IsActiveOnDate(openAssignment, new DateOnly(2026, 3, 1)));
        Assert.True(service.IsActiveOnDate(openAssignment, new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public async Task AssignRemoveAndReAdd_PreservesFullHistory_AndWritesAuditLogs()
    {
        var (context, service) = CreateTestEnvironment();
        await SeedBaselineDataAsync(context);

        // 1. Assign Alice (3) and Bob (4) at once
        var assignResult = await service.AssignMembersAsync(
            projectId: 100,
            requests:
            [
                new MemberAssignmentRequest(3, 2, new DateOnly(2026, 2, 1)),
                new MemberAssignmentRequest(4, 3, new DateOnly(2026, 2, 5))
            ],
            performedByUserId: 2,
            isAdmin: false);

        Assert.True(assignResult.Succeeded);
        Assert.Equal(2, await context.ProjectAssignments.CountAsync(pa => pa.ProjectId == 100 && pa.EndDate == null));

        // 2. Duplicate active assignment for Alice is rejected
        var duplicateResult = await service.AssignMembersAsync(
            projectId: 100,
            requests: [new MemberAssignmentRequest(3, 4, new DateOnly(2026, 2, 10))],
            performedByUserId: 2,
            isAdmin: false);

        Assert.False(duplicateResult.Succeeded);
        Assert.Contains("already holds an active assignment", duplicateResult.ErrorMessage);

        // 3. Remove Alice on 2026-03-01
        var aliceFirstAssignment = await context.ProjectAssignments.FirstAsync(pa => pa.ProjectId == 100 && pa.UserId == 3);
        var removeResult = await service.RemoveMemberAsync(
            projectId: 100,
            assignmentId: aliceFirstAssignment.Id,
            effectiveEndDate: new DateOnly(2026, 3, 1),
            reason: "Completed initial module",
            performedByUserId: 2);

        Assert.True(removeResult.Succeeded);

        // 4. Re-add Alice on 2026-04-01 in Analyst role -> creates a NEW row, leaving old row intact
        var reAddResult = await service.AssignMembersAsync(
            projectId: 100,
            requests: [new MemberAssignmentRequest(3, 4, new DateOnly(2026, 4, 1))],
            performedByUserId: 2,
            isAdmin: false);

        Assert.True(reAddResult.Succeeded);

        var aliceHistory = await context.ProjectAssignments
            .Where(pa => pa.ProjectId == 100 && pa.UserId == 3)
            .OrderBy(pa => pa.StartDate)
            .ToListAsync();

        Assert.Equal(2, aliceHistory.Count);
        Assert.Equal(new DateOnly(2026, 3, 1), aliceHistory[0].EndDate);
        Assert.Null(aliceHistory[1].EndDate);

        // 5. Point-in-time query on 2026-02-15 returns both Alice and Bob; on 2026-03-15 returns only Bob
        var teamInFeb = await service.GetTeamOnAsync(100, new DateOnly(2026, 2, 15));
        var teamInMar = await service.GetTeamOnAsync(100, new DateOnly(2026, 3, 15));
        Assert.Equal(2, teamInFeb.Count);
        Assert.Single(teamInMar);
        Assert.Equal(4, teamInMar[0].UserId);

        // 6. Verify audit logs were written for Assign and Remove
        var auditActions = await context.AuditLogs
            .Where(a => a.Entity == "Project" && a.EntityId == "100")
            .Select(a => a.Action)
            .ToListAsync();

        Assert.Contains("AssignedToTeam", auditActions);
        Assert.Contains("RemovedFromTeam", auditActions);
    }

    [Fact]
    public async Task InactiveUser_CannotBeAssigned_AndCompletedProjectRequiresAdminOverrideWithReason()
    {
        var (context, service) = CreateTestEnvironment();
        await SeedBaselineDataAsync(context);

        // 1. Deactivated user (5) cannot be assigned
        var inactiveResult = await service.AssignMembersAsync(
            projectId: 100,
            requests: [new MemberAssignmentRequest(5, 2, new DateOnly(2026, 2, 1))],
            performedByUserId: 1,
            isAdmin: true);

        Assert.False(inactiveResult.Succeeded);
        Assert.Contains("deactivated", inactiveResult.ErrorMessage);

        // 2. Assigning to Completed project (101) as Manager is blocked
        var managerClosedProjectResult = await service.AssignMembersAsync(
            projectId: 101,
            requests: [new MemberAssignmentRequest(3, 2, new DateOnly(2026, 2, 1))],
            performedByUserId: 2,
            isAdmin: false);

        Assert.False(managerClosedProjectResult.Succeeded);

        // 3. Assigning to Completed project (101) as Admin WITHOUT reason is blocked
        var adminNoReasonResult = await service.AssignMembersAsync(
            projectId: 101,
            requests: [new MemberAssignmentRequest(3, 2, new DateOnly(2026, 2, 1))],
            performedByUserId: 1,
            isAdmin: true,
            adminOverrideReason: "");

        Assert.False(adminNoReasonResult.Succeeded);

        // 4. Assigning to Completed project (101) as Admin WITH reason succeeds
        var adminWithReasonResult = await service.AssignMembersAsync(
            projectId: 101,
            requests: [new MemberAssignmentRequest(3, 2, new DateOnly(2026, 2, 1))],
            performedByUserId: 1,
            isAdmin: true,
            adminOverrideReason: "Post-completion audit support");

        Assert.True(adminWithReasonResult.Succeeded);
    }

    [Fact]
    public async Task ProjectLead_AppearsOnTeam_CannotBeRemovedUntilNewLeadChosen_AndLeadChangeHonorsKeepFlag()
    {
        var (context, service) = CreateTestEnvironment();
        await SeedBaselineDataAsync(context);

        await service.EnsureLeadOnTeamAsync(100, 2, new DateOnly(2026, 1, 1), 2);
        var leadAssignment = await context.ProjectAssignments.FirstAsync(pa => pa.ProjectId == 100 && pa.UserId == 2);

        // Trying to remove the current Project Lead is blocked
        var removeLeadResult = await service.RemoveMemberAsync(100, leadAssignment.Id, new DateOnly(2026, 2, 1), "Try remove lead", 1);
        Assert.False(removeLeadResult.Succeeded);
        Assert.Contains("Project Lead", removeLeadResult.ErrorMessage);

        // Changing lead from 2 to 1 with keepOldLeadOnTeam = false closes user 2's assignment and adds user 1 as Lead
        await service.HandleLeadChangeAsync(100, oldLeadUserId: 2, newLeadUserId: 1, keepOldLeadOnTeam: false, performedByUserId: 1);

        var oldLeadReloaded = await context.ProjectAssignments.FirstAsync(pa => pa.Id == leadAssignment.Id);
        var newLeadAssignment = await context.ProjectAssignments.FirstAsync(pa => pa.ProjectId == 100 && pa.UserId == 1 && pa.EndDate == null);

        Assert.NotNull(oldLeadReloaded.EndDate);
        Assert.Equal(1, newLeadAssignment.RoleOnProjectId); // Lead role
    }

    [Fact]
    public async Task DeactivationBlocker_BlocksUserWhoLeadsProjectsOrHasActiveAssignments()
    {
        var (context, service) = CreateTestEnvironment();
        await SeedBaselineDataAsync(context);

        await service.AssignMembersAsync(100, [new MemberAssignmentRequest(3, 2, new DateOnly(2026, 2, 1))], 2, false);

        // User 2 leads Project 100
        var leadBlocker = await service.GetDeactivationBlockersAsync(2);
        Assert.True(leadBlocker.IsBlocked);
        Assert.NotEmpty(leadBlocker.LedProjects);

        // User 3 has an active assignment on Project 100
        var empBlocker = await service.GetDeactivationBlockersAsync(3);
        Assert.True(empBlocker.IsBlocked);
        Assert.NotEmpty(empBlocker.ActiveAssignedProjects);

        // User 4 has neither -> not blocked
        var freeUserBlocker = await service.GetDeactivationBlockersAsync(4);
        Assert.False(freeUserBlocker.IsBlocked);
    }

    [Fact]
    public void SchemaAndService_EnforceNoHardDeletes_AndFilteredUniqueIndexOnActiveAssignments()
    {
        var (context, _) = CreateTestEnvironment();

        // 1. Verify ITeamAssignmentService exposes no hard-delete methods
        var methods = typeof(ITeamAssignmentService).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(methods, m => m.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));

        // 2. Verify EF Core model metadata for ProjectAssignment
        var entityType = context.Model.FindEntityType(typeof(ProjectAssignment));
        Assert.NotNull(entityType);

        // Verify all FKs on ProjectAssignment use DeleteBehavior.Restrict
        foreach (var fk in entityType!.GetForeignKeys())
        {
            Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        }

        // Verify filtered unique index on (ProjectId, UserId) WHERE [EndDate] IS NULL
        var activeIndex = entityType.GetIndexes()
            .FirstOrDefault(ix => ix.IsUnique && ix.Properties.Select(p => p.Name).SequenceEqual(["ProjectId", "UserId"]));

        Assert.NotNull(activeIndex);
        Assert.Equal("[EndDate] IS NULL", activeIndex!.GetFilter());
    }
}

