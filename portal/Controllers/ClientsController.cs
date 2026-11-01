using System.Security.Claims;
using AspnetCoreMvcFull.Data;
using AspnetCoreMvcFull.Entities;
using AspnetCoreMvcFull.Services;
using AspnetCoreMvcFull.ViewModels.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AspnetCoreMvcFull.Controllers;

[Authorize(Policy = "RequireViewerRole")]
public class ClientsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<ClientsController> _logger;

    public ClientsController(
        ApplicationDbContext context,
        IAuditService auditService,
        ILogger<ClientsController> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    // GET: /Clients
    public async Task<IActionResult> Index(
        string? search,
        string? statusFilter = "all",
        string? sortBy = "name",
        string? sortOrder = "asc",
        int page = 1,
        int pageSize = 10)
    {
        var query = _context.Clients
            .Include(c => c.ProjectClients)
            .AsNoTracking()
            .AsQueryable();

        // 1. Search across Name, Contact Person, Email, Phone
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c =>
                c.Name.Contains(s) ||
                (c.ContactPerson != null && c.ContactPerson.Contains(s)) ||
                (c.Email != null && c.Email.Contains(s)) ||
                (c.Phone != null && c.Phone.Contains(s)));
        }

        // 2. Active / Inactive / All Filter
        if (statusFilter == "active")
        {
            query = query.Where(c => c.IsActive);
        }
        else if (statusFilter == "inactive")
        {
            query = query.Where(c => !c.IsActive);
        }

        // 3. Sorting
        query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
        {
            ("name", "desc") => query.OrderByDescending(c => c.Name),
            ("name", _) => query.OrderBy(c => c.Name),
            ("contact", "desc") => query.OrderByDescending(c => c.ContactPerson),
            ("contact", _) => query.OrderBy(c => c.ContactPerson),
            ("email", "desc") => query.OrderByDescending(c => c.Email),
            ("email", _) => query.OrderBy(c => c.Email),
            ("projects", "desc") => query.OrderByDescending(c => c.ProjectClients.Count),
            ("projects", _) => query.OrderBy(c => c.ProjectClients.Count),
            ("status", "desc") => query.OrderByDescending(c => c.IsActive),
            ("status", _) => query.OrderBy(c => c.IsActive),
            ("created", "asc") => query.OrderBy(c => c.CreatedAt),
            ("created", _) => query.OrderByDescending(c => c.CreatedAt),
            _ => query.OrderBy(c => c.Name)
        };

        var totalItems = await query.CountAsync();

        // 4. Server-Side Pagination
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 5, 100);

        var clients = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(c => new ClientItemViewModel
            {
                Id = c.Id,
                Name = c.Name,
                ContactPerson = c.ContactPerson,
                Email = c.Email,
                Phone = c.Phone,
                IsActive = c.IsActive,
                TotalProjectsCount = c.ProjectClients.Count,
                ActiveProjectsCount = c.ProjectClients.Count(pc => pc.Project.Status == ProjectStatus.InProgress),
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        var viewModel = new ClientListViewModel
        {
            Clients = clients,
            Search = search,
            StatusFilter = statusFilter,
            SortBy = sortBy,
            SortOrder = sortOrder,
            Page = safePage,
            PageSize = safePageSize,
            TotalItems = totalItems
        };

        return View(viewModel);
    }

    // GET: /Clients/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var client = await _context.Clients
            .Include(c => c.ProjectClients)
                .ThenInclude(pc => pc.Project)
                    .ThenInclude(p => p.Lead)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client is null)
        {
            return NotFound();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var projects = client.ProjectClients
            .Select(pc => pc.Project)
            .Select(p => new ClientProjectItemViewModel
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Status = p.Status,
                Priority = p.Priority,
                ProgressPercent = p.ProgressPercent,
                LeadName = p.Lead?.Name ?? "Unassigned",
                StartDate = p.StartDate,
                PlannedEndDate = p.PlannedEndDate,
                IsOverdue = p.Status != ProjectStatus.Completed &&
                            p.Status != ProjectStatus.Cancelled &&
                            p.PlannedEndDate < today,
                IsArchived = p.IsArchived
            })
            .OrderByDescending(p => p.StartDate)
            .ToList();

        var model = new ClientDetailsViewModel
        {
            Client = client,
            Projects = projects
        };

        return View(model);
    }

    // GET: /Clients/Create
    [Authorize(Policy = "RequireManagerRole")]
    public IActionResult Create()
    {
        return View(new ClientFormViewModel());
    }

    // POST: /Clients/Create
    [HttpPost]
    [Authorize(Policy = "RequireManagerRole")]
    public async Task<IActionResult> Create(ClientFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError("Name", "Client name is required.");
        }
        else
        {
            var normalizedName = model.Name.Trim().ToLower();
            var exists = await _context.Clients
                .AnyAsync(c => c.Name.ToLower() == normalizedName);

            if (exists)
            {
                ModelState.AddModelError("Name", "A client with this name already exists (case-insensitive duplicate detected).");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var client = new Client
        {
            Name = model.Name.Trim(),
            ContactPerson = model.ContactPerson?.Trim(),
            Email = model.Email?.Trim(),
            Phone = model.Phone?.Trim(),
            Notes = model.Notes?.Trim(),
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Created",
            entity: "Client",
            entityId: client.Id.ToString(),
            details: new { client.Name, client.ContactPerson, client.Email, client.Phone, client.IsActive },
            userId: CurrentUserId
        );

        TempData["Success"] = $"Client '{client.Name}' created successfully.";
        return RedirectToAction(nameof(Details), new { id = client.Id });
    }

    // GET: /Clients/Edit/5
    [Authorize(Policy = "RequireManagerRole")]
    public async Task<IActionResult> Edit(int id)
    {
        var client = await _context.Clients.FindAsync(id);
        if (client is null)
        {
            return NotFound();
        }

        var model = new ClientFormViewModel
        {
            Id = client.Id,
            Name = client.Name,
            ContactPerson = client.ContactPerson,
            Email = client.Email,
            Phone = client.Phone,
            Notes = client.Notes,
            IsActive = client.IsActive,
            RowVersion = Convert.ToBase64String(client.RowVersion)
        };

        return View(model);
    }

    // POST: /Clients/Edit/5
    [HttpPost]
    [Authorize(Policy = "RequireManagerRole")]
    public async Task<IActionResult> Edit(int id, ClientFormViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError("Name", "Client name is required.");
        }
        else
        {
            var normalizedName = model.Name.Trim().ToLower();
            var duplicateExists = await _context.Clients
                .AnyAsync(c => c.Id != id && c.Name.ToLower() == normalizedName);

            if (duplicateExists)
            {
                ModelState.AddModelError("Name", "A client with this name already exists (case-insensitive duplicate detected).");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var client = await _context.Clients.FindAsync(id);
        if (client is null)
        {
            return NotFound();
        }

        try
        {
            if (!string.IsNullOrEmpty(model.RowVersion))
            {
                _context.Entry(client).Property(c => c.RowVersion).OriginalValue = Convert.FromBase64String(model.RowVersion);
            }

            client.Name = model.Name.Trim();
            client.ContactPerson = model.ContactPerson?.Trim();
            client.Email = model.Email?.Trim();
            client.Phone = model.Phone?.Trim();
            client.Notes = model.Notes?.Trim();
            client.IsActive = model.IsActive;
            client.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                action: "Updated",
                entity: "Client",
                entityId: client.Id.ToString(),
                details: new { client.Name, client.ContactPerson, client.Email, client.Phone, client.IsActive },
                userId: CurrentUserId
            );

            TempData["Success"] = $"Client '{client.Name}' updated successfully.";
            return RedirectToAction(nameof(Details), new { id = client.Id });
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrency conflict detected while editing Client Id {ClientId}", id);

            // Reload database values to present conflict to user
            await _context.Entry(client).ReloadAsync();
            ModelState.AddModelError(string.Empty, "Another user has updated this client since you loaded the page. Your changes were not saved. Please review the updated values below and submit again.");
            model.RowVersion = Convert.ToBase64String(client.RowVersion);
            return View(model);
        }
    }

    // POST: /Clients/ToggleStatus/5
    [HttpPost]
    [Authorize(Policy = "RequireManagerRole")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var client = await _context.Clients.FindAsync(id);
        if (client is null)
        {
            return NotFound();
        }

        client.IsActive = !client.IsActive;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var actionName = client.IsActive ? "Activated" : "Deactivated";
        await _auditService.LogAsync(
            action: actionName,
            entity: "Client",
            entityId: client.Id.ToString(),
            details: new { client.Name, client.IsActive },
            userId: CurrentUserId
        );

        TempData["Success"] = $"Client '{client.Name}' was {actionName.ToLower()} successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Clients/Delete/5
    [HttpPost]
    [Authorize(Policy = "RequireManagerRole")]
    public async Task<IActionResult> Delete(int id)
    {
        var client = await _context.Clients
            .Include(c => c.ProjectClients)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client is null)
        {
            return NotFound();
        }

        // Spec Requirement: Deactivate instead of delete when a client is linked to any project
        if (client.ProjectClients.Count > 0)
        {
            client.IsActive = false;
            client.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                action: "Deactivated",
                entity: "Client",
                entityId: client.Id.ToString(),
                details: new
                {
                    client.Name,
                    client.IsActive,
                    Reason = "Linked to active projects - deletion converted to deactivation per governance policy."
                },
                userId: CurrentUserId
            );

            TempData["Warning"] = $"Client '{client.Name}' is linked to {client.ProjectClients.Count} project(s). To preserve historical data, the client was deactivated instead of deleted.";
            return RedirectToAction(nameof(Index));
        }

        // If no linked projects, delete
        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Deleted",
            entity: "Client",
            entityId: id.ToString(),
            details: new { client.Name },
            userId: CurrentUserId
        );

        TempData["Success"] = $"Client '{client.Name}' deleted successfully.";
        return RedirectToAction(nameof(Index));
    }
}
