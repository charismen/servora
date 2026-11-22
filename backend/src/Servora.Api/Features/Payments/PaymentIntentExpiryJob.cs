using Microsoft.EntityFrameworkCore;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Payments;

public sealed class PaymentIntentExpiryJob
{
    private readonly AppDbContext _context;
    private readonly ILogger<PaymentIntentExpiryJob> _logger;

    public PaymentIntentExpiryJob(AppDbContext context, ILogger<PaymentIntentExpiryJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> ExpirePastDueIntentsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredIntents = await _context.PaymentCheckoutIntents
            .Where(x => x.Status == PaymentCheckoutStatus.Pending && x.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expiredIntents.Count == 0)
        {
            return 0;
        }

        foreach (var intent in expiredIntents)
        {
            intent.Status = PaymentCheckoutStatus.Expired;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Expired {Count} past-due payment checkout intent(s).", expiredIntents.Count);
        return expiredIntents.Count;
    }
}
