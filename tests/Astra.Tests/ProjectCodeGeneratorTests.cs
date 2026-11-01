using AspnetCoreMvcFull.Data;
using AspnetCoreMvcFull.Entities;
using AspnetCoreMvcFull.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Astra.Tests;

public class ProjectCodeGeneratorTests
{
    private ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GenerateNextCodeAsync_WhenNoProjectsExist_ReturnsFirstCodeForCurrentYear()
    {
        using var context = CreateInMemoryContext();
        var generator = new ProjectCodeGenerator(context);

        var code = await generator.GenerateNextCodeAsync();
        var expectedYear = DateTime.UtcNow.Year;

        Assert.Equal($"PRJ-{expectedYear}-001", code);
    }

    [Fact]
    public async Task GenerateNextCodeAsync_WhenProjectsExist_IncrementsHighestSequence()
    {
        using var context = CreateInMemoryContext();
        var year = DateTime.UtcNow.Year;

        context.Projects.AddRange(
            new Project { Code = $"PRJ-{year}-001", Name = "Alpha", StartDate = DateOnly.FromDateTime(DateTime.UtcNow), PlannedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)) },
            new Project { Code = $"PRJ-{year}-005", Name = "Beta", StartDate = DateOnly.FromDateTime(DateTime.UtcNow), PlannedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)) },
            new Project { Code = $"PRJ-{year}-002", Name = "Gamma", StartDate = DateOnly.FromDateTime(DateTime.UtcNow), PlannedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)) }
        );
        await context.SaveChangesAsync();

        var generator = new ProjectCodeGenerator(context);
        var code = await generator.GenerateNextCodeAsync();

        Assert.Equal($"PRJ-{year}-006", code);
    }
}
