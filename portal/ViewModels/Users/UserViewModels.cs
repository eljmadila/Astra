using System.ComponentModel.DataAnnotations;

namespace AspnetCoreMvcFull.ViewModels.Users;

public class UserListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class UserIndexViewModel
{
    public List<UserListItemViewModel> Users { get; set; } = [];
    public string? Search { get; set; }
    public string? RoleFilter { get; set; }
    public bool? ActiveFilter { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public List<string> AvailableRoles { get; set; } = [];
}

public class CreateUserViewModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    [Display(Name = "Full Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email Address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Corporate Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Department is required.")]
    [StringLength(100, ErrorMessage = "Department cannot exceed 100 characters.")]
    public string Department { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a role.")]
    public string Role { get; set; } = "Employee";

    [Display(Name = "Temporary Password (Optional - Leave blank to auto-generate)")]
    public string? InitialPassword { get; set; }
}

public class EditUserViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Corporate Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Department is required.")]
    [StringLength(100)]
    public string Department { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a role.")]
    public string Role { get; set; } = "Employee";

    public bool IsActive { get; set; }
}
