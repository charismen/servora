using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Activity;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Notifications;

public static class PublicQuoteApprovalEndpoints
{
    public sealed record PublicQuoteApprovalsRequest(string? PhoneLastFour);
    public sealed record SendQuoteOtpRequest(string? PhoneLastFour, string Channel);
    public sealed record RespondQuoteApprovalRequest(
        string? PhoneLastFour,
        string? ChallengeToken,
        string? OtpCode,
        string Decision,
        string? Note);

    public sealed record PublicOtpChannelOption(string Channel, string MaskedDestination);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/track/{token}/approvals", ListPublicApprovalsAsync)
            .WithName("GetPublicTrackingApprovals")
            .WithOpenApi()
            .AllowAnonymous()
            .RequireRateLimiting("PublicTrackingVerify");

        app.MapPost("/track/{token}/approvals/send-otp", SendQuoteApprovalOtpAsync)
            .WithName("SendPublicQuoteApprovalOtp")
            .WithOpenApi()
            .AllowAnonymous()
            .RequireRateLimiting("PublicTrackingVerify");

        app.MapPost("/track/{token}/approvals/{approvalId:int}/respond", RespondPublicQuoteApprovalAsync)
            .WithName("RespondPublicQuoteApproval")
            .WithOpenApi()
            .AllowAnonymous()
            .RequireRateLimiting("PublicTrackingVerify");
    }

    public static (bool RequiresOtp, IReadOnlyList<PublicOtpChannelOption> Channels) EvaluateQuoteOtpOptions(
        WorkspaceNotificationChannel? channelConfig,
        bool systemSmtpConfigured,
        string? customerEmail,
        string? customerPhone)
    {
        if (channelConfig is null || !channelConfig.RequireOtpForPublicQuoteApproval)
        {
            return (false, []);
        }

        var options = new List<PublicOtpChannelOption>();
        if (channelConfig.WhatsAppEnabled
            && channelConfig.WhatsAppProvider != WhatsAppProviderType.Manual
            && !string.IsNullOrWhiteSpace(customerPhone))
        {
            options.Add(new PublicOtpChannelOption("WhatsApp", OtpCodeHelper.MaskPhone(customerPhone)));
        }

        var emailUsable = channelConfig.EmailEnabled
            && ((!channelConfig.UseSystemSmtp && !string.IsNullOrWhiteSpace(channelConfig.SmtpHost)) || systemSmtpConfigured);
        if (emailUsable && !string.IsNullOrWhiteSpace(customerEmail))
        {
            options.Add(new PublicOtpChannelOption("Email", OtpCodeHelper.MaskEmail(customerEmail)));
        }

        // Graceful fallback to phone last-4 if no API channel is usable for this customer
        return (options.Count > 0, options);
    }

    private static async Task<IResult> ListPublicApprovalsAsync(
        string token,
        [FromBody] PublicQuoteApprovalsRequest request,
        AppDbContext context,
        NotificationDispatcherService dispatcher,
        CancellationToken cancellationToken)
    {
        var (ticket, error) = await ResolveAndVerifyPhoneAsync(token, request.PhoneLastFour, context, cancellationToken);
        if (error is not null) return error;

        var approvals = await context.ApprovalRequests.AsNoTracking()
            .Where(a => a.ShopId == ticket!.ShopId && a.ServiceTicketId == ticket.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Description,
                a.EstimatedAmount,
                Status = a.Status.ToString(),
                a.ResolutionNote,
                a.CreatedAt,
                a.ResolvedAt
            })
            .ToListAsync(cancellationToken);

        var workItems = await context.ServiceTicketWorkItems.AsNoTracking()
            .Where(w => w.ShopId == ticket!.ShopId && w.ServiceTicketId == ticket.Id && w.ApprovalRequestId != null)
            .Select(w => new
            {
                w.Id,
                w.ApprovalRequestId,
                w.Title,
                w.Quantity,
                w.UnitPrice,
                LineTotal = TicketBillingCalculator.ComputeLineTotal(w.Quantity, w.UnitPrice),
                Status = w.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        var channelConfig = await context.WorkspaceNotificationChannels.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShopId == ticket!.ShopId, cancellationToken);
        var (requiresOtp, otpChannels) = EvaluateQuoteOtpOptions(
            channelConfig,
            dispatcher.IsSystemSmtpConfigured(),
            ticket!.Customer.Email,
            !string.IsNullOrWhiteSpace(ticket.CustomerPhoneSnapshot) ? ticket.CustomerPhoneSnapshot : ticket.Customer.PhoneNumber);

        return Results.Ok(new
        {
            currencyCode = ticket.Shop.CurrencyCode,
            requiresOtp,
            otpChannels,
            approvals = approvals.Select(a => new
            {
                a.Id,
                a.Title,
                a.Description,
                a.EstimatedAmount,
                a.Status,
                a.ResolutionNote,
                a.CreatedAt,
                a.ResolvedAt,
                proposedWorkItems = workItems.Where(w => w.ApprovalRequestId == a.Id).ToList()
            }).ToList()
        });
    }

    private static async Task<IResult> SendQuoteApprovalOtpAsync(
        string token,
        [FromBody] SendQuoteOtpRequest request,
        AppDbContext context,
        OtpChallengeService otpService,
        NotificationDispatcherService dispatcher,
        CancellationToken cancellationToken)
    {
        var (ticket, error) = await ResolveAndVerifyPhoneAsync(token, request.PhoneLastFour, context, cancellationToken);
        if (error is not null) return error;

        var (onCooldown, retryAfter) = await otpService.CheckCooldownAsync(OtpPurpose.PublicQuoteApproval, null, ticket!.Id, cancellationToken);
        if (onCooldown)
        {
            return Results.Problem(
                detail: $"Please wait {retryAfter} seconds before requesting another verification code.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var channelConfig = await context.WorkspaceNotificationChannels.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShopId == ticket.ShopId, cancellationToken);
        var (requiresOtp, otpChannels) = EvaluateQuoteOtpOptions(
            channelConfig,
            dispatcher.IsSystemSmtpConfigured(),
            ticket.Customer.Email,
            !string.IsNullOrWhiteSpace(ticket.CustomerPhoneSnapshot) ? ticket.CustomerPhoneSnapshot : ticket.Customer.PhoneNumber);

        if (!requiresOtp)
        {
            return Results.BadRequest("OTP verification is not enabled for this workspace.");
        }

        var chosen = otpChannels.FirstOrDefault(c => string.Equals(c.Channel, request.Channel, StringComparison.OrdinalIgnoreCase))
            ?? otpChannels[0];

        var parsedChannel = chosen.Channel == "WhatsApp" ? NotificationChannel.WhatsApp : NotificationChannel.Email;
        var (challenge, plainCode) = await otpService.CreateChallengeAsync(
            ticket.ShopId,
            null,
            ticket.Id,
            OtpPurpose.PublicQuoteApproval,
            parsedChannel,
            chosen.MaskedDestination,
            TimeSpan.FromMinutes(10),
            cancellationToken: cancellationToken);

        ChannelSendResult sendResult;
        if (parsedChannel == NotificationChannel.WhatsApp)
        {
            var phone = !string.IsNullOrWhiteSpace(ticket.CustomerPhoneSnapshot) ? ticket.CustomerPhoneSnapshot : ticket.Customer.PhoneNumber;
            sendResult = await dispatcher.SendDirectWhatsAppAsync(
                ticket.ShopId,
                phone,
                $"[{ticket.Shop.Name}] Your quote approval verification code for {ticket.TicketNumber} is: {plainCode}. Valid for 10 minutes.",
                "quote.approval_otp",
                "ServiceTicket",
                ticket.Id,
                cancellationToken);
        }
        else
        {
            sendResult = await dispatcher.SendDirectEmailAsync(
                ticket.ShopId,
                ticket.Customer.Email!,
                $"[{ticket.Shop.Name}] Quote Approval Verification Code ({ticket.TicketNumber})",
                $"Your verification code to approve or decline the estimate for ticket {ticket.TicketNumber} is:\n\n{plainCode}\n\nThis code expires in 10 minutes.",
                null,
                "quote.approval_otp",
                "ServiceTicket",
                ticket.Id,
                cancellationToken);
        }

        if (!sendResult.Succeeded)
        {
            return Results.Problem(detail: sendResult.ErrorMessage ?? "Could not deliver verification code.", statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.Ok(new
        {
            challengeToken = challenge.ChallengeToken,
            channel = chosen.Channel,
            maskedDestination = chosen.MaskedDestination,
            expiresAt = challenge.ExpiresAt
        });
    }

    private static async Task<IResult> RespondPublicQuoteApprovalAsync(
        string token,
        int approvalId,
        [FromBody] RespondQuoteApprovalRequest request,
        AppDbContext context,
        IConfiguration configuration,
        OtpChallengeService otpService,
        NotificationDispatcherService dispatcher,
        CancellationToken cancellationToken)
    {
        var (ticket, error) = await ResolveAndVerifyPhoneAsync(token, request.PhoneLastFour, context, cancellationToken, tracking: true);
        if (error is not null) return error;

        var decision = request.Decision?.Trim();
        var isApprove = string.Equals(decision, "Approve", StringComparison.OrdinalIgnoreCase);
        var isReject = string.Equals(decision, "Reject", StringComparison.OrdinalIgnoreCase);
        if (!isApprove && !isReject)
        {
            return Results.BadRequest("Decision must be 'Approve' or 'Reject'.");
        }

        var channelConfig = await context.WorkspaceNotificationChannels.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShopId == ticket!.ShopId, cancellationToken);
        var (requiresOtp, _) = EvaluateQuoteOtpOptions(
            channelConfig,
            dispatcher.IsSystemSmtpConfigured(),
            ticket!.Customer.Email,
            !string.IsNullOrWhiteSpace(ticket.CustomerPhoneSnapshot) ? ticket.CustomerPhoneSnapshot : ticket.Customer.PhoneNumber);

        if (requiresOtp)
        {
            if (string.IsNullOrWhiteSpace(request.ChallengeToken) || string.IsNullOrWhiteSpace(request.OtpCode))
            {
                return Results.BadRequest("A 6-digit verification code is required before responding to this quote.");
            }

            var verify = await otpService.VerifyChallengeAsync(
                request.ChallengeToken.Trim(),
                request.OtpCode.Trim(),
                OtpPurpose.PublicQuoteApproval,
                verifyCodeHash: true,
                cancellationToken: cancellationToken);

            if (!verify.Succeeded || verify.Challenge?.ServiceTicketId != ticket.Id)
            {
                return Results.BadRequest(verify.ErrorMessage ?? "Invalid verification code.");
            }
        }

        var approval = await context.ApprovalRequests
            .FirstOrDefaultAsync(a => a.Id == approvalId && a.ShopId == ticket.ShopId && a.ServiceTicketId == ticket.Id, cancellationToken);
        if (approval is null) return Results.NotFound("Approval request not found.");
        if (approval.Status != ApprovalStatus.Pending)
        {
            return Results.BadRequest($"This approval request has already been {approval.Status.ToString().ToLowerInvariant()}.");
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        approval.Status = isApprove ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        approval.ResolutionNote = note is null
            ? $"Resolved online by customer via public tracking."
            : $"Resolved online by customer: {(note.Length > 420 ? note[..420] : note)}";
        approval.ResolvedAt = DateTimeOffset.UtcNow;

        var linkedItems = await context.ServiceTicketWorkItems
            .Where(w => w.ShopId == ticket.ShopId && w.ServiceTicketId == ticket.Id && w.ApprovalRequestId == approval.Id && w.Status == WorkItemStatus.PendingApproval)
            .ToListAsync(cancellationToken);

        foreach (var item in linkedItems)
        {
            item.Status = isApprove ? WorkItemStatus.Approved : WorkItemStatus.Rejected;
        }

        await context.SaveChangesAsync(cancellationToken);

        await TicketBillingService.RecalculateTicketBillingAsync(
            context,
            ticket.ShopId,
            ticket,
            actorUserId: null,
            reasonNote: $"Customer {(isApprove ? "approved" : "declined")} quote online",
            cancellationToken: cancellationToken);

        context.ServiceTicketHistories.Add(new ServiceTicketHistory
        {
            ShopId = ticket.ShopId,
            ServiceTicketId = ticket.Id,
            ActorUserId = null,
            EventType = TicketHistoryEventType.ApprovalResolved,
            Note = $"Customer {(isApprove ? "approved" : "declined")} quote '{approval.Title}' online." + (note is null ? string.Empty : $" Note: {note}"),
            IsPublicVisible = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.ActivityLogs.Add(new ActivityLog
        {
            ShopId = ticket.ShopId,
            ActorUserId = null,
            EntityType = "ServiceTicket",
            EntityId = ticket.Id,
            ActionType = isApprove ? "CustomerQuoteApproved" : "CustomerQuoteRejected",
            Title = $"{ticket.TicketNumber}: Customer {(isApprove ? "approved" : "declined")} quote",
            Description = approval.Title
        });

        await context.SaveChangesAsync(cancellationToken);

        var frontendBase = (configuration["PublicFrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        await dispatcher.PublishEventAsync(new NotificationEventEnvelope(
            ShopId: ticket.ShopId,
            EventType: NotificationEventType.ApprovalResolved,
            Title: $"Quote {(isApprove ? "Approved" : "Declined")}: {ticket.TicketNumber}",
            Message: $"{ticket.CustomerNameSnapshot} {(isApprove ? "approved" : "declined")} quote '{approval.Title}'" + (note is null ? "." : $" — \"{note}\""),
            DeepLinkUrl: $"{frontendBase}/app/tickets/{ticket.Id}",
            CustomerName: ticket.CustomerNameSnapshot,
            CustomerEmail: ticket.Customer.Email,
            CustomerPhone: ticket.CustomerPhoneSnapshot,
            ReferenceEntityType: "ServiceTicket",
            ReferenceEntityId: ticket.Id,
            Data: new { ticketId = ticket.Id, ticketNumber = ticket.TicketNumber, approvalId = approval.Id, decision = approval.Status.ToString(), note }),
            cancellationToken);

        return Results.Ok(new
        {
            approval.Id,
            status = approval.Status.ToString(),
            approval.ResolutionNote,
            approval.ResolvedAt
        });
    }

    private static async Task<(ServiceTicket? Ticket, IResult? Error)> ResolveAndVerifyPhoneAsync(
        string token,
        string? phoneLastFour,
        AppDbContext context,
        CancellationToken cancellationToken,
        bool tracking = false)
    {
        var inputDigits = new string((phoneLastFour ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (inputDigits.Length != 4)
        {
            return (null, Results.BadRequest("Enter the last 4 digits of the registered phone number."));
        }

        IQueryable<ServiceTicket> query = context.ServiceTickets
            .Include(x => x.Shop)
            .Include(x => x.Customer);
        if (!tracking) query = query.AsNoTracking();

        var ticket = await query.FirstOrDefaultAsync(
            x => x.PublicTrackingEnabled && x.PublicTrackingToken == token && x.DeletedAt == null,
            cancellationToken);

        if (ticket is null)
        {
            return (null, Results.NotFound());
        }

        var phoneRaw = !string.IsNullOrWhiteSpace(ticket.CustomerPhoneSnapshot)
            ? ticket.CustomerPhoneSnapshot
            : ticket.Customer.PhoneNumber;
        var phoneDigits = new string((phoneRaw ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (phoneDigits.Length < 4)
        {
            return (null, Results.BadRequest("Phone verification failed."));
        }

        var expectedBytes = Encoding.UTF8.GetBytes(phoneDigits[^4..]);
        var actualBytes = Encoding.UTF8.GetBytes(inputDigits);
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            return (null, Results.BadRequest("Phone number digits did not match."));
        }

        return (ticket, null);
    }
}
