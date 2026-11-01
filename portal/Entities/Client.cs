using System.ComponentModel.DataAnnotations;

namespace AspnetCoreMvcFull.Entities;

public class Client
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? ContactPerson { get; set; }

    [MaxLength(256)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(50)]
    [Phone]
    public string? Phone { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    // Navigation properties
    public ICollection<ProjectClient> ProjectClients { get; set; } = new List<ProjectClient>();
}
