using System.ComponentModel.DataAnnotations;
using AspnetCoreMvcFull.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AspnetCoreMvcFull.ViewModels.Projects;

public class ProjectListItemViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> ClientNames { get; set; } = [];
    public int LeadId { get; set; }
    public string LeadName { get; set; } = string.Empty;
    public string LeadEmail { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; }
    public ProjectPriority Priority { get; set; }
    public ProgressMode ProgressMode { get; set; }
    public int ProgressPercent { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public bool IsOverdue { get; set; }
    public int DaysOverdue { get; set; }
    public bool IsArchived { get; set; }
    public bool CanEdit { get; set; }
    public bool CanArchive { get; set; }
}

public class ProjectListViewModel
{
    public List<ProjectListItemViewModel> Projects { get; set; } = [];

    // Filters
    public string? Search { get; set; }
    public ProjectStatus? Status { get; set; }
    public int? ClientId { get; set; }
    public int? LeadId { get; set; }
    public ProjectPriority? Priority { get; set; }
    public DateOnly? StartDateFrom { get; set; }
    public DateOnly? PlannedEndBefore { get; set; }
    public string? ArchiveFilter { get; set; } = "active"; // "active", "archived", "all"

    // Sorting & Pagination
    public string? SortBy { get; set; } = "code";
    public string? SortOrder { get; set; } = "asc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    // Filter Options
    public List<SelectListItem> ClientOptions { get; set; } = [];
    public List<SelectListItem> LeadOptions { get; set; } = [];
}

public class ProjectFormViewModel : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Project code is required.")]
    [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters.")]
    public string Code { get; set; } = string.Empty;

    public bool IsCodeEditable { get; set; } = false;

    [Required(ErrorMessage = "Project name is required.")]
    [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Project lead is required.")]
    [Display(Name = "Project Lead")]
    public int LeadId { get; set; }

    [Display(Name = "Associated Clients")]
    public List<int> SelectedClientIds { get; set; } = [];

    public ProjectStatus Status { get; set; } = ProjectStatus.NotStarted;

    public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;

    [Display(Name = "Progress Calculation Mode")]
    public ProgressMode ProgressMode { get; set; } = ProgressMode.Manual;

    [Range(0, 100, ErrorMessage = "Progress must be between 0 and 100%.")]
    [Display(Name = "Progress (%)")]
    public int ProgressPercent { get; set; } = 0;

    [Required(ErrorMessage = "Start date is required.")]
    [Display(Name = "Start Date")]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required(ErrorMessage = "Planned end date is required.")]
    [Display(Name = "Planned End Date")]
    public DateOnly PlannedEndDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3));

    [Display(Name = "Actual End Date")]
    public DateOnly? ActualEndDate { get; set; }

    public string? RowVersion { get; set; }

    // Dropdown SelectLists
    public List<SelectListItem> ClientOptions { get; set; } = [];
    public List<SelectListItem> LeadOptions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PlannedEndDate < StartDate)
        {
            yield return new ValidationResult(
                "Planned end date cannot be earlier than the start date.",
                [nameof(PlannedEndDate)]);
        }

        if (ActualEndDate.HasValue && ActualEndDate.Value < StartDate)
        {
            yield return new ValidationResult(
                "Actual end date cannot be earlier than the start date.",
                [nameof(ActualEndDate)]);
        }

        if (Status is ProjectStatus.Completed or ProjectStatus.Cancelled && !ActualEndDate.HasValue)
        {
            yield return new ValidationResult(
                $"Actual end date is required when project status is {Status}.",
                [nameof(ActualEndDate)]);
        }
    }
}

public class ProjectDetailsViewModel
{
    public Project Project { get; set; } = null!;
    public bool CanEdit { get; set; }
    public bool CanArchive { get; set; }
    public List<AuditLog> RecentAuditLogs { get; set; } = [];
}

public class ProjectConflictViewModel
{
    public int ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public ProjectFormViewModel SubmittedModel { get; set; } = null!;
    public Project CurrentDatabaseProject { get; set; } = null!;
}
