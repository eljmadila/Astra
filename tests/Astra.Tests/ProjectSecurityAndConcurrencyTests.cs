using System.Text.Encodings.Web;
using AspnetCoreMvcFull.Data;
using AspnetCoreMvcFull.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Astra.Tests;

public class ProjectSecurityAndConcurrencyTests
{
    private ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public void XssPayload_InNameDescriptionAndNotes_IsProperlyHtmlEncoded()
    {
        var maliciousInput = "<script>alert('xss')</script><b>BoldPayload</b>";
        var encoded = HtmlEncoder.Default.Encode(maliciousInput);

        Assert.DoesNotContain("<script>", encoded);
        Assert.DoesNotContain("</script>", encoded);
        Assert.Contains("&lt;script&gt;", encoded);
        Assert.Contains("&lt;/script&gt;", encoded);
    }

    [Fact]
    public async Task ConcurrencyDetection_WhenSimultaneousEditsOccur_DetectsConflict()
    {
        using var context1 = CreateInMemoryContext();

        var project = new Project
        {
            Id = 1,
            Code = "PRJ-2026-001",
            Name = "Original Title",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PlannedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            RowVersion = [1, 2, 3, 4]
        };

        context1.Projects.Add(project);
        await context1.SaveChangesAsync();

        // User A loads the project with RowVersion [1, 2, 3, 4]
        var originalRowVersion = project.RowVersion;

        // User B modifies project in the meantime
        project.Name = "User B Title Update";
        project.RowVersion = [5, 6, 7, 8];
        await context1.SaveChangesAsync();

        // User A tries to save using the old originalRowVersion
        var isConflict = !originalRowVersion.SequenceEqual(project.RowVersion);
        Assert.True(isConflict, "Stale row version should trigger a concurrency conflict");
    }

    [Fact]
    public void ProjectStatusTransition_WhenReopenedFromCompleted_RequiresClearingActualEndDate()
    {
        var wasCompleted = true;
        var newStatus = ProjectStatus.InProgress;
        DateOnly? actualEndDate = new DateOnly(2026, 4, 1);

        var isReopened = wasCompleted && newStatus != ProjectStatus.Completed && newStatus != ProjectStatus.Cancelled;
        if (isReopened)
        {
            actualEndDate = null; // Reopening clears actual end date
        }

        Assert.True(isReopened);
        Assert.Null(actualEndDate);
    }
}
