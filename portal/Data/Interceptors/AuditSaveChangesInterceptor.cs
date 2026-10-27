using System.Text.Json;
using AspnetCoreMvcFull.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AspnetCoreMvcFull.Data.Interceptors;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp"
    };

    public AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var context = eventData.Context;
        var currentUserId = GetCurrentUserId();
        var clientIp = GetClientIpAddress();
        var auditEntries = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            var entityName = entry.Metadata.ClrType.Name;
            var action = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted => "Deleted",
                _ => entry.State.ToString()
            };

            var changes = new Dictionary<string, object?>();

            foreach (var prop in entry.Properties)
            {
                var propName = prop.Metadata.Name;
                if (SensitiveFields.Contains(propName))
                {
                    continue;
                }

                if (entry.State == EntityState.Added)
                {
                    changes[propName] = prop.CurrentValue;
                }
                else if (entry.State == EntityState.Deleted)
                {
                    changes[propName] = prop.OriginalValue;
                }
                else if (entry.State == EntityState.Modified && prop.IsModified)
                {
                    changes[propName] = new
                    {
                        From = prop.OriginalValue,
                        To = prop.CurrentValue
                    };
                }
            }

            var primaryKey = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString();

            auditEntries.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = action,
                Entity = entityName,
                EntityId = primaryKey,
                Details = JsonSerializer.Serialize(changes),
                IpAddress = clientIp,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (auditEntries.Count > 0)
        {
            context.Set<AuditLog>().AddRange(auditEntries);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private int? GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var subClaim = httpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(subClaim, out var id) ? id : null;
    }

    private string? GetClientIpAddress()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.Connection.RemoteIpAddress?.ToString();
    }
}
