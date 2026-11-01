using System.ComponentModel.DataAnnotations;
using AspnetCoreMvcFull.Entities;

namespace AspnetCoreMvcFull.ViewModels.Clients;

public class ClientItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public int ActiveProjectsCount { get; set; }
    public int TotalProjectsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClientListViewModel
{
    public List<ClientItemViewModel> Clients { get; set; } = [];
    public string? Search { get; set; }
    public string? StatusFilter { get; set; } = "all";
    public string? SortBy { get; set; } = "name";
    public string? SortOrder { get; set; } = "asc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class ClientFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Client name is required.")]
    [StringLength(200, ErrorMessage = "Client name cannot exceed 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "Contact person cannot exceed 150 characters.")]
    public string? ContactPerson { get; set; }

    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string? Email { get; set; }

    [StringLength(50, ErrorMessage = "Phone cannot exceed 50 characters.")]
    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    public string? Phone { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public string? RowVersion { get; set; }
}

public class ClientProjectItemViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; }
    public ProjectPriority Priority { get; set; }
    public int ProgressPercent { get; set; }
    public string LeadName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsArchived { get; set; }
}

public class ClientDetailsViewModel
{
    public Client Client { get; set; } = null!;
    public List<ClientProjectItemViewModel> Projects { get; set; } = [];
}
