using AspnetCoreMvcFull.Data;
using Microsoft.EntityFrameworkCore;

namespace AspnetCoreMvcFull.Services;

public interface IProjectCodeGenerator
{
    Task<string> GenerateNextCodeAsync(CancellationToken cancellationToken = default);
}

public class ProjectCodeGenerator : IProjectCodeGenerator
{
    private readonly ApplicationDbContext _context;

    public ProjectCodeGenerator(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextCodeAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PRJ-{year}-";

        var existingCodes = await _context.Projects
            .Where(p => p.Code.StartsWith(prefix))
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);

        var maxNumber = 0;
        foreach (var code in existingCodes)
        {
            if (code.Length > prefix.Length &&
                int.TryParse(code[prefix.Length..], out var number) &&
                number > maxNumber)
            {
                maxNumber = number;
            }
        }

        var nextNumber = maxNumber + 1;
        return $"{prefix}{nextNumber:D3}";
    }
}
