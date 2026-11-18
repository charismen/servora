using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Blueprints;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Inventory;
using Servora.Api.Features.Settings;
using Servora.Api.Features.ServiceTemplates;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.ServiceTickets;

public static class ServiceTicketEndpoints
{
    private const string EntityType = "ServiceTicket";
    private static readonly HashSet<string> AllowedAttachmentMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
    };
    private static readonly Dictionary<string, string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    public sealed record TicketSummaryDto(
        int Id,
        string TicketNumber,
        string CustomerName,
        string CustomerPhone,
        string Device,
        string Status,
        string StatusColor,
        string Priority,
        string PaymentStatus,
        string? AssignedTechnician,
        DateTimeOffset? WarrantyUntil,
        bool WarrantyActive,
        DateTimeOffset CreatedAt,
        bool IsInternalWorkOrder = false,
        string ServiceLocationType = "InShop",
        string? SiteAddressSnapshot = null,
        DateTimeOffset? ScheduledVisitStart = null,
        DateTimeOffset? SlaDueAt = null);
    public sealed record TicketPageDto(IReadOnlyCollection<TicketSummaryDto> Items, int TotalCount, int Page, int PageSize);

    public sealed record KanbanColumnDto(int StatusId, string Name, string Color, int SortOrder, IReadOnlyCollection<KanbanTicketDto> Tickets, bool IsFinal = false, bool IsCancelled = false);
    public sealed record KanbanTicketDto(
        int Id,
        string TicketNumber,
        string CustomerName,
        string Device,
        string Complaint,
        string Priority,
        string PaymentStatus,
        string? AssignedTechnician,
        DateTimeOffset CreatedAt,
        bool WaitingApproval,
        bool Unpaid,
        bool Blocked,
        bool Overdue);

    public sealed record DashboardSummaryDto(
        int OpenTickets,
        int WaitingApproval,
        int UnpaidTickets,
        int BlockedTickets,
        int OverdueTickets,
        int CompletedToday);

    public sealed record TicketDetailDto(
        int Id,
        string TicketNumber,
        int CustomerId,
        int DeviceId,
        int StatusId,
        string Status,
        string StatusColor,
        string CustomerName,
        string CustomerPhone,
        string Device,
        string Priority,
        string Complaint,
        string? InitialDiagnosis,
        string? PhysicalConditionNotes,
        string? AccessorySummary,
        string? PhotoNotes,
        string? SecurityPasscode,
        string? SecurityPattern,
        string? PreRepairChecklist,
        string? Diagnosis,
        string? RepairNotes,
        DateTimeOffset? WarrantyUntil,
        string? WarrantyTerms,
        bool WarrantyActive,
        bool ApprovalRequired,
        bool IsPreApproved,
        string PaymentStatus,
        string? AssignedTechnician,
        int? AssignedTechnicianUserId,
        IReadOnlyCollection<AccessoryDto> Accessories,
        IReadOnlyCollection<AttachmentDto> Attachments,
        IReadOnlyCollection<TransitionDto> AllowedTransitions,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        bool HasPendingApproval = false,
        decimal WorkItemsSubtotal = 0m,
        decimal ConsumedPartsSubtotal = 0m,
        decimal DiscountAmount = 0m,
        decimal? TotalAmount = null,
        decimal AmountPaid = 0m,
        decimal RemainingBalance = 0m,
        decimal OverpaidAmount = 0m,
        bool IsInternalWorkOrder = false,
        string? DepartmentCode = null,
        string? CostCenter = null,
        DateTimeOffset? SlaDueAt = null,
        string ServiceLocationType = "InShop",
        int? SiteLocationId = null,
        string? SiteAddressSnapshot = null,
        DateTimeOffset? ScheduledVisitStart = null,
        DateTimeOffset? ScheduledVisitEnd = null,
        int? ParentTicketId = null,
        IReadOnlyCollection<LinkedAssetDto>? LinkedAssets = null,
        IReadOnlyCollection<BlueprintEndpoints.FieldValueDto>? CustomFields = null,
        IReadOnlyCollection<BlueprintEndpoints.FieldValueDto>? AssetCustomFields = null,
        IReadOnlyCollection<BlueprintEndpoints.TicketInspectionDto>? Inspections = null);

    public sealed record LinkedAssetDto(int ServiceAssetId, string Brand, string Model, string? PrimaryIdentifier, string? Notes);

    public sealed record TransitionDto(int StatusId, string Name, string Color, bool RequiresReason, bool RequiresApproval, bool RequiresPaymentCompleted);
    public sealed record AccessoryDto(int Id, string Name, decimal Quantity, string? Notes);
    public sealed record AttachmentDto(int Id, string FileName, string MimeType, long FileSize, string AttachmentType, bool IsPublicVisible, DateTimeOffset CreatedAt);
    public sealed record HistoryDto(int Id, string EventType, string? FromStatus, string? ToStatus, string? Reason, string? Note, bool IsPublicVisible, DateTimeOffset CreatedAt);
    public sealed record ApprovalDto(int Id, string Title, string Description, decimal? EstimatedAmount, string Status, string? ResolutionNote, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt);
    public sealed record WorkItemDto(
        int Id,
        int? ApprovalRequestId,
        string ItemType,
        string Title,
        string? Description,
        decimal Quantity,
        decimal UnitPrice,
        decimal LineTotal,
        string Status,
        bool IsCompleted,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);
    public sealed record PaymentTransactionDto(
        int Id,
        string EntryType,
        string Status,
        decimal TransactionAmount,
        decimal AmountPaid,
        decimal? TotalAmount,
        string? Method,
        string? ReferenceNumber,
        string? Notes,
        string? UpdatedByName,
        DateTimeOffset RecordedAt,
        DateTimeOffset CreatedAt,
        bool IsVoided,
        DateTimeOffset? VoidedAt,
        string? VoidedByName,
        string? VoidReason);
    public sealed record PaymentDto(
        string Status,
        decimal AmountPaid,
        decimal? TotalAmount,
        string? Method,
        string? ReferenceNumber,
        string? Notes,
        DateTimeOffset? LastUpdatedAt,
        decimal CalculatedTotalAmount = 0m,
        decimal DiscountAmount = 0m,
        bool IsTotalManuallyOverridden = false,
        decimal RemainingBalance = 0m,
        decimal OverpaidAmount = 0m,
        decimal WorkItemsSubtotal = 0m,
        decimal ConsumedPartsSubtotal = 0m,
        IReadOnlyCollection<PaymentTransactionDto>? Transactions = null);
    public sealed record ChecklistItemDto(int Id, string Title, string? Description, int SortOrder, bool IsCompleted, DateTimeOffset? CompletedAt);
    public sealed record ServiceHistoryTicketDto(int Id, string TicketNumber, string Device, string Status, string StatusColor, string Priority, string PaymentStatus, DateTimeOffset CreatedAt, DateTimeOffset? WarrantyUntil, bool WarrantyActive);
    public sealed record RepeatCustomerSummaryDto(int CustomerId, int TotalTickets, int ActiveTickets, int CompletedTickets, DateTimeOffset? LastTicketAt);
    public sealed record WarrantyDto(int TicketId, string TicketNumber, bool WarrantyActive, DateTimeOffset? WarrantyUntil, string? WarrantyTerms);
    public sealed record ReceiptWorkItemDto(string ItemType, string Title, string? Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);
    public sealed record ReceiptPartDto(string Sku, string ItemName, string Unit, decimal NetQuantity, decimal UnitPrice, decimal LineTotal);
    public sealed record TicketReceiptDto(
        int Id,
        string TicketNumber,
        string CustomerName,
        string CustomerPhone,
        string Device,
        string Complaint,
        string? InitialDiagnosis,
        string? PhysicalConditionNotes,
        string? AccessorySummary,
        string? PhotoNotes,
        string? SecurityPasscode,
        string? SecurityPattern,
        string Priority,
        string Status,
        DateTimeOffset CreatedAt,
        string PublicTrackingToken,
        string ShopName,
        string? LogoUrl,
        string PhoneNumber,
        string WhatsAppNumber,
        string Address,
        string ReceiptFooter,
        string TermsAndConditions,
        bool ShowPhysicalCondition,
        bool ShowAccessories,
        bool ShowPhotoNotes,
        bool ShowQrCode,
        bool ShowPhoneNumber,
        bool ShowPaymentSummary = true,
        string PaymentStatus = "Unpaid",
        string CurrencyCode = "IDR",
        decimal WorkItemsSubtotal = 0m,
        decimal ConsumedPartsSubtotal = 0m,
        decimal DiscountAmount = 0m,
        decimal? TotalAmount = null,
        decimal AmountPaid = 0m,
        decimal RemainingBalance = 0m,
        decimal OverpaidAmount = 0m,
        IReadOnlyCollection<ReceiptWorkItemDto>? WorkItems = null,
        IReadOnlyCollection<ReceiptPartDto>? ConsumedParts = null,
        string ServiceLocationType = "InShop",
        string? SiteAddressSnapshot = null,
        DateTimeOffset? ScheduledVisitStart = null,
        bool IsInternalWorkOrder = false,
        IReadOnlyCollection<BlueprintEndpoints.FieldValueDto>? PrintCustomFields = null,
        TerminologyConfigDto? Terminology = null);

    public sealed record CreateTicketRequest(
        int CustomerId,
        int? DeviceId,
        NewTicketDeviceRequest? NewDevice,
        string Complaint,
        string? InitialDiagnosis,
        string? PhysicalConditionNotes,
        string? AccessorySummary,
        string? PhotoNotes,
        string? SecurityPasscode,
        string? SecurityPattern,
        string? PreRepairChecklist,
        string? Priority,
        int? AssignedTechnicianUserId,
        bool ApprovalRequired,
        bool IsPreApproved,
        DateTimeOffset? CreatedAt,
        string? IdempotencyKey = null,
        string? BannedCustomerOverrideReason = null,
        int IntakePhotoCount = 0,
        bool? IsInternalWorkOrder = null,
        string? DepartmentCode = null,
        string? CostCenter = null,
        DateTimeOffset? SlaDueAt = null,
        ServiceLocationType ServiceLocationType = ServiceLocationType.InShop,
        int? SiteLocationId = null,
        string? SiteAddressSnapshot = null,
        DateTimeOffset? ScheduledVisitStart = null,
        DateTimeOffset? ScheduledVisitEnd = null,
        int? ParentTicketId = null,
        IReadOnlyCollection<int>? AdditionalAssetIds = null,
        IReadOnlyCollection<BlueprintEndpoints.FieldValueInput>? CustomFields = null,
        IReadOnlyCollection<BlueprintEndpoints.TicketInspectionInput>? Inspections = null);
    public sealed record NewTicketDeviceRequest(
        string DeviceType,
        string Brand,
        string Model,
        string? SerialNumber,
        string? IdentifierLabel,
        string? Notes,
        decimal? MeterReading = null,
        string? MeterUnit = null,
        int? SiteLocationId = null,
        IReadOnlyCollection<BlueprintEndpoints.FieldValueInput>? CustomFields = null);

    public sealed record UpdateTicketRequest(
        string Complaint,
        string? InitialDiagnosis,
        string? PhysicalConditionNotes,
        string? AccessorySummary,
        string? PhotoNotes,
        string? SecurityPasscode,
        string? SecurityPattern,
        string? PreRepairChecklist,
        string? Diagnosis,
        string? RepairNotes,
        DateTimeOffset? WarrantyUntil,
        string? WarrantyTerms,
        string? Priority,
        DateTimeOffset? CreatedAt,
        DateTimeOffset? UpdatedAt,
        bool? IsInternalWorkOrder = null,
        string? DepartmentCode = null,
        string? CostCenter = null,
        DateTimeOffset? SlaDueAt = null,
        ServiceLocationType? ServiceLocationType = null,
        int? SiteLocationId = null,
        string? SiteAddressSnapshot = null,
        DateTimeOffset? ScheduledVisitStart = null,
        DateTimeOffset? ScheduledVisitEnd = null);
    public sealed record UpsertAccessoryRequest(string Name, decimal Quantity, string? Notes);
    public sealed record ChangeStatusRequest(int StatusId, string? Reason, bool OverridePaymentBlock);
    public sealed record AssignTicketRequest(int? TechnicianUserId);
    public sealed record AddNoteRequest(string Note, bool IsPublicVisible);
    public sealed record ProposedWorkItemRequest(string? ItemType, string Title, string? Description, decimal Quantity, decimal UnitPrice);
    public sealed record UpsertWorkItemRequest(string? ItemType, string Title, string? Description, decimal Quantity, decimal UnitPrice, bool IsCompleted = false, int? ApprovalRequestId = null);
    public sealed record CreateApprovalRequest(string Title, string Description, decimal? EstimatedAmount, IReadOnlyCollection<ProposedWorkItemRequest>? ProposedWorkItems = null);
    public sealed record ResolveApprovalRequest(string? Note);
    public sealed record UpdatePaymentRequest(string Status, decimal AmountPaid, decimal? TotalAmount, string? Method, string? ReferenceNumber, string? Notes);
    public sealed record RecordPaymentTransactionRequest(string? EntryType, decimal Amount, string? Method, string? ReferenceNumber, string? Notes, DateTimeOffset? RecordedAt = null);
    public sealed record VoidPaymentTransactionRequest(string Reason);
    public sealed record UpdateBillingRequest(decimal DiscountAmount, bool IsWaived, decimal? ManualTotalAmount, bool ClearManualOverride = false, string? Notes = null);
    public sealed record PickupOverrideRequest(string Reason);
    public sealed record ApplyTemplateRequest(int TemplateId);
    public sealed record UpdateChecklistItemRequest(bool IsCompleted);
    public sealed record UpdateAttachmentVisibilityRequest(bool IsPublicVisible);
    public sealed record VerifyPublicPhotosRequest(string PhoneLastFour);
    public sealed record TicketCreatedDto(int Id, string TicketNumber, string PublicTrackingToken);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/service-tickets", ListTicketsAsync)
            .WithName("GetServiceTickets")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapGet("/service-tickets/kanban", GetKanbanAsync)
            .WithName("GetServiceTicketKanban")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapGet("/dashboard/summary", GetDashboardSummaryAsync)
            .WithName("GetDashboardSummary")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPost("/service-tickets", CreateTicketAsync)
            .WithName("CreateServiceTicket")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapGet("/service-tickets/by-number/{ticketNumber}", GetTicketByNumberAsync)
            .WithName("GetServiceTicketByNumber")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapGet("/service-tickets/{id:int}", GetTicketAsync)
            .WithName("GetServiceTicket")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapGet("/service-tickets/{id:int}/receipt", GetReceiptAsync)
            .WithName("GetServiceTicketReceipt")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPost("/service-tickets/{id:int}/receipt/print-log", LogReceiptPrintAsync)
            .WithName("LogServiceTicketReceiptPrint")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapGet("/service-tickets/{id:int}/warranty", GetWarrantyAsync)
            .WithName("GetServiceTicketWarranty")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapGet("/customers/{customerId:int}/service-history", GetCustomerServiceHistoryAsync)
            .WithName("GetCustomerServiceHistory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapGet("/customers/{customerId:int}/repeat-summary", GetRepeatCustomerSummaryAsync)
            .WithName("GetRepeatCustomerSummary")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapGet("/customers/{customerId:int}/devices/{deviceId:int}/service-history", GetDeviceServiceHistoryAsync)
            .WithName("GetDeviceServiceHistory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPut("/service-tickets/{id:int}", UpdateTicketAsync)
            .WithName("UpdateServiceTicket")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPost("/service-tickets/{id:int}/change-status", ChangeStatusAsync)
            .WithName("ChangeServiceTicketStatus")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsMove);

        app.MapPost("/service-tickets/{id:int}/move", ChangeStatusAsync)
            .WithName("MoveServiceTicket")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsMove);

        app.MapPost("/service-tickets/{id:int}/assign", AssignTicketAsync)
            .WithName("AssignServiceTicket")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPost("/service-tickets/{id:int}/notes", AddNoteAsync)
            .WithName("AddServiceTicketNote")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapGet("/service-tickets/{id:int}/history", GetHistoryAsync)
            .WithName("GetServiceTicketHistory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPost("/service-tickets/{id:int}/accessories", AddAccessoryAsync)
            .WithName("AddServiceTicketAccessory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapDelete("/service-tickets/{id:int}/accessories/{accessoryId:int}", DeleteAccessoryAsync)
            .WithName("DeleteServiceTicketAccessory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPost("/service-tickets/{id:int}/attachments", UploadAttachmentAsync)
            .WithName("UploadServiceTicketAttachment")
            .WithOpenApi()
            .DisableAntiforgery()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapGet("/service-tickets/{id:int}/attachments/{attachmentId:int}", DownloadAttachmentAsync)
            .WithName("DownloadServiceTicketAttachment")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapDelete("/service-tickets/{id:int}/attachments/{attachmentId:int}", DeleteAttachmentAsync)
            .WithName("DeleteServiceTicketAttachment")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPut("/service-tickets/{id:int}/attachments/{attachmentId:int}/visibility", UpdateAttachmentVisibilityAsync)
            .WithName("UpdateServiceTicketAttachmentVisibility")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapGet("/service-tickets/{id:int}/approvals", GetApprovalsAsync)
            .WithName("GetServiceTicketApprovals")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPost("/service-tickets/{id:int}/approvals", CreateApprovalAsync)
            .WithName("CreateServiceTicketApproval")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPost("/service-tickets/{id:int}/approvals/{approvalId:int}/approve", ApproveApprovalAsync)
            .WithName("ApproveServiceTicketApproval")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPost("/service-tickets/{id:int}/approvals/{approvalId:int}/reject", RejectApprovalAsync)
            .WithName("RejectServiceTicketApproval")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapGet("/service-tickets/{id:int}/work-items", GetWorkItemsAsync)
            .WithName("GetServiceTicketWorkItems")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPost("/service-tickets/{id:int}/work-items", CreateWorkItemAsync)
            .WithName("CreateServiceTicketWorkItem")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPut("/service-tickets/{id:int}/work-items/{workItemId:int}", UpdateWorkItemAsync)
            .WithName("UpdateServiceTicketWorkItem")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapDelete("/service-tickets/{id:int}/work-items/{workItemId:int}", DeleteWorkItemAsync)
            .WithName("DeleteServiceTicketWorkItem")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapGet("/service-tickets/{id:int}/payment", GetPaymentAsync)
            .WithName("GetServiceTicketPayment")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPut("/service-tickets/{id:int}/payment", UpdatePaymentAsync)
            .WithName("UpdateServiceTicketPayment")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPost("/service-tickets/{id:int}/payment/transactions", RecordPaymentTransactionAsync)
            .WithName("RecordServiceTicketPaymentTransaction")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPost("/service-tickets/{id:int}/payment/transactions/{transactionId:int}/void", VoidPaymentTransactionAsync)
            .WithName("VoidServiceTicketPaymentTransaction")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPut("/service-tickets/{id:int}/billing", UpdateBillingAsync)
            .WithName("UpdateServiceTicketBilling")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPost("/service-tickets/{id:int}/pickup-override", PickupOverrideAsync)
            .WithName("OverrideServiceTicketPickup")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapGet("/service-tickets/{id:int}/checklist", GetChecklistAsync)
            .WithName("GetServiceTicketChecklist")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPost("/service-tickets/{id:int}/apply-template", ApplyTemplateAsync)
            .WithName("ApplyServiceTicketTemplate")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPut("/service-tickets/{id:int}/checklist/{checklistItemId:int}", UpdateChecklistItemAsync)
            .WithName("UpdateServiceTicketChecklistItem")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapDelete("/service-tickets/{id:int}", DeleteTicketAsync)
            .WithName("DeleteServiceTicket")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsDelete);

        app.MapGet("/track/{token}", GetPublicTrackingAsync)
            .WithName("GetPublicServiceTicketTracking")
            .WithOpenApi()
            .AllowAnonymous();

        app.MapPost("/track/{token}/verify-photos", VerifyPublicTrackingPhotosAsync)
            .WithName("VerifyPublicServiceTicketPhotos")
            .WithOpenApi()
            .AllowAnonymous()
            .RequireRateLimiting("PublicTrackingVerify");
    }

    private static async Task<IResult> ListTicketsAsync(
        AppDbContext context,
        IShopContext shopContext,
        ClaimsPrincipal principal,
        int? page,
        int? pageSize,
        string? search,
        string? status,
        string? priority,
        string? sort,
        int? technicianUserId,
        bool? myTickets,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var query = context.ServiceTickets
            .AsNoTracking()
            .Where(ticket => ticket.ShopId == shopId && ticket.DeletedAt == null);

        if (myTickets == true)
        {
            var currentUserId = GetUserId(principal);
            query = query.Where(ticket => ticket.AssignedTechnicianUserId == currentUserId);
        }
        else if (technicianUserId.HasValue)
        {
            query = query.Where(ticket => ticket.AssignedTechnicianUserId == technicianUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(ticket =>
                ticket.TicketNumber.Contains(term) ||
                ticket.CustomerNameSnapshot.Contains(term) ||
                ticket.CustomerPhoneSnapshot.Contains(term) ||
                ticket.DeviceSnapshot.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusName = status.Trim();
            query = query.Where(ticket => ticket.Status.Name == statusName);
        }

        if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<ServiceTicketPriority>(priority, true, out var parsedPriority))
        {
            query = query.Where(ticket => ticket.Priority == parsedPriority);
        }

        IQueryable<ServiceTicket> orderedQuery = sort?.Trim().ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(ticket => ticket.CreatedAt).ThenBy(ticket => ticket.Id),
            "ticket-asc" => query.OrderBy(ticket => ticket.TicketNumber),
            "priority-desc" => query
                .OrderByDescending(ticket => ticket.Priority == ServiceTicketPriority.Urgent)
                .ThenByDescending(ticket => ticket.Priority == ServiceTicketPriority.High)
                .ThenBy(ticket => ticket.CreatedAt),
            _ => query.OrderByDescending(ticket => ticket.CreatedAt)
        };

        var shouldPage = page.HasValue || pageSize.HasValue || !string.IsNullOrWhiteSpace(search) || !string.IsNullOrWhiteSpace(status) || !string.IsNullOrWhiteSpace(priority) || !string.IsNullOrWhiteSpace(sort) || myTickets == true || technicianUserId.HasValue;
        var currentPage = Math.Max(1, page ?? 1);
        var currentPageSize = Math.Clamp(pageSize ?? 20, 1, 100);
        var totalCount = shouldPage ? await query.CountAsync(cancellationToken) : 0;

        if (shouldPage)
        {
            orderedQuery = orderedQuery.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize);
        }

        var tickets = await orderedQuery
            .Select(ticket => new TicketSummaryDto(
                ticket.Id,
                ticket.TicketNumber,
                ticket.CustomerNameSnapshot,
                ticket.CustomerPhoneSnapshot,
                ticket.DeviceSnapshot,
                ticket.Status.Name,
                ticket.Status.Color,
                ticket.Priority.ToString(),
                ticket.PaymentStatus.ToString(),
                ticket.AssignedTechnicianUser == null ? null : (ticket.AssignedTechnicianUser.FirstName + " " + ticket.AssignedTechnicianUser.LastName).Trim(),
                ticket.WarrantyUntil,
                ticket.WarrantyUntil.HasValue && ticket.WarrantyUntil >= now,
                ticket.CreatedAt,
                ticket.IsInternalWorkOrder,
                ticket.ServiceLocationType.ToString(),
                ticket.SiteAddressSnapshot,
                ticket.ScheduledVisitStart,
                ticket.SlaDueAt))
            .ToListAsync(cancellationToken);

        if (shouldPage) return Results.Ok(new TicketPageDto(tickets, totalCount, currentPage, currentPageSize));
        return Results.Ok(tickets);
    }

    private static async Task<IResult> CreateTicketAsync(
        [FromBody] CreateTicketRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Complaint)) return Results.BadRequest("Complaint is required.");
        if (!TryParsePriority(request.Priority, out var priority)) return Results.BadRequest("Invalid priority.");

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (await IsSettingEnabledAsync(context, shopId, ShopProfileSettingKeys.RequirePhotoEvidenceOnIntake, cancellationToken) && request.IntakePhotoCount <= 0)
        {
            return Results.BadRequest("Photo evidence is required during ticket intake.");
        }

        var idempotencyKey = TrimOrNull(request.IdempotencyKey);
        if (idempotencyKey is { Length: > 120 }) return Results.BadRequest("Idempotency key is too long.");
        if (idempotencyKey is not null)
        {
            var existingTicket = await FindTicketByIdempotencyKeyAsync(context, shopId, idempotencyKey, cancellationToken);
            if (existingTicket is not null) return Results.Ok(existingTicket);
        }

        if (request.AssignedTechnicianUserId.HasValue && !await IsActiveShopUserAsync(context, shopId, request.AssignedTechnicianUserId.Value, cancellationToken))
        {
            return Results.BadRequest("Technician is not an active member of this shop.");
        }

        var customer = await context.Customers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.CustomerId && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (customer is null) return Results.BadRequest("Customer was not found.");
        if (customer.IsBanned && string.IsNullOrWhiteSpace(request.BannedCustomerOverrideReason))
        {
            return Results.BadRequest("Banned customer override reason is required.");
        }

        var initialStatus = await context.ServiceStatuses.AsNoTracking()
            .Where(status => status.ShopId == shopId)
            .OrderByDescending(status => status.IsInitial)
            .ThenBy(status => status.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);
        if (initialStatus is null) return Results.BadRequest("Initial service status is not configured.");

        var workspace = await shopContext.GetCurrentWorkspaceAsync(cancellationToken);
        var isInternalWorkOrder = request.IsInternalWorkOrder
            ?? (workspace.OperatingMode == OperatingMode.InternalMaintenance);

        string? siteAddressSnapshot = TrimOrNull(request.SiteAddressSnapshot);
        if (request.SiteLocationId.HasValue && string.IsNullOrWhiteSpace(siteAddressSnapshot))
        {
            siteAddressSnapshot = await context.CustomerLocations
                .AsNoTracking()
                .Where(loc => loc.Id == request.SiteLocationId.Value && loc.ShopId == shopId)
                .Select(loc => loc.Address)
                .FirstOrDefaultAsync(cancellationToken);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var device = await GetOrCreateDeviceAsync(context, request, customer.Id, shopId, cancellationToken);
        if (device is null) return Results.BadRequest("Select an existing service asset or provide new asset details.");

        var ticket = new ServiceTicket
        {
            ShopId = shopId,
            CustomerId = customer.Id,
            Device = device,
            StatusId = initialStatus.Id,
            AssignedTechnicianUserId = request.AssignedTechnicianUserId,
            TicketNumber = await GenerateTicketNumberAsync(context, shopId, cancellationToken),
            IdempotencyKey = idempotencyKey,
            PublicTrackingToken = GenerateToken(),
            PublicTrackingEnabled = !isInternalWorkOrder,
            CustomerNameSnapshot = customer.FullName,
            CustomerPhoneSnapshot = customer.PhoneNumber,
            DeviceSnapshot = $"{device.Brand} {device.Model}".Trim(),
            Priority = priority,
            IsInternalWorkOrder = isInternalWorkOrder,
            DepartmentCode = TrimOrNull(request.DepartmentCode) ?? customer.DepartmentCode,
            CostCenter = TrimOrNull(request.CostCenter) ?? customer.CostCenter,
            SlaDueAt = request.SlaDueAt,
            ServiceLocationType = request.ServiceLocationType,
            SiteLocationId = request.SiteLocationId ?? device.SiteLocationId,
            SiteAddressSnapshot = siteAddressSnapshot ?? customer.Address,
            ScheduledVisitStart = request.ScheduledVisitStart,
            ScheduledVisitEnd = request.ScheduledVisitEnd,
            ParentTicketId = request.ParentTicketId,
            Complaint = request.Complaint.Trim(),
            InitialDiagnosis = TrimOrNull(request.InitialDiagnosis),
            PhysicalConditionNotes = TrimOrNull(request.PhysicalConditionNotes),
            AccessorySummary = TrimOrNull(request.AccessorySummary),
            PhotoNotes = TrimOrNull(request.PhotoNotes),
            SecurityPasscode = TrimOrNull(request.SecurityPasscode),
            SecurityPattern = TrimOrNull(request.SecurityPattern),
            PreRepairChecklist = TrimOrNull(request.PreRepairChecklist),
            ApprovalRequired = request.ApprovalRequired,
            IsPreApproved = request.IsPreApproved,
            PaymentStatus = isInternalWorkOrder ? PaymentStatus.Waived : PaymentStatus.Unpaid
        };

        if (request.CreatedAt.HasValue)
        {
            ticket.CreatedAt = request.CreatedAt.Value;
        }

        context.ServiceTickets.Add(ticket);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (idempotencyKey is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            var existingTicket = await FindTicketByIdempotencyKeyAsync(context, shopId, idempotencyKey, cancellationToken);
            if (existingTicket is not null) return Results.Ok(existingTicket);
            throw;
        }
        if (!string.IsNullOrWhiteSpace(ticket.AccessorySummary))
        {
            context.ServiceTicketAccessories.Add(new ServiceTicketAccessory
            {
                ShopId = shopId,
                ServiceTicketId = ticket.Id,
                Name = "Intake accessories",
                Quantity = 1,
                Notes = ticket.AccessorySummary
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        // Persist additional linked assets for multi-unit field visits
        if (request.AdditionalAssetIds is { Count: > 0 })
        {
            var validExtraIds = await context.Devices
                .Where(d => d.ShopId == shopId && d.DeletedAt == null && d.Id != device.Id && request.AdditionalAssetIds.Contains(d.Id))
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

            foreach (var extraId in validExtraIds.Distinct())
            {
                context.ServiceTicketAssets.Add(new ServiceTicketAsset
                {
                    ShopId = shopId,
                    ServiceTicketId = ticket.Id,
                    ServiceAssetId = extraId
                });
            }
            await context.SaveChangesAsync(cancellationToken);
        }

        // Persist dynamic ticket custom fields
        if (request.CustomFields is { Count: > 0 })
        {
            var validFieldIds = await context.AssetFieldDefinitions
                .Where(f => f.ShopId == shopId && f.Scope == CustomFieldScope.Ticket)
                .Select(f => f.Id)
                .ToHashSetAsync(cancellationToken);

            foreach (var cf in request.CustomFields)
            {
                if (!validFieldIds.Contains(cf.FieldDefinitionId) || string.IsNullOrWhiteSpace(cf.Value)) continue;
                context.TicketFieldValues.Add(new TicketFieldValue
                {
                    ShopId = shopId,
                    ServiceTicketId = ticket.Id,
                    FieldDefinitionId = cf.FieldDefinitionId,
                    Value = cf.Value.Trim()
                });
            }
            await context.SaveChangesAsync(cancellationToken);
        }

        // Persist inline new-asset custom fields if provided
        if (request.NewDevice?.CustomFields is { Count: > 0 })
        {
            var validAssetFieldIds = await context.AssetFieldDefinitions
                .Where(f => f.ShopId == shopId && f.Scope == CustomFieldScope.Asset)
                .Select(f => f.Id)
                .ToHashSetAsync(cancellationToken);

            foreach (var cf in request.NewDevice.CustomFields)
            {
                if (!validAssetFieldIds.Contains(cf.FieldDefinitionId) || string.IsNullOrWhiteSpace(cf.Value)) continue;
                context.AssetFieldValues.Add(new AssetFieldValue
                {
                    ShopId = shopId,
                    ServiceAssetId = device.Id,
                    FieldDefinitionId = cf.FieldDefinitionId,
                    Value = cf.Value.Trim()
                });
            }
            await context.SaveChangesAsync(cancellationToken);
        }

        // Persist configurable intake inspection checklist results
        if (request.Inspections is { Count: > 0 })
        {
            foreach (var item in request.Inspections)
            {
                if (string.IsNullOrWhiteSpace(item.Label)) continue;
                context.TicketInspectionResults.Add(new TicketInspectionResult
                {
                    ShopId = shopId,
                    ServiceTicketId = ticket.Id,
                    ChecklistTemplateId = item.ChecklistTemplateId,
                    Label = item.Label.Trim(),
                    Status = item.Status,
                    Notes = TrimOrNull(item.Notes)
                });
            }
            await context.SaveChangesAsync(cancellationToken);
        }

        await AddHistoryAsync(context, principal, shopId, ticket.Id, TicketHistoryEventType.Created, note: "Ticket created.", isPublicVisible: true, createdAt: ticket.CreatedAt, cancellationToken: cancellationToken);
        if (customer.IsBanned)
        {
            await AddHistoryAsync(context, principal, shopId, ticket.Id, TicketHistoryEventType.Updated, note: $"Banned customer override: {request.BannedCustomerOverrideReason!.Trim()}", cancellationToken: cancellationToken);
        }

        if (ticket.IsPreApproved)
        {
            context.ApprovalRequests.Add(new ApprovalRequest
            {
                ShopId = shopId,
                ServiceTicketId = ticket.Id,
                Title = "Pre-approved service",
                Description = "Customer pre-approved work during intake.",
                Status = ApprovalStatus.Approved,
                RequestedByUserId = GetUserId(principal),
                ResolvedByUserId = GetUserId(principal),
                ResolvedAt = DateTimeOffset.UtcNow,
                ResolutionNote = "Pre-approved during intake."
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        await activityLogger.LogAsync(EntityType, ticket.Id, "TicketCreated", $"Ticket created: {ticket.TicketNumber}", cancellationToken: cancellationToken);
        if (idempotencyKey is not null)
        {
            context.ActivityLogs.Add(new()
            {
                ShopId = shopId,
                ActorUserId = GetUserId(principal),
                EntityType = "IdempotencyKey",
                EntityId = ticket.Id,
                ActionType = "ServiceTicketCreate",
                Title = idempotencyKey,
                Description = ticket.TicketNumber
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/v1/service-tickets/{ticket.Id}", new TicketCreatedDto(ticket.Id, ticket.TicketNumber, ticket.PublicTrackingToken));
    }

    private static Task<TicketCreatedDto?> FindTicketByIdempotencyKeyAsync(AppDbContext context, int shopId, string idempotencyKey, CancellationToken cancellationToken)
        => context.ServiceTickets
            .AsNoTracking()
            .Where(ticket => ticket.ShopId == shopId && ticket.IdempotencyKey == idempotencyKey && ticket.DeletedAt == null)
            .Select(ticket => new TicketCreatedDto(ticket.Id, ticket.TicketNumber, ticket.PublicTrackingToken))
            .FirstOrDefaultAsync(cancellationToken);

    private static async Task<Device?> GetOrCreateDeviceAsync(AppDbContext context, CreateTicketRequest request, int customerId, int shopId, CancellationToken cancellationToken)
    {
        if (request.DeviceId.GetValueOrDefault() > 0)
        {
            return await context.Devices
                .FirstOrDefaultAsync(item => item.Id == request.DeviceId && item.CustomerId == customerId && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        }

        var newDevice = request.NewDevice;
        if (newDevice is null ||
            string.IsNullOrWhiteSpace(newDevice.DeviceType) ||
            string.IsNullOrWhiteSpace(newDevice.Brand) ||
            string.IsNullOrWhiteSpace(newDevice.Model))
        {
            return null;
        }

        var device = new Device
        {
            ShopId = shopId,
            CustomerId = customerId,
            DeviceType = newDevice.DeviceType.Trim(),
            Brand = newDevice.Brand.Trim(),
            Model = newDevice.Model.Trim(),
            SerialNumber = TrimOrNull(newDevice.SerialNumber),
            IdentifierLabel = TrimOrNull(newDevice.IdentifierLabel),
            MeterReading = newDevice.MeterReading,
            MeterUnit = TrimOrNull(newDevice.MeterUnit),
            SiteLocationId = newDevice.SiteLocationId,
            Notes = TrimOrNull(newDevice.Notes)
        };

        context.Devices.Add(device);
        return device;
    }

    private static async Task<IResult> GetKanbanAsync(
        AppDbContext context,
        IShopContext shopContext,
        ClaimsPrincipal principal,
        int? technicianUserId,
        bool? myTickets,
        string? priority,
        string? paymentStatus,
        string? approvalStatus,
        string? search,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (myTickets == true)
        {
            technicianUserId = GetUserId(principal);
        }

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var statuses = await context.ServiceStatuses
            .AsNoTracking()
            .Where(status => status.ShopId == shopId)
            .OrderBy(status => status.SortOrder)
            .ThenBy(status => status.Id)
            .Select(status => new { status.Id, status.Name, status.Color, status.SortOrder, status.IsFinal, status.IsCancelled })
            .ToListAsync(cancellationToken);

        var query = ApplyKanbanFilters(context.ServiceTickets.AsNoTracking().Where(ticket => ticket.ShopId == shopId && ticket.DeletedAt == null), technicianUserId, priority, paymentStatus, search, from, to);
        var tickets = await query
            .OrderByDescending(ticket => ticket.Priority == ServiceTicketPriority.Urgent)
            .ThenByDescending(ticket => ticket.Priority == ServiceTicketPriority.High)
            .ThenBy(ticket => ticket.CreatedAt)
            .Select(ticket => new
            {
                ticket.Id,
                ticket.StatusId,
                ticket.TicketNumber,
                ticket.CustomerNameSnapshot,
                ticket.DeviceSnapshot,
                ticket.Complaint,
                ticket.Priority,
                ticket.PaymentStatus,
                ticket.CreatedAt,
                ticket.ApprovalRequired,
                ticket.Status.IsBlocked,
                ticket.Status.IsFinal,
                ticket.Status.IsCancelled,
                AssignedTechnician = ticket.AssignedTechnicianUser == null ? null : (ticket.AssignedTechnicianUser.FirstName + " " + ticket.AssignedTechnicianUser.LastName).Trim(),
                HasApprovedApproval = context.ApprovalRequests.Any(approval => approval.ServiceTicketId == ticket.Id && approval.Status == ApprovalStatus.Approved),
                HasPendingApproval = context.ApprovalRequests.Any(approval => approval.ServiceTicketId == ticket.Id && approval.Status == ApprovalStatus.Pending)
            })
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(approvalStatus))
        {
            tickets = approvalStatus.Trim().ToLowerInvariant() switch
            {
                "pending" => tickets.Where(ticket => !ticket.IsFinal && !ticket.IsCancelled && (ticket.HasPendingApproval || (ticket.ApprovalRequired && !ticket.HasApprovedApproval))).ToList(),
                "approved" => tickets.Where(ticket => ticket.HasApprovedApproval).ToList(),
                "none" => tickets.Where(ticket => !ticket.HasPendingApproval && !ticket.HasApprovedApproval && !ticket.ApprovalRequired).ToList(),
                _ => tickets
            };
        }

        var overdueThreshold = DateTimeOffset.UtcNow.AddDays(-3);
        var columns = statuses
            .Select(status => new KanbanColumnDto(
                status.Id,
                status.Name,
                status.Color,
                status.SortOrder,
                tickets
                    .Where(ticket => ticket.StatusId == status.Id)
                    .Select(ticket => new KanbanTicketDto(
                        ticket.Id,
                        ticket.TicketNumber,
                        ticket.CustomerNameSnapshot,
                        ticket.DeviceSnapshot,
                        ticket.Complaint,
                        ticket.Priority.ToString(),
                        ticket.PaymentStatus.ToString(),
                        ticket.AssignedTechnician,
                        ticket.CreatedAt,
                        !ticket.IsFinal && !ticket.IsCancelled && (ticket.HasPendingApproval || (ticket.ApprovalRequired && !ticket.HasApprovedApproval)),
                        ticket.PaymentStatus is PaymentStatus.Unpaid or PaymentStatus.Partial,
                        ticket.IsBlocked,
                        !ticket.IsFinal && ticket.CreatedAt < overdueThreshold))
                    .ToList(),
                status.IsFinal,
                status.IsCancelled))
            .ToList();

        return Results.Ok(columns);
    }

    private static async Task<IResult> GetDashboardSummaryAsync(AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var tomorrowStart = todayStart.AddDays(1);
        var overdueThreshold = now.AddDays(-3);

        var activeTickets = context.ServiceTickets.AsNoTracking().Where(ticket => ticket.ShopId == shopId && ticket.DeletedAt == null);
        var openTickets = await activeTickets.CountAsync(ticket => !ticket.Status.IsFinal && !ticket.Status.IsCancelled, cancellationToken);
        var waitingApproval = await activeTickets.CountAsync(ticket =>
            !ticket.Status.IsFinal &&
            !ticket.Status.IsCancelled &&
            (context.ApprovalRequests.Any(approval => approval.ServiceTicketId == ticket.Id && approval.Status == ApprovalStatus.Pending) ||
             (ticket.ApprovalRequired &&
              !context.ApprovalRequests.Any(approval => approval.ServiceTicketId == ticket.Id && approval.Status == ApprovalStatus.Approved))),
            cancellationToken);
        var unpaidTickets = await activeTickets.CountAsync(ticket => ticket.PaymentStatus == PaymentStatus.Unpaid || ticket.PaymentStatus == PaymentStatus.Partial, cancellationToken);
        var blockedTickets = await activeTickets.CountAsync(ticket => ticket.Status.IsBlocked, cancellationToken);
        var overdueTickets = await activeTickets.CountAsync(ticket => !ticket.Status.IsFinal && ticket.CreatedAt < overdueThreshold, cancellationToken);
        var completedToday = await activeTickets.CountAsync(ticket => ticket.Status.IsFinal && ticket.UpdatedAt >= todayStart && ticket.UpdatedAt < tomorrowStart, cancellationToken);

        return Results.Ok(new DashboardSummaryDto(openTickets, waitingApproval, unpaidTickets, blockedTickets, overdueTickets, completedToday));
    }

    private static async Task<IResult> GetTicketAsync(int id, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets
            .AsNoTracking()
            .Where(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null)
            .Select(item => new
            {
                Ticket = item,
                StatusName = item.Status.Name,
                StatusColor = item.Status.Color,
                AssignedTechnician = item.AssignedTechnicianUser == null ? null : (item.AssignedTechnicianUser.FirstName + " " + item.AssignedTechnicianUser.LastName).Trim(),
                HasPendingApproval = context.ApprovalRequests.Any(approval => approval.ShopId == shopId && approval.ServiceTicketId == item.Id && approval.Status == ApprovalStatus.Pending),
                Accessories = context.ServiceTicketAccessories
                    .Where(accessory => accessory.ShopId == shopId && accessory.ServiceTicketId == item.Id)
                    .OrderBy(accessory => accessory.Id)
                    .Select(accessory => new AccessoryDto(accessory.Id, accessory.Name, accessory.Quantity, accessory.Notes))
                    .ToList(),
                Attachments = context.ServiceTicketAttachments
                    .Where(attachment => attachment.ShopId == shopId && attachment.ServiceTicketId == item.Id)
                    .OrderByDescending(attachment => attachment.CreatedAt)
                    .Select(attachment => new AttachmentDto(attachment.Id, attachment.FileName, attachment.MimeType, attachment.FileSize, attachment.AttachmentType.ToString(), attachment.IsPublicVisible, attachment.CreatedAt))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (ticket is null) return Results.NotFound();

        var billing = await TicketBillingService.GetBillingSnapshotAsync(context, shopId, ticket.Ticket, cancellationToken);

        var linkedAssets = await context.ServiceTicketAssets
            .AsNoTracking()
            .Where(x => x.ShopId == shopId && x.ServiceTicketId == ticket.Ticket.Id)
            .Select(x => new LinkedAssetDto(x.ServiceAssetId, x.ServiceAsset.Brand, x.ServiceAsset.Model, x.ServiceAsset.SerialNumber, x.Notes))
            .ToListAsync(cancellationToken);

        var ticketCustomFields = await context.TicketFieldValues
            .AsNoTracking()
            .Where(v => v.ShopId == shopId && v.ServiceTicketId == ticket.Ticket.Id)
            .OrderBy(v => v.FieldDefinition.SortOrder)
            .Select(v => new BlueprintEndpoints.FieldValueDto(
                v.FieldDefinitionId, v.FieldDefinition.FieldKey, v.FieldDefinition.LabelEn, v.FieldDefinition.LabelId,
                v.FieldDefinition.FieldType.ToString(), v.Value, v.FieldDefinition.ShowOnPrint, v.FieldDefinition.ShowOnPublicTracking))
            .ToListAsync(cancellationToken);

        var assetCustomFields = await context.AssetFieldValues
            .AsNoTracking()
            .Where(v => v.ShopId == shopId && v.ServiceAssetId == ticket.Ticket.DeviceId)
            .OrderBy(v => v.FieldDefinition.SortOrder)
            .Select(v => new BlueprintEndpoints.FieldValueDto(
                v.FieldDefinitionId, v.FieldDefinition.FieldKey, v.FieldDefinition.LabelEn, v.FieldDefinition.LabelId,
                v.FieldDefinition.FieldType.ToString(), v.Value, v.FieldDefinition.ShowOnPrint, v.FieldDefinition.ShowOnPublicTracking))
            .ToListAsync(cancellationToken);

        var inspections = await context.TicketInspectionResults
            .AsNoTracking()
            .Where(r => r.ShopId == shopId && r.ServiceTicketId == ticket.Ticket.Id)
            .OrderBy(r => r.Id)
            .Select(r => new BlueprintEndpoints.TicketInspectionDto(r.Id, r.ChecklistTemplateId, r.Label, r.Status.ToString(), r.Notes))
            .ToListAsync(cancellationToken);

        return Results.Ok(new TicketDetailDto(
            ticket.Ticket.Id,
            ticket.Ticket.TicketNumber,
            ticket.Ticket.CustomerId,
            ticket.Ticket.DeviceId,
            ticket.Ticket.StatusId,
            ticket.StatusName,
            ticket.StatusColor,
            ticket.Ticket.CustomerNameSnapshot,
            ticket.Ticket.CustomerPhoneSnapshot,
            ticket.Ticket.DeviceSnapshot,
            ticket.Ticket.Priority.ToString(),
            ticket.Ticket.Complaint,
            ticket.Ticket.InitialDiagnosis,
            ticket.Ticket.PhysicalConditionNotes,
            ticket.Ticket.AccessorySummary,
            ticket.Ticket.PhotoNotes,
            ticket.Ticket.SecurityPasscode,
            ticket.Ticket.SecurityPattern,
            ticket.Ticket.PreRepairChecklist,
            ticket.Ticket.Diagnosis,
            ticket.Ticket.RepairNotes,
            ticket.Ticket.WarrantyUntil,
            ticket.Ticket.WarrantyTerms,
            IsWarrantyActive(ticket.Ticket.WarrantyUntil),
            ticket.Ticket.ApprovalRequired,
            ticket.Ticket.IsPreApproved,
            billing.PaymentStatus.ToString(),
            ticket.AssignedTechnician,
            ticket.Ticket.AssignedTechnicianUserId,
            ticket.Accessories,
            ticket.Attachments,
            await GetAllowedTransitionsAsync(context, shopId, ticket.Ticket.StatusId, cancellationToken),
            ticket.Ticket.CreatedAt,
            ticket.Ticket.UpdatedAt,
            ticket.HasPendingApproval,
            billing.WorkItemsSubtotal,
            billing.ConsumedPartsSubtotal,
            billing.DiscountAmount,
            billing.TotalAmount,
            billing.AmountPaid,
            billing.RemainingBalance,
            billing.OverpaidAmount,
            ticket.Ticket.IsInternalWorkOrder,
            ticket.Ticket.DepartmentCode,
            ticket.Ticket.CostCenter,
            ticket.Ticket.SlaDueAt,
            ticket.Ticket.ServiceLocationType.ToString(),
            ticket.Ticket.SiteLocationId,
            ticket.Ticket.SiteAddressSnapshot,
            ticket.Ticket.ScheduledVisitStart,
            ticket.Ticket.ScheduledVisitEnd,
            ticket.Ticket.ParentTicketId,
            linkedAssets,
            ticketCustomFields,
            assetCustomFields,
            inspections));
    }

    private static async Task<IResult> GetTicketByNumberAsync(string ticketNumber, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticketNumber)) return Results.BadRequest("Ticket number is required.");

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var id = await context.ServiceTickets.AsNoTracking()
            .Where(item => item.ShopId == shopId && item.DeletedAt == null && item.TicketNumber == ticketNumber.Trim())
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return id is null
            ? Results.NotFound()
            : await GetTicketAsync(id.Value, context, shopContext, cancellationToken);
    }

    private static async Task<IResult> GetWarrantyAsync(int id, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets.AsNoTracking()
            .Where(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null)
            .Select(item => new { item.Id, item.TicketNumber, item.WarrantyUntil, item.WarrantyTerms })
            .FirstOrDefaultAsync(cancellationToken);

        return ticket is null
            ? Results.NotFound()
            : Results.Ok(new WarrantyDto(ticket.Id, ticket.TicketNumber, IsWarrantyActive(ticket.WarrantyUntil), ticket.WarrantyUntil, ticket.WarrantyTerms));
    }

    private static async Task<IResult> GetReceiptAsync(int id, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticketRow = await context.ServiceTickets.AsNoTracking()
            .Where(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null)
            .Select(item => new
            {
                Ticket = item,
                Status = item.Status.Name
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (ticketRow is null) return Results.NotFound();

        var ticket = ticketRow.Ticket;
        var settings = await GetShopProfileSettings.ReadSettingsAsync(context, shopContext, cancellationToken);
        var printSettings = await PrintTemplateSettingsEndpoints.ReadSettingsAsync(context, shopContext, cancellationToken);
        var billing = await TicketBillingService.GetBillingSnapshotAsync(context, shopId, ticket, cancellationToken);

        var rawWorkItems = await context.ServiceTicketWorkItems.AsNoTracking()
            .Where(work => work.ShopId == shopId && work.ServiceTicketId == id && work.Status == WorkItemStatus.Approved)
            .OrderBy(work => work.CreatedAt)
            .ThenBy(work => work.Id)
            .Select(work => new
            {
                work.ItemType,
                work.Title,
                work.Description,
                work.Quantity,
                work.UnitPrice
            })
            .ToListAsync(cancellationToken);

        var workItems = rawWorkItems
            .Select(work => new ReceiptWorkItemDto(
                work.ItemType.ToString(),
                work.Title,
                work.Description,
                work.Quantity,
                work.UnitPrice,
                TicketBillingCalculator.ComputeLineTotal(work.Quantity, work.UnitPrice)))
            .ToList();

        var rawUsages = await context.TicketInventoryUsages.AsNoTracking()
            .Where(usage => usage.ShopId == shopId && usage.ServiceTicketId == id)
            .OrderBy(usage => usage.CreatedAt)
            .ThenBy(usage => usage.Id)
            .Select(usage => new
            {
                usage.InventoryItem.Sku,
                usage.InventoryItem.Name,
                usage.InventoryItem.Unit,
                usage.ConsumedQuantity,
                usage.ReturnedQuantity,
                usage.UnitPriceSnapshot,
                CurrentPrice = usage.InventoryItem.FixedSalePrice > 0
                    ? usage.InventoryItem.FixedSalePrice
                    : usage.InventoryItem.SalePrice
            })
            .ToListAsync(cancellationToken);

        var consumedParts = rawUsages
            .Select(usage => new
            {
                usage.Sku,
                usage.Name,
                usage.Unit,
                NetQuantity = Math.Max(0m, usage.ConsumedQuantity - usage.ReturnedQuantity),
                UnitPrice = usage.UnitPriceSnapshot ?? usage.CurrentPrice
            })
            .Where(usage => usage.NetQuantity > 0m)
            .Select(usage => new ReceiptPartDto(
                usage.Sku,
                usage.Name,
                usage.Unit,
                usage.NetQuantity,
                usage.UnitPrice,
                TicketBillingCalculator.ComputeLineTotal(usage.NetQuantity, usage.UnitPrice)))
            .ToList();

        var workspace = await shopContext.GetCurrentWorkspaceAsync(cancellationToken);
        var printAssetFields = await context.AssetFieldValues
            .AsNoTracking()
            .Where(v => v.ShopId == shopId && v.ServiceAssetId == ticket.DeviceId && v.FieldDefinition.ShowOnPrint && v.Value != null)
            .OrderBy(v => v.FieldDefinition.SortOrder)
            .Select(v => new BlueprintEndpoints.FieldValueDto(
                v.FieldDefinitionId, v.FieldDefinition.FieldKey, v.FieldDefinition.LabelEn, v.FieldDefinition.LabelId,
                v.FieldDefinition.FieldType.ToString(), v.Value, true, v.FieldDefinition.ShowOnPublicTracking))
            .ToListAsync(cancellationToken);
        var printTicketFields = await context.TicketFieldValues
            .AsNoTracking()
            .Where(v => v.ShopId == shopId && v.ServiceTicketId == ticket.Id && v.FieldDefinition.ShowOnPrint && v.Value != null)
            .OrderBy(v => v.FieldDefinition.SortOrder)
            .Select(v => new BlueprintEndpoints.FieldValueDto(
                v.FieldDefinitionId, v.FieldDefinition.FieldKey, v.FieldDefinition.LabelEn, v.FieldDefinition.LabelId,
                v.FieldDefinition.FieldType.ToString(), v.Value, true, v.FieldDefinition.ShowOnPublicTracking))
            .ToListAsync(cancellationToken);
        var printFields = printAssetFields.Concat(printTicketFields).ToList();

        return Results.Ok(new TicketReceiptDto(
            ticket.Id,
            ticket.TicketNumber,
            ticket.CustomerNameSnapshot,
            ticket.CustomerPhoneSnapshot,
            ticket.DeviceSnapshot,
            ticket.Complaint,
            ticket.InitialDiagnosis,
            ticket.PhysicalConditionNotes,
            ticket.AccessorySummary,
            ticket.PhotoNotes,
            ticket.SecurityPasscode,
            ticket.SecurityPattern,
            ticket.Priority.ToString(),
            ticketRow.Status,
            ticket.CreatedAt,
            ticket.PublicTrackingToken,
            settings.ShopName,
            settings.LogoUrl,
            settings.PhoneNumber,
            settings.WhatsAppNumber,
            settings.Address,
            settings.ReceiptFooter,
            printSettings.TermsAndConditions,
            printSettings.ShowPhysicalCondition,
            printSettings.ShowAccessories,
            printSettings.ShowPhotoNotes,
            printSettings.ShowQrCode,
            printSettings.ShowPhoneNumber,
            printSettings.ShowPaymentSummary && !ticket.IsInternalWorkOrder,
            billing.PaymentStatus.ToString(),
            settings.CurrencyCode,
            billing.WorkItemsSubtotal,
            billing.ConsumedPartsSubtotal,
            billing.DiscountAmount,
            billing.TotalAmount,
            billing.AmountPaid,
            billing.RemainingBalance,
            billing.OverpaidAmount,
            workItems,
            consumedParts,
            ticket.ServiceLocationType.ToString(),
            ticket.SiteAddressSnapshot,
            ticket.ScheduledVisitStart,
            ticket.IsInternalWorkOrder,
            printFields,
            IndustryBlueprintCatalog.DeserializeTerminology(workspace.TerminologyConfig, workspace.IndustryBlueprintKey)));
    }

    private static async Task<IResult> LogReceiptPrintAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var exists = await context.ServiceTickets
            .AnyAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (!exists) return Results.NotFound();

        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.ReceiptPrinted, note: "Receipt printed.", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AddAccessoryAsync(
        int id,
        [FromBody] UpsertAccessoryRequest request,
        AppDbContext context,
        IShopContext shopContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var validationErrors = new List<(string Field, string Message)>();
        if (string.IsNullOrWhiteSpace(request.Name)) validationErrors.Add(("name", "Accessory name is required."));
        if (request.Quantity <= 0) validationErrors.Add(("quantity", "Quantity must be greater than zero."));
        if (validationErrors.Count > 0) return Validation(validationErrors);

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (!await TicketExistsAsync(context, shopId, id, cancellationToken)) return Results.NotFound();

        context.ServiceTicketAccessories.Add(new ServiceTicketAccessory
        {
            ShopId = shopId,
            ServiceTicketId = id,
            Name = request.Name.Trim(),
            Quantity = request.Quantity,
            Notes = TrimOrNull(request.Notes)
        });
        await context.SaveChangesAsync(cancellationToken);
        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.Updated, note: $"Accessory added: {request.Name.Trim()}", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAccessoryAsync(
        int id,
        int accessoryId,
        AppDbContext context,
        IShopContext shopContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var accessory = await context.ServiceTicketAccessories.FirstOrDefaultAsync(item => item.Id == accessoryId && item.ServiceTicketId == id && item.ShopId == shopId, cancellationToken);
        if (accessory is null) return Results.NotFound();

        context.ServiceTicketAccessories.Remove(accessory);
        await context.SaveChangesAsync(cancellationToken);
        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.Updated, note: $"Accessory removed: {accessory.Name}", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> UploadAttachmentAsync(
        int id,
        IFormFile file,
        string? attachmentType,
        bool? isPublicVisible,
        HttpRequest httpRequest,
        AppDbContext context,
        IShopContext shopContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var validationErrors = new List<(string Field, string Message)>();
        if (file.Length == 0) validationErrors.Add(("file", "File is required."));
        if (file.Length > 5 * 1024 * 1024) validationErrors.Add(("file", "File must be 5MB or smaller."));
        if (!AllowedAttachmentMimeTypes.Contains(file.ContentType)) validationErrors.Add(("file", "Only JPEG, PNG, or WEBP images are allowed."));
        if (validationErrors.Count > 0) return Validation(validationErrors);
        var rawAttachmentType = attachmentType ?? (httpRequest.HasFormContentType ? httpRequest.Form["attachmentType"].FirstOrDefault() : null);
        if (!Enum.TryParse<ServiceTicketAttachmentType>(rawAttachmentType, true, out var parsedType)) parsedType = ServiceTicketAttachmentType.Other;
        var rawIsPublic = isPublicVisible;
        if (!rawIsPublic.HasValue && httpRequest.HasFormContentType)
        {
            var formVal = httpRequest.Form["isPublicVisible"].FirstOrDefault();
            if (bool.TryParse(formVal, out var parsedBool)) rawIsPublic = parsedBool;
            else if (string.Equals(formVal, "on", StringComparison.OrdinalIgnoreCase) || formVal == "1") rawIsPublic = true;
        }

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (!await TicketExistsAsync(context, shopId, id, cancellationToken)) return Results.NotFound();

        var uploadRoot = AttachmentRoot();
        Directory.CreateDirectory(uploadRoot);
        var storedFileName = $"{Guid.NewGuid():N}{AllowedAttachmentExtensions[file.ContentType]}";
        var path = Path.Combine(uploadRoot, storedFileName);
        try
        {
            await using (var stream = File.Create(path))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            context.ServiceTicketAttachments.Add(new ServiceTicketAttachment
            {
                ShopId = shopId,
                ServiceTicketId = id,
                FileName = Path.GetFileName(file.FileName),
                StoredFileName = storedFileName,
                MimeType = file.ContentType,
                FileSize = file.Length,
                AttachmentType = parsedType,
                IsPublicVisible = rawIsPublic ?? false,
                UploadedByUserId = GetUserId(principal)
            });
            await context.SaveChangesAsync(cancellationToken);
            await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.Updated, note: $"Attachment uploaded: {file.FileName}", cancellationToken: cancellationToken);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateAttachmentVisibilityAsync(
        int id,
        int attachmentId,
        [FromBody] UpdateAttachmentVisibilityRequest request,
        AppDbContext context,
        IShopContext shopContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var attachment = await context.ServiceTicketAttachments
            .FirstOrDefaultAsync(item => item.Id == attachmentId && item.ServiceTicketId == id && item.ShopId == shopId, cancellationToken);
        if (attachment is null) return Results.NotFound();

        attachment.IsPublicVisible = request.IsPublicVisible;
        await context.SaveChangesAsync(cancellationToken);
        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.Updated, note: $"Attachment visibility updated: {attachment.FileName} ({(request.IsPublicVisible ? "Public" : "Internal")})", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DownloadAttachmentAsync(int id, int attachmentId, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var attachment = await context.ServiceTicketAttachments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == attachmentId && item.ServiceTicketId == id && item.ShopId == shopId, cancellationToken);
        if (attachment is null) return Results.NotFound();

        var path = Path.Combine(AttachmentRoot(), attachment.StoredFileName);
        return File.Exists(path)
            ? Results.File(path, attachment.MimeType, attachment.FileName)
            : Results.NotFound();
    }

    private static async Task<IResult> DeleteAttachmentAsync(int id, int attachmentId, AppDbContext context, IShopContext shopContext, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var attachment = await context.ServiceTicketAttachments.FirstOrDefaultAsync(item => item.Id == attachmentId && item.ServiceTicketId == id && item.ShopId == shopId, cancellationToken);
        if (attachment is null) return Results.NotFound();

        var path = Path.Combine(AttachmentRoot(), attachment.StoredFileName);
        if (File.Exists(path)) File.Delete(path);
        context.ServiceTicketAttachments.Remove(attachment);
        await context.SaveChangesAsync(cancellationToken);
        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.Updated, note: $"Attachment deleted: {attachment.FileName}", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCustomerServiceHistoryAsync(int customerId, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (!await context.Customers.AnyAsync(item => item.Id == customerId && item.ShopId == shopId && item.DeletedAt == null, cancellationToken)) return Results.NotFound();
        return Results.Ok(await GetServiceHistoryTicketsAsync(context, shopId, customerId, null, cancellationToken));
    }

    private static async Task<IResult> GetDeviceServiceHistoryAsync(int customerId, int deviceId, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (!await context.Devices.AnyAsync(item => item.Id == deviceId && item.CustomerId == customerId && item.ShopId == shopId && item.DeletedAt == null, cancellationToken)) return Results.NotFound();
        return Results.Ok(await GetServiceHistoryTicketsAsync(context, shopId, customerId, deviceId, cancellationToken));
    }

    private static async Task<IResult> GetRepeatCustomerSummaryAsync(int customerId, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (!await context.Customers.AnyAsync(item => item.Id == customerId && item.ShopId == shopId && item.DeletedAt == null, cancellationToken)) return Results.NotFound();

        var tickets = await context.ServiceTickets.AsNoTracking()
            .Where(ticket => ticket.ShopId == shopId && ticket.CustomerId == customerId && ticket.DeletedAt == null)
            .Select(ticket => new { ticket.CreatedAt, ticket.Status.IsFinal, ticket.Status.IsCancelled })
            .ToListAsync(cancellationToken);

        return Results.Ok(new RepeatCustomerSummaryDto(
            customerId,
            tickets.Count,
            tickets.Count(ticket => !ticket.IsFinal && !ticket.IsCancelled),
            tickets.Count(ticket => ticket.IsFinal && !ticket.IsCancelled),
            tickets.Count == 0 ? null : tickets.Max(ticket => ticket.CreatedAt)));
    }

    private static async Task<IResult> UpdateTicketAsync(int id, [FromBody] UpdateTicketRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var validationErrors = new List<(string Field, string Message)>();
        if (string.IsNullOrWhiteSpace(request.Complaint)) validationErrors.Add(("complaint", "Complaint is required."));
        if (!TryParsePriority(request.Priority, out var priority)) validationErrors.Add(("priority", "Invalid priority."));
        if (request.CreatedAt.HasValue && request.CreatedAt.Value > DateTimeOffset.UtcNow.AddDays(1)) validationErrors.Add(("createdAt", "Intake date cannot be in the future."));
        if (validationErrors.Count > 0) return Validation(validationErrors);

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();
        if (ticket.Status.IsFinal || ticket.Status.IsCancelled) return Results.BadRequest("Final or cancelled tickets cannot be edited.");
        if (request.UpdatedAt.HasValue && ticket.UpdatedAt.HasValue && request.UpdatedAt.Value != ticket.UpdatedAt.Value)
        {
            return Results.Problem(
                detail: "Ticket was updated by another user. Reload before saving again.",
                statusCode: StatusCodes.Status409Conflict);
        }
        if (request.WarrantyUntil.HasValue && request.WarrantyUntil.Value.Date < (request.CreatedAt ?? ticket.CreatedAt).Date) return Validation(("warrantyUntil", "Warranty date cannot be before intake date."));

        ticket.Complaint = request.Complaint.Trim();
        ticket.InitialDiagnosis = TrimOrNull(request.InitialDiagnosis);
        ticket.PhysicalConditionNotes = TrimOrNull(request.PhysicalConditionNotes);
        ticket.AccessorySummary = TrimOrNull(request.AccessorySummary);
        ticket.PhotoNotes = TrimOrNull(request.PhotoNotes);
        ticket.SecurityPasscode = TrimOrNull(request.SecurityPasscode);
        ticket.SecurityPattern = TrimOrNull(request.SecurityPattern);
        ticket.PreRepairChecklist = TrimOrNull(request.PreRepairChecklist);
        ticket.Diagnosis = TrimOrNull(request.Diagnosis);
        ticket.RepairNotes = TrimOrNull(request.RepairNotes);
        ticket.WarrantyUntil = request.WarrantyUntil;
        ticket.WarrantyTerms = TrimOrNull(request.WarrantyTerms);
        ticket.Priority = priority;
        if (request.IsInternalWorkOrder.HasValue) ticket.IsInternalWorkOrder = request.IsInternalWorkOrder.Value;
        if (request.DepartmentCode is not null) ticket.DepartmentCode = TrimOrNull(request.DepartmentCode);
        if (request.CostCenter is not null) ticket.CostCenter = TrimOrNull(request.CostCenter);
        if (request.SlaDueAt.HasValue) ticket.SlaDueAt = request.SlaDueAt;
        if (request.ServiceLocationType.HasValue) ticket.ServiceLocationType = request.ServiceLocationType.Value;
        if (request.SiteLocationId.HasValue) ticket.SiteLocationId = request.SiteLocationId;
        if (request.SiteAddressSnapshot is not null) ticket.SiteAddressSnapshot = TrimOrNull(request.SiteAddressSnapshot);
        if (request.ScheduledVisitStart.HasValue) ticket.ScheduledVisitStart = request.ScheduledVisitStart;
        if (request.ScheduledVisitEnd.HasValue) ticket.ScheduledVisitEnd = request.ScheduledVisitEnd;
        if (request.CreatedAt.HasValue)
        {
            ticket.CreatedAt = request.CreatedAt.Value;
            var initialHistory = await context.ServiceTicketHistories
                .FirstOrDefaultAsync(h => h.ServiceTicketId == ticket.Id && h.EventType == TicketHistoryEventType.Created, cancellationToken);
            if (initialHistory != null)
            {
                initialHistory.CreatedAt = request.CreatedAt.Value;
            }
        }
        await context.SaveChangesAsync(cancellationToken);
        await AddHistoryAsync(context, principal, shopId, ticket.Id, TicketHistoryEventType.Updated, note: "Ticket details updated.", cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, ticket.Id, "TicketUpdated", $"Ticket updated: {ticket.TicketNumber}", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangeStatusAsync(int id, [FromBody] ChangeStatusRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets.Include(item => item.Status).FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();

        var toStatus = await context.ServiceStatuses.FirstOrDefaultAsync(status => status.Id == request.StatusId && status.ShopId == shopId, cancellationToken);
        if (toStatus is null) return Results.BadRequest("Target status was not found.");

        var rule = await context.StatusTransitionRules.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ShopId == shopId && item.FromStatusId == ticket.StatusId && item.ToStatusId == request.StatusId && item.IsActive, cancellationToken);
        var hasAnyRules = await context.StatusTransitionRules.AnyAsync(item => item.ShopId == shopId && item.FromStatusId == ticket.StatusId && item.IsActive, cancellationToken);
        if (hasAnyRules && rule is null) return Results.BadRequest("Status transition is not allowed.");
        if (!hasAnyRules && (ticket.Status.IsFinal || ticket.Status.IsCancelled)) return Results.BadRequest("Status transition is not allowed.");

        var requiresReason = toStatus.IsCancelled || toStatus.IsBlocked || rule?.RequiresReason == true;
        if (requiresReason && string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest("Reason is required for this status change.");

        var hasApprovedApproval = await context.ApprovalRequests
            .AnyAsync(item => item.ShopId == shopId && item.ServiceTicketId == ticket.Id && item.Status == ApprovalStatus.Approved, cancellationToken);
        var hasPendingApproval = await context.ApprovalRequests
            .AnyAsync(item => item.ShopId == shopId && item.ServiceTicketId == ticket.Id && item.Status == ApprovalStatus.Pending, cancellationToken);

        var requiresApproval = (ticket.ApprovalRequired && !toStatus.IsCancelled) || rule?.RequiresApproval == true;
        if (requiresApproval && (!hasApprovedApproval || hasPendingApproval))
        {
            return Results.BadRequest("Approved customer approval is required before this status change.");
        }

        var isForwardProgression = !toStatus.IsCancelled && !toStatus.IsBlocked && (toStatus.IsFinal || toStatus.SortOrder > ticket.Status.SortOrder);
        if (hasPendingApproval && isForwardProgression)
        {
            return Results.BadRequest("Pending customer approval requests must be resolved before moving this ticket forward.");
        }

        var requiresPaymentCompleted = rule is not null
            ? rule.RequiresPaymentCompleted
            : (toStatus.IsFinal && !toStatus.IsCancelled);
        var paymentBlocked = requiresPaymentCompleted && !ticket.IsInternalWorkOrder && ticket.PaymentStatus is not PaymentStatus.Paid and not PaymentStatus.Waived;
        if (paymentBlocked && !request.OverridePaymentBlock)
        {
            return Results.BadRequest("Payment must be completed before this status change.");
        }

        if (paymentBlocked)
        {
            if (!await IsSettingEnabledAsync(context, shopId, ShopProfileSettingKeys.AllowUnpaidPickupOverride, cancellationToken))
            {
                return Results.BadRequest("Unpaid pickup override is disabled for this shop.");
            }

            if (string.IsNullOrWhiteSpace(ticket.PickupOverrideReason))
            {
                return Results.BadRequest("Pickup override reason is required before overriding the payment block.");
            }
        }

        var fromStatusId = ticket.StatusId;
        ticket.StatusId = toStatus.Id;
        if (toStatus.IsCancelled)
        {
            await InventoryEndpoints.ReleaseOpenReservationsForTicketAsync(context, activityLogger, shopId, ticket.Id, cancellationToken);

            var openApprovals = await context.ApprovalRequests
                .Where(item => item.ShopId == shopId && item.ServiceTicketId == ticket.Id && item.Status == ApprovalStatus.Pending)
                .ToListAsync(cancellationToken);
            if (openApprovals.Count > 0)
            {
                var now = DateTimeOffset.UtcNow;
                var actorUserId = GetUserId(principal);
                foreach (var openApproval in openApprovals)
                {
                    openApproval.Status = ApprovalStatus.Cancelled;
                    openApproval.ResolvedAt = now;
                    openApproval.ResolvedByUserId = actorUserId;
                    openApproval.ResolutionNote = TrimOrNull(request.Reason) ?? "Cancelled when ticket was cancelled.";
                }

                var approvalIds = openApprovals.Select(item => item.Id).ToList();
                var pendingWorkItems = await context.ServiceTicketWorkItems
                    .Where(item =>
                        item.ShopId == shopId &&
                        item.ServiceTicketId == ticket.Id &&
                        item.ApprovalRequestId.HasValue &&
                        approvalIds.Contains(item.ApprovalRequestId.Value) &&
                        item.Status == WorkItemStatus.PendingApproval)
                    .ToListAsync(cancellationToken);
                foreach (var workItem in pendingWorkItems)
                {
                    workItem.Status = WorkItemStatus.Cancelled;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await AddHistoryAsync(context, principal, shopId, ticket.Id, TicketHistoryEventType.StatusChanged, fromStatusId, toStatus.Id, request.Reason, isPublicVisible: true, cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, ticket.Id, "TicketStatusChanged", $"Ticket {ticket.TicketNumber} moved to {toStatus.Name}", request.Reason, cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AssignTicketAsync(int id, [FromBody] AssignTicketRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets.FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();

        if (request.TechnicianUserId.HasValue)
        {
            if (!await IsActiveShopUserAsync(context, shopId, request.TechnicianUserId.Value, cancellationToken))
            {
                return Results.BadRequest("Technician is not an active member of this shop.");
            }
        }

        ticket.AssignedTechnicianUserId = request.TechnicianUserId;
        await context.SaveChangesAsync(cancellationToken);
        await AddHistoryAsync(context, principal, shopId, ticket.Id, TicketHistoryEventType.Assigned, note: request.TechnicianUserId.HasValue ? "Technician assigned." : "Technician unassigned.", cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, ticket.Id, "TicketAssigned", $"Ticket assignment updated: {ticket.TicketNumber}", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AddNoteAsync(int id, [FromBody] AddNoteRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Note)) return Results.BadRequest("Note is required.");
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var exists = await context.ServiceTickets.AnyAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (!exists) return Results.NotFound();

        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.NoteAdded, note: request.Note.Trim(), isPublicVisible: request.IsPublicVisible, cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, id, "TicketNoteAdded", "Ticket note added.", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetHistoryAsync(int id, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (!await context.ServiceTickets.AnyAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken)) return Results.NotFound();
        var history = await GetHistoryDtosAsync(context, shopId, id, false, cancellationToken);
        return Results.Ok(history);
    }

    private static async Task<IResult> GetApprovalsAsync(int id, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var approvals = await context.ApprovalRequests.AsNoTracking()
            .Where(item => item.ShopId == shopId && item.ServiceTicketId == id)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new ApprovalDto(item.Id, item.Title, item.Description, item.EstimatedAmount, item.Status.ToString(), item.ResolutionNote, item.CreatedAt, item.ResolvedAt))
            .ToListAsync(cancellationToken);
        return Results.Ok(approvals);
    }

    private static async Task<IResult> CreateApprovalAsync(int id, [FromBody] CreateApprovalRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return Results.BadRequest("Approval title and description are required.");
        }

        var validationErrors = new List<(string Field, string Message)>();
        var validatedItems = new List<(WorkItemType ItemType, string Title, string? Description, decimal Quantity, decimal UnitPrice)>();
        if (request.ProposedWorkItems is not null)
        {
            var index = 0;
            foreach (var proposed in request.ProposedWorkItems)
            {
                if (!TryParseWorkItemType(proposed.ItemType, out var itemType))
                {
                    validationErrors.Add(($"proposedWorkItems[{index}].itemType", "Invalid work item type."));
                }

                var title = TrimOrNull(proposed.Title);
                if (title is null)
                {
                    validationErrors.Add(($"proposedWorkItems[{index}].title", "Work item title is required."));
                }
                else if (title.Length > 150)
                {
                    validationErrors.Add(($"proposedWorkItems[{index}].title", "Work item title must be 150 characters or fewer."));
                }

                var description = TrimOrNull(proposed.Description);
                if (description is { Length: > 1000 })
                {
                    validationErrors.Add(($"proposedWorkItems[{index}].description", "Work item description must be 1000 characters or fewer."));
                }

                if (proposed.Quantity <= 0m)
                {
                    validationErrors.Add(($"proposedWorkItems[{index}].quantity", "Quantity must be greater than zero."));
                }

                if (proposed.UnitPrice < 0m)
                {
                    validationErrors.Add(($"proposedWorkItems[{index}].unitPrice", "Unit price cannot be negative."));
                }

                if (title is not null)
                {
                    validatedItems.Add((itemType, title, description, proposed.Quantity, proposed.UnitPrice));
                }

                index++;
            }
        }

        if (request.EstimatedAmount.HasValue && request.EstimatedAmount.Value < 0m)
        {
            validationErrors.Add(("estimatedAmount", "Estimated amount cannot be negative."));
        }

        if (validationErrors.Count > 0) return Validation(validationErrors);

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();
        if (ticket.Status.IsFinal || ticket.Status.IsCancelled)
        {
            return Results.BadRequest("Approvals cannot be requested on final or cancelled tickets.");
        }

        var effectiveEstimatedAmount = request.EstimatedAmount;
        if ((!effectiveEstimatedAmount.HasValue || effectiveEstimatedAmount.Value <= 0m) && validatedItems.Count > 0)
        {
            effectiveEstimatedAmount = TicketBillingCalculator.RoundMoney(
                validatedItems.Sum(item => TicketBillingCalculator.ComputeLineTotal(item.Quantity, item.UnitPrice)));
        }

        if (await IsSettingEnabledAsync(context, shopId, ShopProfileSettingKeys.RequireApprovalEstimate, cancellationToken) &&
            (!effectiveEstimatedAmount.HasValue || effectiveEstimatedAmount.Value <= 0m))
        {
            return Results.BadRequest("Estimated amount is required for approval requests in this shop.");
        }

        var actorUserId = GetUserId(principal);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var approval = new ApprovalRequest
        {
            ShopId = shopId,
            ServiceTicketId = id,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            EstimatedAmount = effectiveEstimatedAmount.HasValue ? TicketBillingCalculator.RoundMoney(effectiveEstimatedAmount.Value) : null,
            RequestedByUserId = actorUserId
        };
        context.ApprovalRequests.Add(approval);
        await context.SaveChangesAsync(cancellationToken);

        if (validatedItems.Count > 0)
        {
            foreach (var proposed in validatedItems)
            {
                context.ServiceTicketWorkItems.Add(new ServiceTicketWorkItem
                {
                    ShopId = shopId,
                    ServiceTicketId = id,
                    ApprovalRequestId = approval.Id,
                    CreatedByUserId = actorUserId,
                    ItemType = proposed.ItemType,
                    Title = proposed.Title,
                    Description = proposed.Description,
                    Quantity = proposed.Quantity,
                    UnitPrice = TicketBillingCalculator.RoundMoney(proposed.UnitPrice),
                    Status = WorkItemStatus.PendingApproval
                });
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        var historyNote = validatedItems.Count > 0
            ? $"{approval.Title} ({validatedItems.Count} proposed work item(s))"
            : approval.Title;
        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.ApprovalRequested, note: historyNote, cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, id, "TicketApprovalRequested", $"Approval requested: {approval.Title}", cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/v1/service-tickets/{id}/approvals/{approval.Id}", new { approval.Id });
    }

    private static Task<IResult> ApproveApprovalAsync(int id, int approvalId, [FromBody] ResolveApprovalRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, ClaimsPrincipal principal, CancellationToken cancellationToken)
        => ResolveApprovalAsync(id, approvalId, ApprovalStatus.Approved, request, context, shopContext, activityLogger, principal, cancellationToken);

    private static Task<IResult> RejectApprovalAsync(int id, int approvalId, [FromBody] ResolveApprovalRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, ClaimsPrincipal principal, CancellationToken cancellationToken)
        => ResolveApprovalAsync(id, approvalId, ApprovalStatus.Rejected, request, context, shopContext, activityLogger, principal, cancellationToken);

    private static async Task<IResult> ResolveApprovalAsync(int id, int approvalId, ApprovalStatus status, ResolveApprovalRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();
        if (ticket.Status.IsFinal || ticket.Status.IsCancelled)
        {
            return Results.BadRequest("Approvals cannot be resolved on final or cancelled tickets.");
        }

        var approval = await context.ApprovalRequests.FirstOrDefaultAsync(item => item.Id == approvalId && item.ServiceTicketId == id && item.ShopId == shopId, cancellationToken);
        if (approval is null) return Results.NotFound();
        if (approval.Status != ApprovalStatus.Pending) return Results.BadRequest("Approval has already been resolved.");

        var actorUserId = GetUserId(principal);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        approval.Status = status;
        approval.ResolvedAt = DateTimeOffset.UtcNow;
        approval.ResolvedByUserId = actorUserId;
        approval.ResolutionNote = TrimOrNull(request.Note);

        var linkedPendingItems = await context.ServiceTicketWorkItems
            .Where(item =>
                item.ShopId == shopId &&
                item.ServiceTicketId == id &&
                item.ApprovalRequestId == approval.Id &&
                item.Status == WorkItemStatus.PendingApproval)
            .ToListAsync(cancellationToken);

        foreach (var workItem in linkedPendingItems)
        {
            workItem.Status = status == ApprovalStatus.Approved
                ? WorkItemStatus.Approved
                : WorkItemStatus.Rejected;
        }

        await context.SaveChangesAsync(cancellationToken);

        if (status == ApprovalStatus.Approved && linkedPendingItems.Count > 0)
        {
            await TicketBillingService.RecalculateTicketBillingAsync(
                context,
                shopId,
                ticket,
                actorUserId,
                $"Approval approved: {approval.Title}",
                forceFormulaWhenZero: false,
                cancellationToken: cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.ApprovalResolved, note: $"{approval.Title}: {status}", cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, id, "TicketApprovalResolved", $"Approval {status}: {approval.Title}", cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetWorkItemsAsync(int id, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (!await TicketExistsAsync(context, shopId, id, cancellationToken)) return Results.NotFound();

        var items = await context.ServiceTicketWorkItems
            .AsNoTracking()
            .Where(item => item.ShopId == shopId && item.ServiceTicketId == id)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return Results.Ok(items.Select(ToWorkItemDto).ToList());
    }

    private static async Task<IResult> CreateWorkItemAsync(
        int id,
        [FromBody] UpsertWorkItemRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateWorkItemRequest(request, out var itemType, out var title, out var description);
        if (validationErrors.Count > 0) return Validation(validationErrors);

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();
        if (ticket.Status.IsFinal || ticket.Status.IsCancelled)
        {
            return Results.BadRequest("Work items cannot be modified on final or cancelled tickets.");
        }

        var targetStatus = WorkItemStatus.Approved;
        if (request.ApprovalRequestId.HasValue)
        {
            var approval = await context.ApprovalRequests.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.ApprovalRequestId.Value && item.ServiceTicketId == id && item.ShopId == shopId, cancellationToken);
            if (approval is null)
            {
                return Validation(("approvalRequestId", "Approval request was not found."));
            }

            if (approval.Status is ApprovalStatus.Rejected or ApprovalStatus.Cancelled)
            {
                return Results.BadRequest("Cannot link a work item to a rejected or cancelled approval request.");
            }

            if (approval.Status == ApprovalStatus.Pending)
            {
                targetStatus = WorkItemStatus.PendingApproval;
            }
        }

        var actorUserId = GetUserId(principal);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var workItem = new ServiceTicketWorkItem
        {
            ShopId = shopId,
            ServiceTicketId = id,
            ApprovalRequestId = request.ApprovalRequestId,
            CreatedByUserId = actorUserId,
            ItemType = itemType,
            Title = title!,
            Description = description,
            Quantity = request.Quantity,
            UnitPrice = TicketBillingCalculator.RoundMoney(request.UnitPrice),
            Status = targetStatus,
            IsCompleted = request.IsCompleted
        };

        context.ServiceTicketWorkItems.Add(workItem);
        await context.SaveChangesAsync(cancellationToken);

        if (targetStatus == WorkItemStatus.Approved)
        {
            await TicketBillingService.RecalculateTicketBillingAsync(
                context,
                shopId,
                ticket,
                actorUserId,
                $"Work item added: {workItem.Title}",
                forceFormulaWhenZero: true,
                cancellationToken: cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        await AddHistoryAsync(
            context,
            principal,
            shopId,
            id,
            TicketHistoryEventType.Updated,
            note: $"Work item added: {workItem.Title} ({workItem.Quantity:0.##} x {workItem.UnitPrice:0.##})",
            cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, id, "TicketWorkItemCreated", $"Work item added: {workItem.Title}", cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/v1/service-tickets/{id}/work-items/{workItem.Id}", ToWorkItemDto(workItem));
    }

    private static async Task<IResult> UpdateWorkItemAsync(
        int id,
        int workItemId,
        [FromBody] UpsertWorkItemRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateWorkItemRequest(request, out var itemType, out var title, out var description);
        if (validationErrors.Count > 0) return Validation(validationErrors);

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();
        if (ticket.Status.IsFinal || ticket.Status.IsCancelled)
        {
            return Results.BadRequest("Work items cannot be modified on final or cancelled tickets.");
        }

        var workItem = await context.ServiceTicketWorkItems
            .FirstOrDefaultAsync(item => item.Id == workItemId && item.ServiceTicketId == id && item.ShopId == shopId, cancellationToken);
        if (workItem is null) return Results.NotFound();
        if (workItem.Status is WorkItemStatus.Rejected or WorkItemStatus.Cancelled)
        {
            return Results.BadRequest("Rejected or cancelled work items cannot be edited.");
        }

        var actorUserId = GetUserId(principal);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        workItem.ItemType = itemType;
        workItem.Title = title!;
        workItem.Description = description;
        workItem.Quantity = request.Quantity;
        workItem.UnitPrice = TicketBillingCalculator.RoundMoney(request.UnitPrice);
        workItem.IsCompleted = request.IsCompleted;
        await context.SaveChangesAsync(cancellationToken);

        if (workItem.Status == WorkItemStatus.Approved)
        {
            await TicketBillingService.RecalculateTicketBillingAsync(
                context,
                shopId,
                ticket,
                actorUserId,
                $"Work item updated: {workItem.Title}",
                forceFormulaWhenZero: true,
                cancellationToken: cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        await AddHistoryAsync(
            context,
            principal,
            shopId,
            id,
            TicketHistoryEventType.Updated,
            note: $"Work item updated: {workItem.Title} ({workItem.Quantity:0.##} x {workItem.UnitPrice:0.##})",
            cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, id, "TicketWorkItemUpdated", $"Work item updated: {workItem.Title}", cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(ToWorkItemDto(workItem));
    }

    private static async Task<IResult> DeleteWorkItemAsync(
        int id,
        int workItemId,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();
        if (ticket.Status.IsFinal || ticket.Status.IsCancelled)
        {
            return Results.BadRequest("Work items cannot be modified on final or cancelled tickets.");
        }

        var workItem = await context.ServiceTicketWorkItems
            .FirstOrDefaultAsync(item => item.Id == workItemId && item.ServiceTicketId == id && item.ShopId == shopId, cancellationToken);
        if (workItem is null) return Results.NotFound();

        var actorUserId = GetUserId(principal);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var wasApproved = workItem.Status == WorkItemStatus.Approved;
        var removedTitle = workItem.Title;
        if (workItem.ApprovalRequestId.HasValue)
        {
            workItem.Status = WorkItemStatus.Cancelled;
        }
        else
        {
            context.ServiceTicketWorkItems.Remove(workItem);
        }

        await context.SaveChangesAsync(cancellationToken);

        if (wasApproved)
        {
            await TicketBillingService.RecalculateTicketBillingAsync(
                context,
                shopId,
                ticket,
                actorUserId,
                $"Work item removed: {removedTitle}",
                forceFormulaWhenZero: true,
                cancellationToken: cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.Updated, note: $"Work item removed: {removedTitle}", cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, id, "TicketWorkItemDeleted", $"Work item removed: {removedTitle}", cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetPaymentAsync(int id, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();

        return Results.Ok(await BuildPaymentDtoAsync(context, shopId, ticket, cancellationToken));
    }

    private static async Task<IResult> RecordPaymentTransactionAsync(
        int id,
        [FromBody] RecordPaymentTransactionRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var validationErrors = new List<(string Field, string Message)>();
        var entryType = PaymentEntryType.Payment;
        if (!string.IsNullOrWhiteSpace(request.EntryType))
        {
            if (!TryParseDefined(request.EntryType, out entryType) || entryType == PaymentEntryType.BillingUpdate)
            {
                validationErrors.Add(("entryType", "Entry type must be Payment or Refund."));
            }
        }

        if (request.Amount <= 0m) validationErrors.Add(("amount", "Transaction amount must be greater than zero."));
        var method = TrimOrNull(request.Method);
        if (method is { Length: > 80 }) validationErrors.Add(("method", "Payment method must be 80 characters or fewer."));
        var referenceNumber = TrimOrNull(request.ReferenceNumber);
        if (referenceNumber is { Length: > 120 }) validationErrors.Add(("referenceNumber", "Reference number must be 120 characters or fewer."));
        var notes = TrimOrNull(request.Notes);
        if (notes is { Length: > 1000 }) validationErrors.Add(("notes", "Notes must be 1000 characters or fewer."));
        if (request.RecordedAt.HasValue && request.RecordedAt.Value > DateTimeOffset.UtcNow.AddDays(1))
        {
            validationErrors.Add(("recordedAt", "Recorded date cannot be in the future."));
        }

        if (validationErrors.Count > 0) return Validation(validationErrors);

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();

        if (ticket.Status.IsCancelled && entryType == PaymentEntryType.Payment)
        {
            return Results.BadRequest("Cannot record new payments on a cancelled ticket.");
        }

        var actorUserId = GetUserId(principal);
        var amount = TicketBillingCalculator.RoundMoney(request.Amount);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var before = await TicketBillingService.GetBillingSnapshotAsync(context, shopId, ticket, cancellationToken);
        if (entryType == PaymentEntryType.Payment)
        {
            if (before.TotalAmount.HasValue && TicketBillingCalculator.RoundMoney(before.AmountPaid + amount) > before.TotalAmount.Value)
            {
                return Validation(("amount", "Total payments cannot exceed the ticket total amount."));
            }

            if (ticket.PaymentStatus == PaymentStatus.Waived)
            {
                ticket.PaymentStatus = PaymentStatus.Unpaid;
            }
        }
        else if (amount > before.AmountPaid)
        {
            return Validation(("amount", "Refund amount cannot exceed total amount paid."));
        }

        var now = DateTimeOffset.UtcNow;
        var record = new PaymentRecord
        {
            ShopId = shopId,
            ServiceTicketId = id,
            Status = ticket.PaymentStatus,
            EntryType = entryType,
            TransactionAmount = amount,
            AmountPaid = before.AmountPaid,
            TotalAmount = before.TotalAmount,
            Method = method,
            ReferenceNumber = referenceNumber,
            Notes = notes,
            UpdatedByUserId = actorUserId,
            RecordedAt = request.RecordedAt ?? now,
            CreatedAt = now
        };
        context.PaymentRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken);

        var after = await TicketBillingService.RecalculateTicketBillingAsync(
            context,
            shopId,
            ticket,
            actorUserId,
            forceFormulaWhenZero: false,
            logStatusTransitionHistory: false,
            cancellationToken: cancellationToken);

        record.Status = after.PaymentStatus;
        record.AmountPaid = after.AmountPaid;
        record.TotalAmount = after.TotalAmount;
        await context.SaveChangesAsync(cancellationToken);

        var totalDisplay = after.TotalAmount?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
        var paidDisplay = after.AmountPaid.ToString("0.##", CultureInfo.InvariantCulture);
        var amountDisplay = amount.ToString("0.##", CultureInfo.InvariantCulture);
        var historyNote = $"{entryType} of {amountDisplay} ({method ?? "-"}): status {after.PaymentStatus}, paid {paidDisplay} / {totalDisplay}";
        var actionType = entryType == PaymentEntryType.Payment ? "TicketPaymentRecorded" : "TicketRefundRecorded";

        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.PaymentUpdated, reason: notes, note: historyNote, cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, id, actionType, $"{entryType} recorded: {amountDisplay} ({after.PaymentStatus})", notes, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(await BuildPaymentDtoAsync(context, shopId, ticket, cancellationToken));
    }

    private static async Task<IResult> VoidPaymentTransactionAsync(
        int id,
        int transactionId,
        [FromBody] VoidPaymentTransactionRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var reason = TrimOrNull(request.Reason);
        if (reason is null) return Validation(("reason", "Void reason is required."));
        if (reason.Length > 500) return Validation(("reason", "Void reason must be 500 characters or fewer."));

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();

        var record = await context.PaymentRecords
            .FirstOrDefaultAsync(item => item.Id == transactionId && item.ServiceTicketId == id && item.ShopId == shopId, cancellationToken);
        if (record is null) return Results.NotFound();
        if (record.IsVoided) return Results.BadRequest("Payment transaction has already been voided.");
        if (record.EntryType == PaymentEntryType.BillingUpdate)
        {
            return Results.BadRequest("Billing adjustment log entries cannot be voided.");
        }

        var actorUserId = GetUserId(principal);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var before = await TicketBillingService.GetBillingSnapshotAsync(context, shopId, ticket, cancellationToken);
        var deltaOnVoid = record.EntryType == PaymentEntryType.Payment ? -record.TransactionAmount : record.TransactionAmount;
        if (TicketBillingCalculator.RoundMoney(before.AmountPaid + deltaOnVoid) < 0m)
        {
            return Results.BadRequest("Cannot void this payment because subsequent refunds depend on its balance.");
        }

        record.IsVoided = true;
        record.VoidedAt = DateTimeOffset.UtcNow;
        record.VoidedByUserId = actorUserId;
        record.VoidReason = reason;
        await context.SaveChangesAsync(cancellationToken);

        var after = await TicketBillingService.RecalculateTicketBillingAsync(
            context,
            shopId,
            ticket,
            actorUserId,
            forceFormulaWhenZero: false,
            logStatusTransitionHistory: false,
            cancellationToken: cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var amountDisplay = record.TransactionAmount.ToString("0.##", CultureInfo.InvariantCulture);
        var paidDisplay = after.AmountPaid.ToString("0.##", CultureInfo.InvariantCulture);
        await AddHistoryAsync(
            context,
            principal,
            shopId,
            id,
            TicketHistoryEventType.PaymentUpdated,
            reason: reason,
            note: $"Voided {record.EntryType} of {amountDisplay}: status {after.PaymentStatus}, paid {paidDisplay}",
            cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, id, "TicketPaymentVoided", $"Payment transaction voided: {amountDisplay}", reason, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(await BuildPaymentDtoAsync(context, shopId, ticket, cancellationToken));
    }

    private static async Task<IResult> UpdateBillingAsync(
        int id,
        [FromBody] UpdateBillingRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var validationErrors = new List<(string Field, string Message)>();
        if (request.DiscountAmount < 0m) validationErrors.Add(("discountAmount", "Discount amount cannot be negative."));
        if (request.ManualTotalAmount.HasValue && request.ManualTotalAmount.Value < 0m)
        {
            validationErrors.Add(("manualTotalAmount", "Manual total amount cannot be negative."));
        }
        if (request.ManualTotalAmount.HasValue && request.ClearManualOverride)
        {
            validationErrors.Add(("manualTotalAmount", "Cannot set manual total and clear manual override at the same time."));
        }
        var notes = TrimOrNull(request.Notes);
        if (notes is { Length: > 1000 }) validationErrors.Add(("notes", "Notes must be 1000 characters or fewer."));
        if (validationErrors.Count > 0) return Validation(validationErrors);

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();
        if (ticket.Status.IsCancelled) return Results.BadRequest("Cannot update billing on a cancelled ticket.");

        var actorUserId = GetUserId(principal);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var beforeDiscount = ticket.DiscountAmount;
        var beforeTotal = ticket.TotalAmount;
        var beforeStatus = ticket.PaymentStatus;
        var beforeOverride = ticket.IsTotalManuallyOverridden;

        ticket.DiscountAmount = TicketBillingCalculator.RoundMoney(request.DiscountAmount);
        if (request.ClearManualOverride)
        {
            ticket.IsTotalManuallyOverridden = false;
            ticket.TotalAmount = null;
        }
        else if (request.ManualTotalAmount.HasValue)
        {
            ticket.IsTotalManuallyOverridden = true;
            ticket.TotalAmount = TicketBillingCalculator.RoundMoney(request.ManualTotalAmount.Value);
        }

        if (request.IsWaived)
        {
            ticket.PaymentStatus = PaymentStatus.Waived;
        }
        else if (ticket.PaymentStatus == PaymentStatus.Waived)
        {
            ticket.PaymentStatus = PaymentStatus.Unpaid;
        }

        var after = await TicketBillingService.RecalculateTicketBillingAsync(
            context,
            shopId,
            ticket,
            actorUserId,
            forceFormulaWhenZero: request.ClearManualOverride,
            logStatusTransitionHistory: false,
            cancellationToken: cancellationToken);

        var changed = beforeDiscount != ticket.DiscountAmount ||
                      beforeTotal != ticket.TotalAmount ||
                      beforeStatus != ticket.PaymentStatus ||
                      beforeOverride != ticket.IsTotalManuallyOverridden ||
                      notes is not null;

        if (changed)
        {
            var now = DateTimeOffset.UtcNow;
            context.PaymentRecords.Add(new PaymentRecord
            {
                ShopId = shopId,
                ServiceTicketId = id,
                Status = after.PaymentStatus,
                EntryType = PaymentEntryType.BillingUpdate,
                TransactionAmount = 0m,
                AmountPaid = after.AmountPaid,
                TotalAmount = after.TotalAmount,
                Notes = notes ?? "Billing settings updated.",
                UpdatedByUserId = actorUserId,
                RecordedAt = now,
                CreatedAt = now
            });
            await context.SaveChangesAsync(cancellationToken);

            var totalDisplay = after.TotalAmount?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
            var discountDisplay = after.DiscountAmount.ToString("0.##", CultureInfo.InvariantCulture);
            await AddHistoryAsync(
                context,
                principal,
                shopId,
                id,
                TicketHistoryEventType.PaymentUpdated,
                reason: notes,
                note: $"Billing updated: total {totalDisplay}, discount {discountDisplay}, status {after.PaymentStatus}",
                cancellationToken: cancellationToken);
            await activityLogger.LogAsync(EntityType, id, "TicketBillingUpdated", $"Billing updated: {after.PaymentStatus}", notes, cancellationToken: cancellationToken);
        }
        else
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(await BuildPaymentDtoAsync(context, shopId, ticket, cancellationToken));
    }

    private static async Task<IResult> UpdatePaymentAsync(int id, [FromBody] UpdatePaymentRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var validationErrors = new List<(string Field, string Message)>();
        if (!TryParseDefined(request.Status, out PaymentStatus requestedStatus)) validationErrors.Add(("status", "Invalid payment status."));
        if (request.AmountPaid < 0) validationErrors.Add(("amountPaid", "Amount paid cannot be negative."));
        if (request.TotalAmount < 0) validationErrors.Add(("totalAmount", "Total amount cannot be negative."));
        if (request.TotalAmount.HasValue && request.AmountPaid > request.TotalAmount.Value) validationErrors.Add(("amountPaid", "Amount paid cannot exceed total amount."));
        if (validationErrors.Count > 0) return Validation(validationErrors);

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets.FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();

        var actorUserId = GetUserId(principal);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var before = await TicketBillingService.GetBillingSnapshotAsync(context, shopId, ticket, cancellationToken);
        if (request.TotalAmount.HasValue)
        {
            var roundedTotal = TicketBillingCalculator.RoundMoney(request.TotalAmount.Value);
            if (before.CalculatedTotalAmount > 0m && roundedTotal != before.CalculatedTotalAmount)
            {
                ticket.IsTotalManuallyOverridden = true;
            }
            ticket.TotalAmount = roundedTotal;
        }

        if (requestedStatus == PaymentStatus.Waived)
        {
            ticket.PaymentStatus = PaymentStatus.Waived;
        }
        else if (ticket.PaymentStatus == PaymentStatus.Waived)
        {
            ticket.PaymentStatus = PaymentStatus.Unpaid;
        }

        var targetPaid = TicketBillingCalculator.RoundMoney(request.AmountPaid);
        var delta = TicketBillingCalculator.RoundMoney(targetPaid - before.AmountPaid);
        var entryType = delta > 0m
            ? PaymentEntryType.Payment
            : (delta < 0m ? PaymentEntryType.Refund : PaymentEntryType.BillingUpdate);
        var transactionAmount = Math.Abs(delta);
        var now = DateTimeOffset.UtcNow;

        var record = new PaymentRecord
        {
            ShopId = shopId,
            ServiceTicketId = id,
            Status = requestedStatus,
            EntryType = entryType,
            TransactionAmount = transactionAmount,
            AmountPaid = targetPaid,
            TotalAmount = ticket.TotalAmount,
            Method = TrimOrNull(request.Method),
            ReferenceNumber = TrimOrNull(request.ReferenceNumber),
            Notes = TrimOrNull(request.Notes),
            UpdatedByUserId = actorUserId,
            RecordedAt = now,
            CreatedAt = now
        };
        context.PaymentRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken);

        var after = await TicketBillingService.RecalculateTicketBillingAsync(
            context,
            shopId,
            ticket,
            actorUserId,
            forceFormulaWhenZero: false,
            logStatusTransitionHistory: false,
            cancellationToken: cancellationToken);

        record.Status = after.PaymentStatus;
        record.AmountPaid = after.AmountPaid;
        record.TotalAmount = after.TotalAmount;
        await context.SaveChangesAsync(cancellationToken);

        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.PaymentUpdated, note: $"Payment status: {after.PaymentStatus}", cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, id, "TicketPaymentUpdated", $"Payment updated: {after.PaymentStatus}", cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> PickupOverrideAsync(int id, [FromBody] PickupOverrideRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest("Override reason is required.");
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (!await IsSettingEnabledAsync(context, shopId, ShopProfileSettingKeys.AllowUnpaidPickupOverride, cancellationToken))
        {
            return Results.BadRequest("Unpaid pickup override is disabled for this shop.");
        }
        var ticket = await context.ServiceTickets.FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();

        ticket.PickupOverrideReason = request.Reason.Trim();
        await context.SaveChangesAsync(cancellationToken);
        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.PickupOverride, reason: request.Reason.Trim(), cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, id, "TicketPickupOverride", "Pickup payment block overridden.", request.Reason.Trim(), cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetChecklistAsync(int id, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (!await TicketExistsAsync(context, shopId, id, cancellationToken)) return Results.NotFound();
        return Results.Ok(await context.ServiceTicketChecklistItems.AsNoTracking()
            .Where(item => item.ShopId == shopId && item.ServiceTicketId == id)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .Select(item => new ChecklistItemDto(item.Id, item.Title, item.Description, item.SortOrder, item.IsCompleted, item.CompletedAt))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ApplyTemplateAsync(int id, [FromBody] ApplyTemplateRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (!await TicketExistsAsync(context, shopId, id, cancellationToken)) return Results.NotFound();
        var template = await context.ServiceTemplates
            .AsNoTracking()
            .Include(item => item.ChecklistItems)
            .FirstOrDefaultAsync(item => item.Id == request.TemplateId && item.ShopId == shopId && item.IsActive, cancellationToken);
        if (template is null) return Results.BadRequest("Template was not found.");
        if (template.ChecklistItems.Count == 0) return Results.BadRequest("Template has no checklist items.");

        await context.ServiceTicketChecklistItems
            .Where(item => item.ShopId == shopId && item.ServiceTicketId == id)
            .ExecuteDeleteAsync(cancellationToken);
        context.ServiceTicketChecklistItems.AddRange(template.ChecklistItems
            .OrderBy(item => item.SortOrder)
            .Select(item => new ServiceTicketChecklistItem
            {
                ShopId = shopId,
                ServiceTicketId = id,
                ServiceTemplateId = template.Id,
                Title = item.Title,
                Description = item.Description,
                SortOrder = item.SortOrder
            }));
        await context.SaveChangesAsync(cancellationToken);
        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.Updated, note: $"Template applied: {template.Name}", cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, id, "TicketTemplateApplied", $"Template applied: {template.Name}", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateChecklistItemAsync(int id, int checklistItemId, [FromBody] UpdateChecklistItemRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var item = await context.ServiceTicketChecklistItems
            .FirstOrDefaultAsync(item => item.Id == checklistItemId && item.ServiceTicketId == id && item.ShopId == shopId, cancellationToken);
        if (item is null) return Results.NotFound();

        item.IsCompleted = request.IsCompleted;
        item.CompletedAt = request.IsCompleted ? DateTimeOffset.UtcNow : null;
        await context.SaveChangesAsync(cancellationToken);
        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.Updated, note: $"Checklist updated: {item.Title}", cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, id, "TicketChecklistUpdated", $"Checklist updated: {item.Title}", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteTicketAsync(int id, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticket = await context.ServiceTickets.FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();

        await InventoryEndpoints.ReleaseOpenReservationsForTicketAsync(context, activityLogger, shopId, ticket.Id, cancellationToken);
        ticket.DeletedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await AddHistoryAsync(context, principal, shopId, id, TicketHistoryEventType.Updated, note: "Ticket deleted.", cancellationToken: cancellationToken);
        await activityLogger.LogAsync(EntityType, id, "TicketDeleted", $"Ticket deleted: {ticket.TicketNumber}", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetPublicTrackingAsync(string token, AppDbContext context, CancellationToken cancellationToken)
    {
        var ticket = await context.ServiceTickets.AsNoTracking()
            .Where(item => item.PublicTrackingEnabled && item.PublicTrackingToken == token && item.DeletedAt == null)
            .Select(item => new
            {
                item.Id,
                item.ShopId,
                item.DeviceId,
                ShopName = item.Shop.Name,
                ShopLogoUrl = item.Shop.LogoUrl,
                item.Shop.IndustryBlueprintKey,
                item.Shop.TerminologyConfig,
                OperatingMode = item.Shop.OperatingMode.ToString(),
                item.TicketNumber,
                item.DeviceSnapshot,
                ServiceLocationType = item.ServiceLocationType.ToString(),
                item.SiteAddressSnapshot,
                item.ScheduledVisitStart,
                Status = item.Status.Name,
                item.Status.Color,
                History = context.ServiceTicketHistories
                    .Where(history => history.ServiceTicketId == item.Id && history.IsPublicVisible)
                    .OrderBy(history => history.CreatedAt)
                    .Select(history => new HistoryDto(history.Id, history.EventType.ToString(), history.FromStatus == null ? null : history.FromStatus.Name, history.ToStatus == null ? null : history.ToStatus.Name, null, history.Note, true, history.CreatedAt))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (ticket is null) return Results.NotFound();

        var showPhotos = await IsSettingEnabledAsync(context, ticket.ShopId, ShopProfileSettingKeys.ShowPhotoEvidenceOnPublicTracking, cancellationToken);

        var publicPhotoCount = showPhotos
            ? await context.ServiceTicketAttachments.AsNoTracking()
                .CountAsync(attachment => attachment.ShopId == ticket.ShopId && attachment.ServiceTicketId == ticket.Id && attachment.IsPublicVisible, cancellationToken)
            : 0;

        var publicAssetFields = await context.AssetFieldValues.AsNoTracking()
            .Where(v => v.ShopId == ticket.ShopId && v.ServiceAssetId == ticket.DeviceId && v.FieldDefinition.ShowOnPublicTracking && v.Value != null)
            .OrderBy(v => v.FieldDefinition.SortOrder)
            .Select(v => new BlueprintEndpoints.FieldValueDto(
                v.FieldDefinitionId, v.FieldDefinition.FieldKey, v.FieldDefinition.LabelEn, v.FieldDefinition.LabelId,
                v.FieldDefinition.FieldType.ToString(), v.Value, v.FieldDefinition.ShowOnPrint, true))
            .ToListAsync(cancellationToken);

        var publicTicketFields = await context.TicketFieldValues.AsNoTracking()
            .Where(v => v.ShopId == ticket.ShopId && v.ServiceTicketId == ticket.Id && v.FieldDefinition.ShowOnPublicTracking && v.Value != null)
            .OrderBy(v => v.FieldDefinition.SortOrder)
            .Select(v => new BlueprintEndpoints.FieldValueDto(
                v.FieldDefinitionId, v.FieldDefinition.FieldKey, v.FieldDefinition.LabelEn, v.FieldDefinition.LabelId,
                v.FieldDefinition.FieldType.ToString(), v.Value, v.FieldDefinition.ShowOnPrint, true))
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            ticket.ShopName,
            ticket.ShopLogoUrl,
            ticket.OperatingMode,
            Terminology = IndustryBlueprintCatalog.DeserializeTerminology(ticket.TerminologyConfig, ticket.IndustryBlueprintKey),
            ticket.TicketNumber,
            ticket.DeviceSnapshot,
            ticket.ServiceLocationType,
            ticket.SiteAddressSnapshot,
            ticket.ScheduledVisitStart,
            ticket.Status,
            ticket.Color,
            ticket.History,
            PublicFields = publicAssetFields.Concat(publicTicketFields).ToList(),
            PublicPhotoCount = publicPhotoCount,
            Attachments = Array.Empty<object>()
        });
    }

    private static async Task<IResult> VerifyPublicTrackingPhotosAsync(
        string token,
        [FromBody] VerifyPublicPhotosRequest request,
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var inputDigits = new string((request.PhoneLastFour ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (inputDigits.Length != 4)
        {
            return Results.BadRequest("Enter the last 4 digits of the registered phone number.");
        }

        var ticket = await context.ServiceTickets.AsNoTracking()
            .Where(item => item.PublicTrackingEnabled && item.PublicTrackingToken == token && item.DeletedAt == null)
            .Select(item => new
            {
                item.Id,
                item.ShopId,
                item.CustomerPhoneSnapshot,
                CustomerPhone = item.Customer.PhoneNumber
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (ticket is null) return Results.NotFound();

        var showPhotos = await IsSettingEnabledAsync(context, ticket.ShopId, ShopProfileSettingKeys.ShowPhotoEvidenceOnPublicTracking, cancellationToken);
        if (!showPhotos) return Results.NotFound();

        var phoneRaw = !string.IsNullOrWhiteSpace(ticket.CustomerPhoneSnapshot) ? ticket.CustomerPhoneSnapshot : ticket.CustomerPhone;
        var phoneDigits = new string((phoneRaw ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (phoneDigits.Length < 4)
        {
            return Results.BadRequest("Phone number verification failed.");
        }

        var expectedLastFour = phoneDigits[^4..];
        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expectedLastFour);
        var actualBytes = System.Text.Encoding.UTF8.GetBytes(inputDigits);
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            return Results.BadRequest("Phone number digits did not match.");
        }

        var attachments = await context.ServiceTicketAttachments.AsNoTracking()
            .Where(attachment => attachment.ShopId == ticket.ShopId && attachment.ServiceTicketId == ticket.Id && attachment.IsPublicVisible)
            .OrderBy(attachment => attachment.CreatedAt)
            .Select(attachment => new
            {
                attachment.FileName,
                attachment.MimeType,
                attachment.AttachmentType,
                Url = $"/uploads/ticket-attachments/{attachment.StoredFileName}",
                attachment.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            Attachments = attachments
        });
    }

    private static async Task<IReadOnlyCollection<TransitionDto>> GetAllowedTransitionsAsync(AppDbContext context, int shopId, int fromStatusId, CancellationToken cancellationToken)
    {
        var rules = await context.StatusTransitionRules.AsNoTracking()
            .Where(rule => rule.ShopId == shopId && rule.FromStatusId == fromStatusId && rule.IsActive)
            .OrderBy(rule => rule.ToStatus.SortOrder)
            .Select(rule => new TransitionDto(rule.ToStatusId, rule.ToStatus.Name, rule.ToStatus.Color, rule.RequiresReason, rule.RequiresApproval, rule.RequiresPaymentCompleted))
            .ToListAsync(cancellationToken);

        if (rules.Count > 0) return rules;

        var fromStatus = await context.ServiceStatuses.AsNoTracking()
            .Where(status => status.ShopId == shopId && status.Id == fromStatusId)
            .Select(status => new { status.IsFinal, status.IsCancelled })
            .FirstOrDefaultAsync(cancellationToken);

        if (fromStatus is null || fromStatus.IsFinal || fromStatus.IsCancelled)
        {
            return Array.Empty<TransitionDto>();
        }

        return await context.ServiceStatuses.AsNoTracking()
            .Where(status => status.ShopId == shopId && status.Id != fromStatusId)
            .OrderBy(status => status.SortOrder)
            .Select(status => new TransitionDto(
                status.Id,
                status.Name,
                status.Color,
                status.IsCancelled || status.IsBlocked,
                false,
                status.IsFinal && !status.IsCancelled))
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<ServiceTicket> ApplyKanbanFilters(
        IQueryable<ServiceTicket> query,
        int? technicianUserId,
        string? priority,
        string? paymentStatus,
        string? search,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (technicianUserId.HasValue)
        {
            query = query.Where(ticket => ticket.AssignedTechnicianUserId == technicianUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<ServiceTicketPriority>(priority, true, out var parsedPriority))
        {
            query = query.Where(ticket => ticket.Priority == parsedPriority);
        }

        if (!string.IsNullOrWhiteSpace(paymentStatus) && Enum.TryParse<PaymentStatus>(paymentStatus, true, out var parsedPaymentStatus))
        {
            query = query.Where(ticket => ticket.PaymentStatus == parsedPaymentStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(ticket =>
                ticket.TicketNumber.Contains(term) ||
                ticket.CustomerNameSnapshot.Contains(term) ||
                ticket.CustomerPhoneSnapshot.Contains(term) ||
                ticket.DeviceSnapshot.Contains(term) ||
                ticket.Complaint.Contains(term));
        }

        if (from.HasValue)
        {
            query = query.Where(ticket => ticket.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(ticket => ticket.CreatedAt <= to.Value);
        }

        return query;
    }

    private static async Task<List<HistoryDto>> GetHistoryDtosAsync(AppDbContext context, int shopId, int ticketId, bool publicOnly, CancellationToken cancellationToken)
    {
        return await context.ServiceTicketHistories.AsNoTracking()
            .Where(item => item.ShopId == shopId && item.ServiceTicketId == ticketId && (!publicOnly || item.IsPublicVisible))
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new HistoryDto(
                item.Id,
                item.EventType.ToString(),
                item.FromStatus == null ? null : item.FromStatus.Name,
                item.ToStatus == null ? null : item.ToStatus.Name,
                item.Reason,
                item.Note,
                item.IsPublicVisible,
                item.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private static async Task<bool> IsSettingEnabledAsync(AppDbContext context, int shopId, string key, CancellationToken cancellationToken)
    {
        var value = await context.AppSettings.AsNoTracking()
            .Where(setting => setting.ShopId == shopId && setting.Key == key)
            .OrderByDescending(setting => setting.UpdatedAt ?? setting.CreatedAt)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken)
            ?? ShopProfileSettingKeys.Defaults.GetValueOrDefault(key, bool.FalseString);

        return bool.TryParse(value, out var enabled) && enabled;
    }

    private static async Task<List<ServiceHistoryTicketDto>> GetServiceHistoryTicketsAsync(AppDbContext context, int shopId, int customerId, int? deviceId, CancellationToken cancellationToken)
    {
        var query = context.ServiceTickets.AsNoTracking()
            .Where(ticket => ticket.ShopId == shopId && ticket.CustomerId == customerId && ticket.DeletedAt == null);

        if (deviceId.HasValue)
        {
            query = query.Where(ticket => ticket.DeviceId == deviceId.Value);
        }

        var tickets = await query
            .OrderByDescending(ticket => ticket.CreatedAt)
            .Select(ticket => new
            {
                ticket.Id,
                ticket.TicketNumber,
                ticket.DeviceSnapshot,
                Status = ticket.Status.Name,
                ticket.Status.Color,
                ticket.Priority,
                ticket.PaymentStatus,
                ticket.CreatedAt,
                ticket.WarrantyUntil
            })
            .ToListAsync(cancellationToken);

        return tickets.Select(ticket => new ServiceHistoryTicketDto(
            ticket.Id,
            ticket.TicketNumber,
            ticket.DeviceSnapshot,
            ticket.Status,
            ticket.Color,
            ticket.Priority.ToString(),
            ticket.PaymentStatus.ToString(),
            ticket.CreatedAt,
            ticket.WarrantyUntil,
            IsWarrantyActive(ticket.WarrantyUntil))).ToList();
    }

    private static async Task AddHistoryAsync(
        AppDbContext context,
        ClaimsPrincipal principal,
        int shopId,
        int ticketId,
        TicketHistoryEventType eventType,
        int? fromStatusId = null,
        int? toStatusId = null,
        string? reason = null,
        string? note = null,
        bool isPublicVisible = false,
        DateTimeOffset? createdAt = null,
        CancellationToken cancellationToken = default)
    {
        context.ServiceTicketHistories.Add(new ServiceTicketHistory
        {
            ShopId = shopId,
            ServiceTicketId = ticketId,
            ActorUserId = GetUserId(principal),
            EventType = eventType,
            FromStatusId = fromStatusId,
            ToStatusId = toStatusId,
            Reason = TrimOrNull(reason),
            Note = TrimOrNull(note),
            IsPublicVisible = isPublicVisible,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<string> GenerateTicketNumberAsync(AppDbContext context, int shopId, CancellationToken cancellationToken)
    {
        var storedSettings = await context.AppSettings.AsNoTracking()
            .Where(setting => setting.ShopId == shopId && (
                setting.Key == ShopProfileSettingKeys.TicketPrefix ||
                setting.Key == ShopProfileSettingKeys.TicketDateFormat ||
                setting.Key == ShopProfileSettingKeys.TicketStartNumber ||
                setting.Key == ShopProfileSettingKeys.TicketMinDigits))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);
        string Setting(string key) => storedSettings.GetValueOrDefault(key) ?? ShopProfileSettingKeys.Defaults[key];
        var prefix = Setting(ShopProfileSettingKeys.TicketPrefix).Trim().ToUpperInvariant();
        var dateFormat = Setting(ShopProfileSettingKeys.TicketDateFormat);
        if (dateFormat is not ("yyyyMMdd" or "yyyyMM" or "yyMMdd"))
        {
            dateFormat = "yyyyMMdd";
        }
        var startNumber = int.TryParse(Setting(ShopProfileSettingKeys.TicketStartNumber), out var configuredStart) ? configuredStart : 1;
        var minDigits = int.TryParse(Setting(ShopProfileSettingKeys.TicketMinDigits), out var configuredDigits) ? configuredDigits : 4;
        var todayPrefix = $"{prefix}-{DateTimeOffset.UtcNow.ToString(dateFormat)}-";
        var existingTicketNumbers = await context.ServiceTickets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.ShopId == shopId && item.TicketNumber.StartsWith(todayPrefix))
            .Select(item => item.TicketNumber)
            .ToListAsync(cancellationToken);

        var maxSequence = existingTicketNumbers
            .Select(ticketNumber => int.TryParse(ticketNumber[todayPrefix.Length..], out var seq) ? seq : 0)
            .DefaultIfEmpty(startNumber - 1)
            .Max();
        var nextSequence = Math.Max(startNumber, maxSequence + 1);
        return $"{todayPrefix}{nextSequence.ToString($"D{minDigits}")}";
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static Task<bool> IsActiveShopUserAsync(AppDbContext context, int shopId, int userId, CancellationToken cancellationToken)
    {
        return context.ShopUsers.AnyAsync(item =>
            item.ShopId == shopId &&
            item.UserId == userId &&
            item.Status == ShopUserStatus.Active &&
            item.User.IsActive,
            cancellationToken);
    }

    private static Task<bool> TicketExistsAsync(AppDbContext context, int shopId, int ticketId, CancellationToken cancellationToken)
        => context.ServiceTickets.AnyAsync(item => item.Id == ticketId && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);

    private static IResult Validation(params (string Field, string Message)[] errors) => Validation((IEnumerable<(string Field, string Message)>)errors);

    private static IResult Validation(IEnumerable<(string Field, string Message)> errors)
        => Results.ValidationProblem(errors
            .GroupBy(error => error.Field)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray()));

    private static string AttachmentRoot()
        => Path.Combine(Directory.GetCurrentDirectory(), "uploads", "ticket-attachments");

    private static int GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : throw new InvalidOperationException("Authenticated user id is required.");
    }

    private static async Task<PaymentDto> BuildPaymentDtoAsync(
        AppDbContext context,
        int shopId,
        ServiceTicket ticket,
        CancellationToken cancellationToken)
    {
        var billing = await TicketBillingService.GetBillingSnapshotAsync(context, shopId, ticket, cancellationToken);
        var rows = await context.PaymentRecords.AsNoTracking()
            .Where(item => item.ShopId == shopId && item.ServiceTicketId == ticket.Id)
            .OrderByDescending(item => item.RecordedAt)
            .ThenByDescending(item => item.Id)
            .Select(item => new
            {
                item.Id,
                item.EntryType,
                item.Status,
                item.TransactionAmount,
                item.AmountPaid,
                item.TotalAmount,
                item.Method,
                item.ReferenceNumber,
                item.Notes,
                UpdatedByName = (item.UpdatedByUser.FirstName + " " + item.UpdatedByUser.LastName).Trim(),
                item.RecordedAt,
                item.CreatedAt,
                item.IsVoided,
                item.VoidedAt,
                VoidedByName = item.VoidedByUser == null ? null : (item.VoidedByUser.FirstName + " " + item.VoidedByUser.LastName).Trim(),
                item.VoidReason
            })
            .ToListAsync(cancellationToken);

        var transactions = rows
            .Select(item => new PaymentTransactionDto(
                item.Id,
                item.EntryType.ToString(),
                item.Status.ToString(),
                item.TransactionAmount,
                item.AmountPaid,
                item.TotalAmount,
                item.Method,
                item.ReferenceNumber,
                item.Notes,
                string.IsNullOrWhiteSpace(item.UpdatedByName) ? null : item.UpdatedByName,
                item.RecordedAt,
                item.CreatedAt,
                item.IsVoided,
                item.VoidedAt,
                string.IsNullOrWhiteSpace(item.VoidedByName) ? null : item.VoidedByName,
                item.VoidReason))
            .ToList();

        var latestNonVoided = transactions.FirstOrDefault(item => !item.IsVoided);
        var latestActivePayment = transactions.FirstOrDefault(item =>
            !item.IsVoided &&
            (item.EntryType == nameof(PaymentEntryType.Payment) || item.EntryType == nameof(PaymentEntryType.Refund)))
            ?? latestNonVoided;

        return new PaymentDto(
            billing.PaymentStatus.ToString(),
            billing.AmountPaid,
            billing.TotalAmount,
            latestActivePayment?.Method,
            latestActivePayment?.ReferenceNumber,
            latestActivePayment?.Notes,
            latestNonVoided?.CreatedAt,
            billing.CalculatedTotalAmount,
            billing.DiscountAmount,
            billing.IsTotalManuallyOverridden,
            billing.RemainingBalance,
            billing.OverpaidAmount,
            billing.WorkItemsSubtotal,
            billing.ConsumedPartsSubtotal,
            transactions);
    }

    private static List<(string Field, string Message)> ValidateWorkItemRequest(
        UpsertWorkItemRequest request,
        out WorkItemType itemType,
        out string? title,
        out string? description)
    {
        var errors = new List<(string Field, string Message)>();
        if (!TryParseWorkItemType(request.ItemType, out itemType))
        {
            errors.Add(("itemType", "Invalid work item type."));
        }

        title = TrimOrNull(request.Title);
        if (title is null)
        {
            errors.Add(("title", "Title is required."));
        }
        else if (title.Length > 150)
        {
            errors.Add(("title", "Title must be 150 characters or fewer."));
        }

        description = TrimOrNull(request.Description);
        if (description is { Length: > 1000 })
        {
            errors.Add(("description", "Description must be 1000 characters or fewer."));
        }

        if (request.Quantity <= 0m)
        {
            errors.Add(("quantity", "Quantity must be greater than zero."));
        }

        if (request.UnitPrice < 0m)
        {
            errors.Add(("unitPrice", "Unit price cannot be negative."));
        }

        return errors;
    }

    private static bool TryParseWorkItemType(string? value, out WorkItemType itemType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            itemType = WorkItemType.Service;
            return true;
        }

        return TryParseDefined(value, out itemType);
    }

    private static WorkItemDto ToWorkItemDto(ServiceTicketWorkItem item) => new(
        item.Id,
        item.ApprovalRequestId,
        item.ItemType.ToString(),
        item.Title,
        item.Description,
        item.Quantity,
        item.UnitPrice,
        TicketBillingCalculator.ComputeLineTotal(item.Quantity, item.UnitPrice),
        item.Status.ToString(),
        item.IsCompleted,
        item.CreatedAt,
        item.UpdatedAt);

    private static bool TryParsePriority(string? value, out ServiceTicketPriority priority)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            priority = ServiceTicketPriority.Normal;
            return true;
        }

        return TryParseDefined(value, out priority);
    }

    private static bool TryParseDefined<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
        => Enum.TryParse(value, true, out result) && Enum.IsDefined(result);

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsWarrantyActive(DateTimeOffset? warrantyUntil) => warrantyUntil.HasValue && warrantyUntil.Value >= DateTimeOffset.UtcNow;
}
