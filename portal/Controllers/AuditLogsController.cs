using System.Text;
using AspnetCoreMvcFull.Data;
using AspnetCoreMvcFull.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AspnetCoreMvcFull.Controllers;

[Authorize(Roles = "Admin")]
public class AuditLogsController : Controller
{
    private readonly ApplicationDbContext _context;

    public AuditLogsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        string? entityFilter = null,
        string? actionFilter = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(a => a.Action.ToLower().Contains(searchLower)
                                  || a.Entity.ToLower().Contains(searchLower)
                                  || (a.EntityId != null && a.EntityId.ToLower().Contains(searchLower))
                                  || (a.Details != null && a.Details.ToLower().Contains(searchLower))
                                  || (a.User != null && a.User.Email!.ToLower().Contains(searchLower)));
        }

        if (!string.IsNullOrWhiteSpace(entityFilter))
        {
            query = query.Where(a => a.Entity == entityFilter);
        }

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a => a.Action == actionFilter);
        }

        if (startDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= startDate.Value.ToUniversalTime());
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= endDate.Value.ToUniversalTime().AddDays(1));
        }

        var totalCount = await query.CountAsync();

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.EntityFilter = entityFilter;
        ViewBag.ActionFilter = actionFilter;
        ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
        ViewBag.PageNumber = pageNumber;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        ViewBag.Entities = await _context.AuditLogs.Select(a => a.Entity).Distinct().ToListAsync();
        ViewBag.Actions = await _context.AuditLogs.Select(a => a.Action).Distinct().ToListAsync();

        return View(logs);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(
        string? search = null,
        string? entityFilter = null,
        string? actionFilter = null)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(a => a.Action.ToLower().Contains(searchLower)
                                  || a.Entity.ToLower().Contains(searchLower)
                                  || (a.EntityId != null && a.EntityId.ToLower().Contains(searchLower)));
        }

        if (!string.IsNullOrWhiteSpace(entityFilter))
        {
            query = query.Where(a => a.Entity == entityFilter);
        }

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a => a.Action == actionFilter);
        }

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(5000)
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Id,Timestamp,Action,Entity,EntityId,UserEmail,IpAddress,Details");

        foreach (var log in logs)
        {
            var detailsEscaped = (log.Details ?? "").Replace("\"", "\"\"");
            csv.AppendLine($"{log.Id},{log.CreatedAt:O},{log.Action},{log.Entity},{log.EntityId},{log.User?.Email ?? "System"},{log.IpAddress},\"{detailsEscaped}\"");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"astra-audit-logs-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }
}
