using System.Diagnostics;
using AspnetCoreMvcFull.Data;
using AspnetCoreMvcFull.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Astra.Tests;

public class ProjectPerformanceAndNPlusOneTests
{
    private ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ProjectListQuery_WithOneThousandProjects_ExecutesUnderBudget()
    {
        using var context = CreateInMemoryContext();

        // 1. Seed Lead and Client
        var lead = new ApplicationUser { Id = 1, Name = "Lead Manager", Email = "lead@astra.internal", Department = "Engineering" };
        var client = new Client { Id = 1, Name = "Enterprise Corp", IsActive = true };
        context.Users.Add(lead);
        context.Clients.Add(client);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var projects = new List<Project>(1000);

        for (int i = 1; i <= 1000; i++)
        {
            var p = new Project
            {
                Id = i,
                Code = $"PRJ-2026-{i:D4}",
                Name = $"High Volume Project Initiative #{i}",
                Description = $"Scalability test project description #{i} for performance benchmarks.",
                LeadId = 1,
                Status = (ProjectStatus)(i % 5),
                Priority = (ProjectPriority)(i % 4),
                ProgressMode = ProgressMode.Manual,
                ProgressPercent = (i * 7) % 101,
                StartDate = today.AddDays(-i),
                PlannedEndDate = today.AddDays(30 - (i % 60)),
                IsArchived = (i % 10 == 0)
            };
            projects.Add(p);
        }

        context.Projects.AddRange(projects);
        await context.SaveChangesAsync();

        // Seed some client linkages
        var linkages = projects.Take(500).Select(p => new ProjectClient
        {
            ProjectId = p.Id,
            ClientId = 1
        });
        context.ProjectClients.AddRange(linkages);
        await context.SaveChangesAsync();

        // 2. Measure Paged & Filtered Query Performance (Simulating /Projects/Index)
        var stopwatch = Stopwatch.StartNew();

        var page = 1;
        var pageSize = 25;

        var query = context.Projects
            .Include(p => p.Lead)
            .Include(p => p.ProjectClients)
                .ThenInclude(pc => pc.Client)
            .AsNoTracking()
            .Where(p => !p.IsArchived && p.Status == ProjectStatus.InProgress)
            .OrderBy(p => p.PlannedEndDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        var results = await query.ToListAsync();
        stopwatch.Stop();

        // Spec Requirement: Under 500 ms budget
        Assert.NotEmpty(results);
        Assert.True(stopwatch.ElapsedMilliseconds < 500, $"Expected query < 500ms, but was {stopwatch.ElapsedMilliseconds}ms");
    }
}
