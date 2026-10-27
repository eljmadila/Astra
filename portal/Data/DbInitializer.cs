using AspnetCoreMvcFull.Entities;
using AspnetCoreMvcFull.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspnetCoreMvcFull.Data;

public static class DbInitializer
{
    public static readonly string[] Roles = ["Admin", "Manager", "Employee", "Viewer"];

    public static async Task InitializeAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration,
        IAuditService auditService,
        ILogger logger)
    {
        try
        {
            // Apply migrations or ensure database exists
            if (context.Database.IsRelational())
            {
                await context.Database.MigrateAsync();
            }
            else
            {
                await context.Database.EnsureCreatedAsync();
            }

            // 1. Seed Roles
            foreach (var roleName in Roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var roleResult = await roleManager.CreateAsync(new ApplicationRole(roleName));
                    if (roleResult.Succeeded)
                    {
                        logger.LogInformation("Seeded role: {RoleName}", roleName);
                    }
                }
            }

            // 2. Seed Initial Admin Account
            var adminEmail = configuration["Astra:Admin:Email"] ?? "admin@astra.internal";
            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);

            if (existingAdmin is null)
            {
                var adminPassword = configuration["Astra:Admin:Password"] ?? "Admin@Astra2026!";
                var adminName = configuration["Astra:Admin:Name"] ?? "System Administrator";
                var adminDept = configuration["Astra:Admin:Department"] ?? "Executive Governance";

                var newAdmin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    Name = adminName,
                    Department = adminDept,
                    EmailConfirmed = true,
                    IsActive = true,
                    MustChangePassword = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(newAdmin, adminPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                    logger.LogInformation("Seeded initial Admin user: {Email}", adminEmail);

                    await auditService.LogAsync(
                        action: "Seeded",
                        entity: "ApplicationUser",
                        entityId: newAdmin.Id.ToString(),
                        details: new { Email = adminEmail, Role = "Admin", Note = "Initial System Provisioning" },
                        userId: newAdmin.Id
                    );
                }
                else
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    logger.LogError("Failed to seed Admin user: {Errors}", errors);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the database.");
            throw;
        }
    }
}
