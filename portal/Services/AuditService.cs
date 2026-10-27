using System.Text.Json;
using AspnetCoreMvcFull.Data;
using AspnetCoreMvcFull.Entities;

namespace AspnetCoreMvcFull.Services;

public interface IAuditService
{
    Task LogAsync(string action, string entity, string? entityId = null, object? details = null, int? userId = null, CancellationToken cancellationToken = default);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action,
        string entity,
        string? entityId = null,
        object? details = null,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedUserId = userId ?? GetCurrentUserId();
        var clientIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        var logEntry = new AuditLog
        {
            UserId = resolvedUserId,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Details = details is null ? null : JsonSerializer.Serialize(details),
            IpAddress = clientIp,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(logEntry);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private int? GetCurrentUserId()
    {
        var subClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(subClaim, out var id) ? id : null;
    }
}
