# AGENTS.md

This is the single canonical instruction file for agentic AI working in this repository.

Agents operating anywhere under this repository must follow this file. Do not create nested `AGENTS.md`, `CLAUDE.md`, or equivalent duplicate agent guidance files unless the user explicitly approves a new tool-specific pointer.

## Scope

- Repository: `servora`
- Solution: `Servora.sln`
- Main service: `src/Servora.Api/`
- Test projects: `tests/Servora.Api.UnitTests/`, `tests/Servora.Api.IntegrationTests/`
- Runtime: ASP.NET Core 10.0 Minimal API Monolithic Backend
- Architecture: Vertical Slice Architecture (VSA) with CQRS
- Key Libraries: EF Core, MediatR, FluentValidation, ASP.NET Core Identity, Hangfire

## Rule Priority

When rules conflict, follow this order:

1. Correctness and safety
2. Maintainability
3. Readability
4. Consistency with existing codebase
5. Style preferences in this document

## Style Guidelines

- Strictly follow existing code style in the codebase.
- Keep code concise, tasteful, and elegant.
- Do not overcomplicate. Do not implement more than needed.
- Ensure code is readable by humans, easy to extend, and maintainable.
- Adhere to SOLID principles, especially Single Responsibility.
- Use explicit classes or records for DTOs and Data Models.
- Use strongly typed enums over bare string or integer literals.
- Prefer exceptions with domain types or `Result<T>` over silent null returns.

### Vertical Slice Architecture & CQRS

- Features are grouped by use-case inside `src/Servora.Api/Features/`.
- Every feature must be self-contained: Command/Query, Validator, Handler, and Minimal API mapping should ideally live close together.
- Use `MediatR` for dispatching Commands and Queries.
- Use `FluentValidation` to validate input commands securely inside the MediatR pipeline.
- Cross-cutting concerns (DB Context, Exceptions, Auth, Notifications, Background Jobs) belong in `src/Servora.Api/Infrastructure/`.
- Use ASP.NET Core Identity for User and Role management. Avoid duplicating auth tables.
- Protect endpoints using dynamic policy requirements (e.g., `.RequireAuthorization("Permissions.Resource.Action")`).

## Documentation

The `docs/` directory holds living reference documents when present. Update them incrementally as you explore, plan, and implement.

- Append new findings; do not overwrite existing content without good reason.
- Keep entries short and factual.
- Read `docs/` into context only when directly relevant to the current task.
- Do not run tests for documentation-only changes.

## Security

- Never hard-code secrets, tokens, or credentials.
- Apply least privilege; do not broaden access scopes beyond what the task requires.
- Set local DB connection values in `src/Servora.Api/appsettings.Development.json`.
- Never commit real production secrets to `appsettings.Development.json` or any tracked file.

## Decision Framework

When multiple approaches are possible:

1. Prefer the simplest solution that satisfies requirements.
2. Prefer explicit over implicit.
3. Prefer type safety over `dynamic` or `object`.
4. Avoid premature abstraction.
5. Prefer the standard library over third-party packages for simple operations.

## Planning Rules

Before starting a non-trivial task, create a plan at:

```text
agent_logs/YYYYMMDD_HHMMSS_<descriptive_name>_plan.md
```

- Each plan must be standalone: define inputs, constraints, and success criteria.
- Plans must be executable without prior context.
- Phases must be scoped with clear acceptance criteria and independently testable.
- Planning output must not modify repository code.
- Must list `files_to_change` and `new_files`.
- Must describe a concise, coherent technical approach.

### Confirmation Gate

After the plan file is written, stop and do the following before implementation:

1. Present a concise summary of the plan to the user.
2. Ask the user to confirm they are happy with the plan.
3. Do not begin implementation until explicit confirmation is received.

## Implementation Rules

- Implement complete phases end-to-end; no incomplete features should remain.
- After completing each phase, update the implementation log so state is recoverable.
- Preserve existing inline comments; do not remove useful historical context.
- Do not commit or push without explicit approval.

### Database and Migration Standards

- Use generic, business-readable table names.
- Use lowercase snake_case physical table names.
- Prefer plural entity table names such as `users`, `roles`, `settings`, `customers`, `service_tickets`, `service_statuses`, `inventory`, and `inventory_items`.
- Use descriptive join table names such as `user_roles`, `service_ticket_inventory_items`, and `service_ticket_accessories`.
- Explicitly map ASP.NET Identity tables instead of accepting framework-default table names.
- Use `int` auto-increment primary keys for application tables.
- Do not use UUID/GUID primary keys for normal application entities.
- Use a single `Id` primary key unless an infrastructure dependency requires a natural key.
- Foreign keys must use `int` to match referenced application primary keys.
- Name migrations with concrete PascalCase schema actions:
  - New tables: `AddTableName`, for example `AddUsers`, `AddCustomers`, `AddUsersRolesSettings`.
  - Added columns: `AddColumnNameToTableName`, for example `AddFullNameToUsers`.
  - Removed columns: `RemoveColumnNameFromTableName`, for example `RemoveFaxNumberFromUsers`.

## Environment

### Setup

```bash
dotnet build Servora.sln
```

Local service run requires:
- MySQL on port `3306`

You can easily spin up the required infrastructure via Docker Compose:
```bash
docker-compose up mysql -d
```

### Docker

```bash
docker-compose up --build -d
```
This starts the stack including the API and MySQL.

## Formatting

Run at the end of full implementation:

```bash
dotnet format Servora.sln
```

## Testing Rules

- Tests use xUnit, FluentAssertions, and Moq.
- Fix all failing tests before proceeding.
- Never bypass tests without explicit instruction.
- Do not run tests for documentation-only changes.

Run all tests:

```bash
dotnet test Servora.sln
```

### Integration Tests
- Integration tests use `WebApplicationFactory` combined with **Testcontainers**.
- Ensure the Docker engine is running before executing integration tests, as Testcontainers will dynamically spin up a MySQL instance.

### Mock Generation
Mocks use `Moq`. No code generation step is needed.

```csharp
var myService = new Mock<IMyService>();
myService.Setup(x => x.DoSomething()).Returns(Task.CompletedTask);
```

## Definition of Done

A task is complete when:

- Plan is fully executed.
- No partial implementations remain.
- Code compiles with `dotnet build Servora.sln`.
- Tests pass with `dotnet test Servora.sln`.
- Logbook is updated appropriately.
- `docs/` is updated wherever relevant.
- Changes are consistent with repository standards.

## Future Work

When you identify improvements beyond the current task scope:

- Document them clearly in the relevant log or doc file.
- Do not implement them unless explicitly instructed.
