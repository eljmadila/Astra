using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AspnetCoreMvcFull.Entities;

public class Project
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int LeadId { get; set; }
    public ApplicationUser? Lead { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.NotStarted;

    public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;

    public ProgressMode ProgressMode { get; set; } = ProgressMode.Manual;

    [Range(0, 100)]
    public int ProgressPercent { get; set; } = 0;

    public DateOnly StartDate { get; set; }

    public DateOnly PlannedEndDate { get; set; }

    public DateOnly? ActualEndDate { get; set; }

    public bool IsArchived { get; set; } = false;

    public int CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    // Navigation properties
    public ICollection<ProjectClient> ProjectClients { get; set; } = new List<ProjectClient>();

    // Derived helpers (computed in memory, not stored in DB)
    [NotMapped]
    public bool IsOverdue =>
        Status != ProjectStatus.Completed &&
        Status != ProjectStatus.Cancelled &&
        PlannedEndDate < DateOnly.FromDateTime(DateTime.UtcNow);

    [NotMapped]
    public int DaysOverdue =>
        IsOverdue ? (DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - PlannedEndDate.DayNumber) : 0;
}
