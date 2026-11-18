using Servora.Api.Infrastructure.Data;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.ShopContext;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Servora.Api.Features.Customers;

public static class UpdateCustomer
{
    public record Command(
        [property: System.Text.Json.Serialization.JsonIgnore] int Id,
        string FullName,
        string PhoneNumber,
        string? Email,
        string? Address,
        string? Notes,
        DateTimeOffset? UpdatedAt = null,
        RequesterType? RequesterType = null,
        string? CompanyName = null,
        string? DepartmentName = null,
        string? DepartmentCode = null,
        string? CostCenter = null,
        string? EmployeeId = null) : IRequest<CustomerUpdateResult>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Full Name is required.")
                .MaximumLength(150).WithMessage("Full Name must not exceed 150 characters.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Phone Number is required.")
                .When(x => (x.RequesterType ?? RequesterType.Customer) == RequesterType.Customer);

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

    internal sealed class Handler : IRequestHandler<Command, CustomerUpdateResult>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public Handler(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        public async Task<CustomerUpdateResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == request.Id && c.ShopId == shopId && c.DeletedAt == null, cancellationToken);

            if (customer is null) return CustomerUpdateResult.NotFound;
            if (request.UpdatedAt.HasValue && customer.UpdatedAt.HasValue && request.UpdatedAt.Value != customer.UpdatedAt.Value)
            {
                return CustomerUpdateResult.Conflict;
            }

            if (request.RequesterType.HasValue)
            {
                customer.RequesterType = request.RequesterType.Value;
            }

            customer.FullName = request.FullName.Trim();
            customer.PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty;
            customer.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            customer.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
            customer.CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? null : request.CompanyName.Trim();
            customer.DepartmentName = string.IsNullOrWhiteSpace(request.DepartmentName) ? null : request.DepartmentName.Trim();
            customer.DepartmentCode = string.IsNullOrWhiteSpace(request.DepartmentCode) ? null : request.DepartmentCode.Trim();
            customer.CostCenter = string.IsNullOrWhiteSpace(request.CostCenter) ? null : request.CostCenter.Trim();
            customer.EmployeeId = string.IsNullOrWhiteSpace(request.EmployeeId) ? null : request.EmployeeId.Trim();
            customer.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            await _context.SaveChangesAsync(cancellationToken);
            return CustomerUpdateResult.Updated;
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/customers/{id}", async (
            int id,
            [FromBody] Command command,
            IMediator mediator,
            IValidator<Command> validator,
            CancellationToken cancellationToken) =>
        {
            var commandWithId = command with { Id = id };

            var validationResult = await validator.ValidateAsync(commandWithId, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var result = await mediator.Send(commandWithId, cancellationToken);
            return result switch
            {
                CustomerUpdateResult.Updated => Results.NoContent(),
                CustomerUpdateResult.Conflict => Results.Problem(
                    detail: "Customer was updated by another user. Reload before saving again.",
                    statusCode: StatusCodes.Status409Conflict),
                _ => Results.NotFound()
            };
        })
        .WithName("UpdateCustomer")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.CustomersWrite);
    }

    internal enum CustomerUpdateResult
    {
        Updated,
        NotFound,
        Conflict
    }
}
