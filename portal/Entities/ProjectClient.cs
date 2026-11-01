namespace AspnetCoreMvcFull.Entities;

public class ProjectClient
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
