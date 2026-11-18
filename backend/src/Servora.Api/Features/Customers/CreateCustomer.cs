using Servora.Api.Infrastructure.Data;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.ShopContext;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Servora.Api.Features.Customers;

public static class CreateCustomer
{
    public record Command(
        string FullName,
        string PhoneNumber,
        string? Email,
        string? Address,
        string? Notes,
        RequesterType RequesterType = RequesterType.Customer,
        string? CompanyName = null,
        string? DepartmentName = null,
        string? DepartmentCode = null,
        string? CostCenter = null,
        string? EmployeeId = null) : IRequest<Result>;

    public record Result(int Id);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Full Name is required.")
                .MaximumLength(150).WithMessage("Full Name must not exceed 150 characters.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Phone Number is required.")
                .When(x => x.RequesterType == RequesterType.Customer);

            RuleFor(x => x.PhoneNumber)
                .MaximumLength(30).WithMessage("Phone Number must not exceed 30 characters.")
                .Matches(@"^\+?[0-9\s\-()]+$").WithMessage("Phone Number format is invalid.")
                .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Email format is invalid.")
                .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
                .When(x => !string.IsNullOrEmpty(x.Email));

            RuleFor(x => x.Address)
                .MaximumLength(500).WithMessage("Address must not exceed 500 characters.");

            RuleFor(x => x.Notes)
                .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters.");
        }
    }

    internal sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;
        private readonly IActivityLogger _activityLogger;

        public Handler(AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger)
        {
            _context = context;
            _shopContext = shopContext;
            _activityLogger = activityLogger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var customer = new Customer
            {
                ShopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken),
                RequesterType = request.RequesterType,
                FullName = request.FullName.Trim(),
                PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty,
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
                CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? null : request.CompanyName.Trim(),
                DepartmentName = string.IsNullOrWhiteSpace(request.DepartmentName) ? null : request.DepartmentName.Trim(),
                DepartmentCode = string.IsNullOrWhiteSpace(request.DepartmentCode) ? null : request.DepartmentCode.Trim(),
                CostCenter = string.IsNullOrWhiteSpace(request.CostCenter) ? null : request.CostCenter.Trim(),
                EmployeeId = string.IsNullOrWhiteSpace(request.EmployeeId) ? null : request.EmployeeId.Trim(),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                IsBanned = false,
                IsStarred = false
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync(cancellationToken);
            await _activityLogger.LogAsync(
                "Customer",
                customer.Id,
                "CustomerCreated",
                $"Requester created: {customer.FullName} ({customer.RequesterType})",
                cancellationToken: cancellationToken);

            return new Result(customer.Id);
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/customers", async ([FromBody] Command command, IMediator mediator, IValidator<Command> validator, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(command, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var result = await mediator.Send(command, cancellationToken);
            return Results.Created($"/api/v1/customers/{result.Id}", result);
        })
        .WithName("CreateCustomer")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.CustomersWrite);
    }
}
