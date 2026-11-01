using System.ComponentModel.DataAnnotations;
using AspnetCoreMvcFull.Entities;
using AspnetCoreMvcFull.ViewModels.Projects;
using Xunit;

namespace Astra.Tests;

public class ProjectValidationAndRulesTests
{
    [Fact]
    public void ProjectForm_WhenPlannedEndBeforeStart_FailsValidation()
    {
        var model = new ProjectFormViewModel
        {
            Code = "PRJ-2026-001",
            Name = "Test Project",
            LeadId = 1,
            StartDate = new DateOnly(2026, 6, 1),
            PlannedEndDate = new DateOnly(2026, 5, 1) // Before start!
        };

        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model);
        var isValid = Validator.TryValidateObject(model, context, validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("Planned end date cannot be earlier"));
    }

    [Fact]
    public void ProjectForm_WhenActualEndBeforeStart_FailsValidation()
    {
        var model = new ProjectFormViewModel
        {
            Code = "PRJ-2026-001",
            Name = "Test Project",
            LeadId = 1,
            StartDate = new DateOnly(2026, 6, 1),
            PlannedEndDate = new DateOnly(2026, 7, 1),
            ActualEndDate = new DateOnly(2026, 5, 15) // Before start!
        };

        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model);
        var isValid = Validator.TryValidateObject(model, context, validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("Actual end date cannot be earlier"));
    }

    [Theory]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Cancelled)]
    public void ProjectForm_WhenStatusIsCompletedOrCancelled_RequiresActualEndDate(ProjectStatus status)
    {
        var model = new ProjectFormViewModel
        {
            Code = "PRJ-2026-001",
            Name = "Test Project",
            LeadId = 1,
            Status = status,
            StartDate = new DateOnly(2026, 1, 1),
            PlannedEndDate = new DateOnly(2026, 6, 1),
            ActualEndDate = null // Missing!
        };

        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model);
        var isValid = Validator.TryValidateObject(model, context, validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains($"Actual end date is required when project status is {status}"));
    }

    [Fact]
    public void Project_OverdueCalculation_WhenPastPlannedDateAndNotCompleted_ReturnsTrue()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var project = new Project
        {
            Code = "PRJ-2026-004",
            Name = "Overdue Initiative",
            Status = ProjectStatus.InProgress,
            StartDate = today.AddMonths(-2),
            PlannedEndDate = today.AddDays(-5) // 5 days in past
        };

        Assert.True(project.IsOverdue);
        Assert.Equal(5, project.DaysOverdue);
    }

    [Fact]
    public void Project_OverdueCalculation_WhenCompleted_ReturnsFalseEvenIfPastPlannedDate()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var project = new Project
        {
            Code = "PRJ-2026-005",
            Name = "Completed Initiative",
            Status = ProjectStatus.Completed,
            StartDate = today.AddMonths(-3),
            PlannedEndDate = today.AddDays(-10),
            ActualEndDate = today.AddDays(-10)
        };

        Assert.False(project.IsOverdue);
        Assert.Equal(0, project.DaysOverdue);
    }
}
