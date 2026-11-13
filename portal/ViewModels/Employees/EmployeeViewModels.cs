using AspnetCoreMvcFull.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AspnetCoreMvcFull.ViewModels.Employees;

public class EmployeeListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string SystemRole { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ActiveProjectsCount { get; set; }
    public int PastProjectsCount { get; set; }
    public List<string> CurrentProjectCodes { get; set; } = [];
}

public class EmployeeListViewModel
{
    public List<EmployeeListItemViewModel> Employees { get; set; } = [];

    // Filters
    public string? Search { get; set; }
    public string? Department { get; set; }
    public string? StatusFilter { get; set; } = "active"; // "active", "inactive", "all"
    public string? RoleFilter { get; set; }

    // Sorting & Pagination
    public string? SortBy { get; set; } = "name";
    public string? SortOrder { get; set; } = "asc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public List<SelectListItem> DepartmentOptions { get; set; } = [];
    public List<string> RoleOptions { get; set; } = [];
    public bool IsEmployeeScoped { get; set; }
}

public class EmployeeProjectEngagementViewModel
{
    public int AssignmentId { get; set; }
    public int ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public ProjectStatus ProjectStatus { get; set; }
    public bool IsProjectArchived { get; set; }
    public List<string> ClientNames { get; set; } = [];
    public string RoleOnProject { get; set; } = string.Empty;
    public bool IsProjectLead { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? RemovalReason { get; set; }
}

public class EmployeeProfileViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string SystemRole { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public List<EmployeeProjectEngagementViewModel> CurrentProjects { get; set; } = [];
    public List<EmployeeProjectEngagementViewModel> PastProjects { get; set; } = [];

    public bool IsAdmin { get; set; }
    public bool IsSelf { get; set; }
}

