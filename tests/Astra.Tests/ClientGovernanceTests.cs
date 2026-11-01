using AspnetCoreMvcFull.Data;
using AspnetCoreMvcFull.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Astra.Tests;

public class ClientGovernanceTests
{
    private ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task DuplicateNameDetection_IsCaseInsensitive()
    {
        using var context = CreateInMemoryContext();
        context.Clients.Add(new Client
        {
            Name = "Acme Global Industries",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var candidateName1 = "acme global industries";
        var candidateName2 = "ACME GLOBAL INDUSTRIES";
        var candidateName3 = "  Acme Global Industries  ";

        var isDuplicate1 = await context.Clients
            .AnyAsync(c => c.Name.ToLower() == candidateName1.Trim().ToLower());
        var isDuplicate2 = await context.Clients
            .AnyAsync(c => c.Name.ToLower() == candidateName2.Trim().ToLower());
        var isDuplicate3 = await context.Clients
            .AnyAsync(c => c.Name.ToLower() == candidateName3.Trim().ToLower());

        Assert.True(isDuplicate1);
        Assert.True(isDuplicate2);
        Assert.True(isDuplicate3);
    }

    [Fact]
    public async Task ClientWithLinkedProjects_WhenDeleteAttempted_IsDeactivatedInsteadOfDeleted()
    {
        using var context = CreateInMemoryContext();

        var client = new Client
        {
            Id = 1,
            Name = "Nexus Health",
            IsActive = true
        };

        var project = new Project
        {
            Id = 10,
            Code = "PRJ-2026-001",
            Name = "Health Engine",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PlannedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1))
        };

        var projectClient = new ProjectClient
        {
            ProjectId = project.Id,
            ClientId = client.Id
        };

        context.Clients.Add(client);
        context.Projects.Add(project);
        context.ProjectClients.Add(projectClient);
        await context.SaveChangesAsync();

        // Simulate Controller Delete Action check
        var clientToDelete = await context.Clients
            .Include(c => c.ProjectClients)
            .FirstOrDefaultAsync(c => c.Id == client.Id);

        Assert.NotNull(clientToDelete);

        if (clientToDelete.ProjectClients.Count > 0)
        {
            clientToDelete.IsActive = false; // Deactivate instead of remove
            await context.SaveChangesAsync();
        }
        else
        {
            context.Clients.Remove(clientToDelete);
            await context.SaveChangesAsync();
        }

        // Verify client row still exists in DB but is deactivated
        var persisted = await context.Clients.FindAsync(client.Id);
        Assert.NotNull(persisted);
        Assert.False(persisted.IsActive);

        // Verify linked project is undisturbed
        var linkedProject = await context.Projects.FindAsync(project.Id);
        Assert.NotNull(linkedProject);
    }
}
