using System.ComponentModel.DataAnnotations;
using AspnetCoreMvcFull.Entities;

namespace AspnetCoreMvcFull.ViewModels.ProjectRoles;

public class ProjectRoleItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int UsageCount { get; set; }
}

public class ProjectRoleListViewModel
{
    public List<ProjectRoleItemViewModel> Roles { get; set; } = [];
    public ProjectRoleFormViewModel NewRoleForm { get; set; } = new();
}

public class ProjectRoleFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Role name is required.")]
    [StringLength(100, ErrorMessage = "Role name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
