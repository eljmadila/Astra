using AspnetCoreMvcFull.Entities;
using AspnetCoreMvcFull.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspnetCoreMvcFull.Data;

public static class DbInitializer
{
    public static readonly string[] Roles = ["Admin", "Manager", "Employee", "Viewer"];

    public static readonly string[] SeedProjectRoles =
    [
        "Lead",
        "Developer",
        "Analyst",
        "Designer",
        "Tester",
        "Support",
        "Other"
    ];

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

            // 1. Seed Identity Roles
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

                existingAdmin = new ApplicationUser
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

                var createResult = await userManager.CreateAsync(existingAdmin, adminPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(existingAdmin, "Admin");
                    logger.LogInformation("Seeded initial Admin user: {Email}", adminEmail);

                    await auditService.LogAsync(
                        action: "Seeded",
                        entity: "ApplicationUser",
                        entityId: existingAdmin.Id.ToString(),
                        details: new { Email = adminEmail, Role = "Admin", Note = "Initial System Provisioning" },
                        userId: existingAdmin.Id
                    );
                }
                else
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    logger.LogError("Failed to seed Admin user: {Errors}", errors);
                }
            }

            // 3. Seed Standard Role Test Users (Manager, Employee, Viewer)
            var testUsers = new[]
            {
                new { Email = "manager@astra.internal", Password = "Manager@Astra2026!", Name = "David Miller", Dept = "Engineering Operations", Role = "Manager", IsActive = true },
                new { Email = "sarah.manager@astra.internal", Password = "Manager@Astra2026!", Name = "Sarah Jenkins", Dept = "Enterprise Delivery", Role = "Manager", IsActive = true },
                new { Email = "employee@astra.internal", Password = "Employee@Astra2026!", Name = "Alex Rivera", Dept = "Core Development", Role = "Employee", IsActive = true },
                new { Email = "elena.karanja@astra.internal", Password = "Employee@Astra2026!", Name = "Elena Karanja", Dept = "Cloud & Infrastructure", Role = "Employee", IsActive = true },
                new { Email = "marcus.vance@astra.internal", Password = "Employee@Astra2026!", Name = "Marcus Vance", Dept = "Quality Assurance", Role = "Employee", IsActive = true },
                new { Email = "priya.patel@astra.internal", Password = "Employee@Astra2026!", Name = "Priya Patel", Dept = "Product Design & UX", Role = "Employee", IsActive = true },
                new { Email = "daniel.ochieng@astra.internal", Password = "Employee@Astra2026!", Name = "Daniel Ochieng", Dept = "Data & Analytics", Role = "Employee", IsActive = true },
                new { Email = "thomas.wright@astra.internal", Password = "Employee@Astra2026!", Name = "Thomas Wright", Dept = "Legacy Systems", Role = "Employee", IsActive = false },
                new { Email = "viewer@astra.internal", Password = "Viewer@Astra2026!", Name = "Rachel Zane", Dept = "Auditing & Compliance", Role = "Viewer", IsActive = true }
            };

            foreach (var userSpec in testUsers)
            {
                var user = await userManager.FindByEmailAsync(userSpec.Email);
                if (user is null)
                {
                    user = new ApplicationUser
                    {
                        UserName = userSpec.Email,
                        Email = userSpec.Email,
                        Name = userSpec.Name,
                        Department = userSpec.Dept,
                        EmailConfirmed = true,
                        IsActive = userSpec.IsActive,
                        MustChangePassword = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    var res = await userManager.CreateAsync(user, userSpec.Password);
                    if (res.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, userSpec.Role);
                        logger.LogInformation("Seeded test user: {Email} ({Role})", userSpec.Email, userSpec.Role);
                    }
                }
            }

            // 4. Seed Project Roles Lookup Table
            foreach (var roleName in SeedProjectRoles)
            {
                if (!await context.ProjectRoles.AnyAsync(r => r.Name == roleName))
                {
                    context.ProjectRoles.Add(new ProjectRole
                    {
                        Name = roleName,
                        IsActive = true
                    });
                }
            }
            await context.SaveChangesAsync();

            // 5. Seed Clients
            if (!await context.Clients.AnyAsync())
            {
                var clients = new List<Client>
                {
                    new()
                    {
                        Name = "Acme Global Industries",
                        ContactPerson = "Alice Vance",
                        Email = "alice.vance@acmeglobal.com",
                        Phone = "+1 555-0199",
                        Notes = "Enterprise manufacturing and supply chain partner.",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow.AddMonths(-6),
                        UpdatedAt = DateTime.UtcNow.AddMonths(-6)
                    },
                    new()
                    {
                        Name = "Nexus Health Technologies",
                        ContactPerson = "Dr. Robert Chen",
                        Email = "rchen@nexushealth.org",
                        Phone = "+1 555-0123",
                        Notes = "HIPAA-regulated health informatics and telecare infrastructure.",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow.AddMonths(-5),
                        UpdatedAt = DateTime.UtcNow.AddMonths(-5)
                    },
                    new()
                    {
                        Name = "Apex FinTech Solutions",
                        ContactPerson = "Marcus Sterling",
                        Email = "msterling@apexfin.io",
                        Phone = "+1 555-0144",
                        Notes = "Financial settlement gateway and high-frequency ledger processing.",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow.AddMonths(-4),
                        UpdatedAt = DateTime.UtcNow.AddMonths(-4)
                    },
                    new()
                    {
                        Name = "Veritas Logistics Global",
                        ContactPerson = "Elena Rostova",
                        Email = "elena.rostova@veritaslog.com",
                        Phone = "+1 555-0188",
                        Notes = "Global freight forwarder and warehouse logistics management.",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow.AddMonths(-3),
                        UpdatedAt = DateTime.UtcNow.AddMonths(-3)
                    },
                    new()
                    {
                        Name = "Starlight Media Group",
                        ContactPerson = "Liam Gallagher",
                        Email = "lgallagher@starlightmedia.com",
                        Phone = "+1 555-0177",
                        Notes = "Digital content distribution and subscription streaming services.",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow.AddMonths(-2),
                        UpdatedAt = DateTime.UtcNow.AddMonths(-2)
                    }
                };

                context.Clients.AddRange(clients);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded 5 initial clients.");
            }

            // 6. Seed Projects
            if (!await context.Projects.AnyAsync())
            {
                var adminUser = await userManager.FindByEmailAsync("admin@astra.internal");
                var managerUser = await userManager.FindByEmailAsync("manager@astra.internal");
                var sarahUser = await userManager.FindByEmailAsync("sarah.manager@astra.internal");

                var clientAcme = await context.Clients.FirstAsync(c => c.Name.Contains("Acme"));
                var clientNexus = await context.Clients.FirstAsync(c => c.Name.Contains("Nexus"));
                var clientApex = await context.Clients.FirstAsync(c => c.Name.Contains("Apex"));
                var clientVeritas = await context.Clients.FirstAsync(c => c.Name.Contains("Veritas"));

                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                var p1 = new Project
                {
                    Code = "PRJ-2026-001",
                    Name = "Cloud Core Infrastructure Modernization",
                    Description = "Multi-region hybrid cloud migration with Zero-Trust workload segmentation and automated failover.",
                    LeadId = adminUser!.Id,
                    CreatedById = adminUser.Id,
                    Status = ProjectStatus.InProgress,
                    Priority = ProjectPriority.Critical,
                    ProgressMode = ProgressMode.Manual,
                    ProgressPercent = 55,
                    StartDate = today.AddMonths(-2),
                    PlannedEndDate = today.AddMonths(1),
                    IsArchived = false,
                    CreatedAt = DateTime.UtcNow.AddMonths(-2),
                    UpdatedAt = DateTime.UtcNow.AddDays(-2)
                };

                var p2 = new Project
                {
                    Code = "PRJ-2026-002",
                    Name = "Patient Portal HIPAA Compliance Engine",
                    Description = "End-to-end cryptographic audit trails and consent management for patient electronic records.",
                    LeadId = managerUser!.Id,
                    CreatedById = managerUser.Id,
                    Status = ProjectStatus.InProgress,
                    Priority = ProjectPriority.High,
                    ProgressMode = ProgressMode.Manual,
                    ProgressPercent = 75,
                    StartDate = today.AddMonths(-1),
                    PlannedEndDate = today.AddMonths(2),
                    IsArchived = false,
                    CreatedAt = DateTime.UtcNow.AddMonths(-1),
                    UpdatedAt = DateTime.UtcNow.AddDays(-5)
                };

                var p3 = new Project
                {
                    Code = "PRJ-2026-003",
                    Name = "NextGen Payment Settlement Gateway",
                    Description = "Low-latency microservice architecture processing cross-border tokenized settlements.",
                    LeadId = sarahUser!.Id,
                    CreatedById = sarahUser.Id,
                    Status = ProjectStatus.NotStarted,
                    Priority = ProjectPriority.High,
                    ProgressMode = ProgressMode.Manual,
                    ProgressPercent = 0,
                    StartDate = today.AddDays(7),
                    PlannedEndDate = today.AddMonths(4),
                    IsArchived = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    UpdatedAt = DateTime.UtcNow.AddDays(-3)
                };

                // PRJ-2026-004: Intentionally OVERDUE to verify overdue calculation & badge
                var p4 = new Project
                {
                    Code = "PRJ-2026-004",
                    Name = "Legacy ERP Warehouse Fleet Integration",
                    Description = "Automated real-time inventory synchronization across legacy AS400 and edge mobile terminals.",
                    LeadId = managerUser.Id,
                    CreatedById = managerUser.Id,
                    Status = ProjectStatus.InProgress,
                    Priority = ProjectPriority.Critical,
                    ProgressMode = ProgressMode.Manual,
                    ProgressPercent = 65,
                    StartDate = today.AddMonths(-3),
                    PlannedEndDate = today.AddDays(-10), // OVERDUE by 10 days
                    IsArchived = false,
                    CreatedAt = DateTime.UtcNow.AddMonths(-3),
                    UpdatedAt = DateTime.UtcNow.AddDays(-1)
                };

                // PRJ-2026-005: Completed project with ActualEndDate and 100% progress
                var p5 = new Project
                {
                    Code = "PRJ-2026-005",
                    Name = "Enterprise Security Baseline & Identity Hardening",
                    Description = "Implementation of strict password policies, account lockouts, and immutable audit logging.",
                    LeadId = adminUser.Id,
                    CreatedById = adminUser.Id,
                    Status = ProjectStatus.Completed,
                    Priority = ProjectPriority.Medium,
                    ProgressMode = ProgressMode.Manual,
                    ProgressPercent = 100,
                    StartDate = today.AddMonths(-4),
                    PlannedEndDate = today.AddMonths(-1),
                    ActualEndDate = today.AddMonths(-1),
                    IsArchived = false,
                    CreatedAt = DateTime.UtcNow.AddMonths(-4),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-1)
                };

                // PRJ-2026-006: On Hold
                var p6 = new Project
                {
                    Code = "PRJ-2026-006",
                    Name = "Telematics Fleet Route Optimization AI",
                    Description = "Predictive route dispatch based on weather, traffic, and fuel efficiency metrics.",
                    LeadId = sarahUser.Id,
                    CreatedById = sarahUser.Id,
                    Status = ProjectStatus.OnHold,
                    Priority = ProjectPriority.Low,
                    ProgressMode = ProgressMode.Manual,
                    ProgressPercent = 20,
                    StartDate = today.AddMonths(-2),
                    PlannedEndDate = today.AddMonths(3),
                    IsArchived = false,
                    CreatedAt = DateTime.UtcNow.AddMonths(-2),
                    UpdatedAt = DateTime.UtcNow.AddDays(-14)
                };

                // PRJ-2026-007: Cancelled project
                var p7 = new Project
                {
                    Code = "PRJ-2026-007",
                    Name = "Automated Billing Reconciliation Microservice",
                    Description = "Superseded by ERP warehouse modernization initiative.",
                    LeadId = managerUser.Id,
                    CreatedById = managerUser.Id,
                    Status = ProjectStatus.Cancelled,
                    Priority = ProjectPriority.Low,
                    ProgressMode = ProgressMode.Manual,
                    ProgressPercent = 15,
                    StartDate = today.AddMonths(-5),
                    PlannedEndDate = today.AddMonths(-2),
                    ActualEndDate = today.AddMonths(-2),
                    IsArchived = false,
                    CreatedAt = DateTime.UtcNow.AddMonths(-5),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-2)
                };

                context.Projects.AddRange(p1, p2, p3, p4, p5, p6, p7);
                await context.SaveChangesAsync();

                // Link clients
                context.ProjectClients.AddRange(
                    new ProjectClient { ProjectId = p1.Id, ClientId = clientAcme.Id },
                    new ProjectClient { ProjectId = p2.Id, ClientId = clientNexus.Id },
                    new ProjectClient { ProjectId = p3.Id, ClientId = clientApex.Id },
                    new ProjectClient { ProjectId = p4.Id, ClientId = clientVeritas.Id },
                    new ProjectClient { ProjectId = p6.Id, ClientId = clientVeritas.Id },
                    new ProjectClient { ProjectId = p7.Id, ClientId = clientAcme.Id }
                );
                await context.SaveChangesAsync();

                logger.LogInformation("Seeded 7 initial projects with client linkages.");
            }

            // 7. Seed Project Assignments (Week 3: Current & Historical Team Roster)
            if (!await context.ProjectAssignments.AnyAsync())
            {
                var projects = await context.Projects.OrderBy(p => p.Code).ToListAsync();
                var roles = await context.ProjectRoles.ToDictionaryAsync(r => r.Name, r => r.Id);

                var adminUser = await userManager.FindByEmailAsync("admin@astra.internal");
                var davidManager = await userManager.FindByEmailAsync("manager@astra.internal");
                var sarahManager = await userManager.FindByEmailAsync("sarah.manager@astra.internal");
                var alexEmp = await userManager.FindByEmailAsync("employee@astra.internal");
                var elenaEmp = await userManager.FindByEmailAsync("elena.karanja@astra.internal");
                var marcusEmp = await userManager.FindByEmailAsync("marcus.vance@astra.internal");
                var priyaEmp = await userManager.FindByEmailAsync("priya.patel@astra.internal");
                var danielEmp = await userManager.FindByEmailAsync("daniel.ochieng@astra.internal");
                var thomasInactive = await userManager.FindByEmailAsync("thomas.wright@astra.internal");

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var seedAssignments = new List<ProjectAssignment>();

                // Ensure every project has its Lead on the team in the Lead role
                foreach (var proj in projects)
                {
                    var isClosedProject = proj.Status is ProjectStatus.Completed or ProjectStatus.Cancelled;
                    seedAssignments.Add(new ProjectAssignment
                    {
                        ProjectId = proj.Id,
                        UserId = proj.LeadId,
                        RoleOnProjectId = roles["Lead"],
                        StartDate = proj.StartDate,
                        EndDate = isClosedProject ? proj.ActualEndDate : null,
                        RemovalReason = isClosedProject ? $"Project {proj.Status}" : null,
                        CreatedById = proj.CreatedById,
                        CreatedAt = proj.CreatedAt
                    });
                }

                var p1 = projects.FirstOrDefault(p => p.Code == "PRJ-2026-001");
                var p2 = projects.FirstOrDefault(p => p.Code == "PRJ-2026-002");
                var p3 = projects.FirstOrDefault(p => p.Code == "PRJ-2026-003");
                var p4 = projects.FirstOrDefault(p => p.Code == "PRJ-2026-004");
                var p5 = projects.FirstOrDefault(p => p.Code == "PRJ-2026-005");

                if (p1 != null && alexEmp != null && elenaEmp != null && marcusEmp != null && thomasInactive != null)
                {
                    // Alex Rivera: had an earlier Analyst stint on PRJ-2026-001, then re-added as Developer
                    seedAssignments.Add(new ProjectAssignment
                    {
                        ProjectId = p1.Id,
                        UserId = alexEmp.Id,
                        RoleOnProjectId = roles["Analyst"],
                        StartDate = p1.StartDate,
                        EndDate = p1.StartDate.AddDays(20),
                        RemovalReason = "Completed initial architecture discovery phase",
                        CreatedById = p1.LeadId,
                        CreatedAt = DateTime.UtcNow.AddMonths(-2)
                    });

                    seedAssignments.Add(new ProjectAssignment
                    {
                        ProjectId = p1.Id,
                        UserId = alexEmp.Id,
                        RoleOnProjectId = roles["Developer"],
                        StartDate = p1.StartDate.AddDays(25),
                        EndDate = null,
                        CreatedById = p1.LeadId,
                        CreatedAt = DateTime.UtcNow.AddMonths(-1)
                    });

                    seedAssignments.Add(new ProjectAssignment
                    {
                        ProjectId = p1.Id,
                        UserId = elenaEmp.Id,
                        RoleOnProjectId = roles["Developer"],
                        StartDate = p1.StartDate.AddDays(3),
                        EndDate = null,
                        CreatedById = p1.LeadId,
                        CreatedAt = DateTime.UtcNow.AddMonths(-2)
                    });

                    seedAssignments.Add(new ProjectAssignment
                    {
                        ProjectId = p1.Id,
                        UserId = marcusEmp.Id,
                        RoleOnProjectId = roles["Tester"],
                        StartDate = p1.StartDate.AddDays(14),
                        EndDate = null,
                        CreatedById = p1.LeadId,
                        CreatedAt = DateTime.UtcNow.AddMonths(-1)
                    });

                    // Deactivated user with historical assignment
                    seedAssignments.Add(new ProjectAssignment
                    {
                        ProjectId = p1.Id,
                        UserId = thomasInactive.Id,
                        RoleOnProjectId = roles["Support"],
                        StartDate = p1.StartDate,
                        EndDate = p1.StartDate.AddDays(30),
                        RemovalReason = "Transitioned off project prior to account deactivation",
                        CreatedById = p1.LeadId,
                        CreatedAt = DateTime.UtcNow.AddMonths(-2)
                    });
                }

                if (p2 != null && alexEmp != null && priyaEmp != null && danielEmp != null)
                {
                    seedAssignments.Add(new ProjectAssignment
                    {
                        ProjectId = p2.Id,
                        UserId = alexEmp.Id,
                        RoleOnProjectId = roles["Developer"],
                        StartDate = p2.StartDate,
                        EndDate = null,
                        CreatedById = p2.LeadId,
                        CreatedAt = DateTime.UtcNow.AddMonths(-1)
                    });

                    seedAssignments.Add(new ProjectAssignment
                    {
                        ProjectId = p2.Id,
                        UserId = priyaEmp.Id,
                        RoleOnProjectId = roles["Designer"],
                        StartDate = p2.StartDate,
                        EndDate = null,
                        CreatedById = p2.LeadId,
                        CreatedAt = DateTime.UtcNow.AddMonths(-1)
                    });

                    seedAssignments.Add(new ProjectAssignment
                    {
                        ProjectId = p2.Id,
                        UserId = danielEmp.Id,
                        RoleOnProjectId = roles["Analyst"],
                        StartDate = p2.StartDate,
                        EndDate = today.AddDays(-5),
                        RemovalReason = "Completed HIPAA data mapping deliverable",
                        CreatedById = p2.LeadId,
                        CreatedAt = DateTime.UtcNow.AddMonths(-1)
                    });
                }

                if (p3 != null && priyaEmp != null && danielEmp != null)
                {
                    seedAssignments.Add(new ProjectAssignment
                    {
                        ProjectId = p3.Id,
                        UserId = danielEmp.Id,
                        RoleOnProjectId = roles["Analyst"],
                        StartDate = p3.StartDate,
                        EndDate = null,
                        CreatedById = p3.LeadId,
                        CreatedAt = DateTime.UtcNow.AddDays(-2)
                    });
                }

                if (p4 != null && elenaEmp != null && marcusEmp != null)
                {
                    seedAssignments.Add(new ProjectAssignment
                    {
                        ProjectId = p4.Id,
                        UserId = elenaEmp.Id,
                        RoleOnProjectId = roles["Developer"],
                        StartDate = p4.StartDate.AddDays(5),
                        EndDate = null,
                        CreatedById = p4.LeadId,
                        CreatedAt = DateTime.UtcNow.AddMonths(-3)
                    });

                    seedAssignments.Add(new ProjectAssignment
                    {
                        ProjectId = p4.Id,
                        UserId = marcusEmp.Id,
                        RoleOnProjectId = roles["Tester"],
                        StartDate = p4.StartDate.AddDays(15),
                        EndDate = null,
                        CreatedById = p4.LeadId,
                        CreatedAt = DateTime.UtcNow.AddMonths(-2)
                    });
                }

                if (p5 != null && alexEmp != null && thomasInactive != null)
                {
                    seedAssignments.Add(new ProjectAssignment
                    {
                        ProjectId = p5.Id,
                        UserId = alexEmp.Id,
                        RoleOnProjectId = roles["Developer"],
                        StartDate = p5.StartDate,
                        EndDate = p5.ActualEndDate ?? today.AddMonths(-1),
                        RemovalReason = "Project completed",
                        CreatedById = p5.LeadId,
                        CreatedAt = DateTime.UtcNow.AddMonths(-4)
                    });
                }

                context.ProjectAssignments.AddRange(seedAssignments);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} current and historical project team assignments.", seedAssignments.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the database.");
            throw;
        }
    }
}
