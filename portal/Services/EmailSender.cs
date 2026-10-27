using AspnetCoreMvcFull.Data;
using AspnetCoreMvcFull.Entities;

namespace AspnetCoreMvcFull.Services;

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string htmlMessage, CancellationToken cancellationToken = default);
}

public class DevEmailSender : IEmailSender
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ApplicationDbContext context, ILogger<DevEmailSender> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("================ EMAIL DISPATCH (DEV OUTBOX) ================");
        _logger.LogInformation("To: {ToEmail}", toEmail);
        _logger.LogInformation("Subject: {Subject}", subject);
        _logger.LogInformation("Body: {Body}", htmlMessage);
        _logger.LogInformation("==============================================================");

        var outboxItem = new EmailOutboxItem
        {
            ToAddress = toEmail,
            Subject = subject,
            Body = htmlMessage,
            Status = OutboxStatus.Sent, // In dev, we consider it delivered to local log
            Attempts = 1,
            CreatedAt = DateTime.UtcNow,
            SentAt = DateTime.UtcNow
        };

        _context.EmailOutbox.Add(outboxItem);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
