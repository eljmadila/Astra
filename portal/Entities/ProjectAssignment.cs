using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AspnetCoreMvcFull.Entities;

public class ProjectAssignment
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public int RoleOnProjectId { get; set; }
    public ProjectRole RoleOnProject { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [MaxLength(500)]
    public string? RemovalReason { get; set; }

    public int CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Spec Section 4.3 / Week 3 Task 5:
    /// Active on date D means start_date &lt;= D AND (end_date IS NULL OR end_date &gt; D).
    /// </summary>
    public bool IsActiveOn(DateOnly date) =>
        StartDate <= date && (!EndDate.HasValue || EndDate.Value > date);

    [NotMapped]
    public bool IsCurrentlyActive => EndDate is null;
}

