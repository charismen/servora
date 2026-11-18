# Service Management Backend

.NET 10 Minimal API backend for multi-industry service, repair, and maintenance operations. Code is organized by vertical feature slices under `src/Servora.Api/Features`.

## 🚀 Features

- **Vertical Slice Architecture**: feature-owned endpoints, requests, validators, handlers, and DTOs.
- **Minimal APIs + MediatR**: thin HTTP endpoints with request handling behind feature slices.
- **Entity Framework Core + MySQL**: application tables use lowercase snake_case names and auto-increment `int` primary keys.
- **Auth/RBAC**: JWT auth, shop membership, multi-role users, additive permissions, paged/searchable/sortable user administration, role CRUD, permission matrix, and permission-gated APIs.
- **Shop-scoped operations**: customers, devices, tickets, statuses, inventory, reports, settings, and notification templates resolve through current shop context.
- **Customer management**: paged customer search/filtering with server-side sorting by default priority, name, age, or ticket count, stale edit protection, device authorization regression coverage, and list/search indexes.
- **Service ticket workflow**: dashboard authorization coverage, paged/sorted ticket list, Kanban authorization/filter coverage, full ticket endpoint permission-matrix coverage, receipt/warranty/ticket-history/move/assign/note/customer-history/device-history/repeat-summary authorization coverage, intake, staff picker with authorization regression coverage, structured accessories, condition/photo notes, local photo-evidence attachments with image MIME/size validation, settings-driven required intake photos through the SvelteKit create action plus backend create guard, ticket numbering, status transitions, history, notes, approvals, payments, pickup override, service-template checklist application, inventory usage actions, receipt data, public tracking token, settings-controlled public photo evidence, safe public tracking responses for unknown/disabled/deleted tickets, and print logging.
- **Ticket create hardening**: optional idempotency keys are persisted on `service_tickets` with a unique `(ShopId, IdempotencyKey)` index so client retries return the existing ticket instead of duplicating intake.
- **Ticket detail safety**: update/payment/accessory/photo endpoints return field-level validation problems where possible, block final/cancelled ticket detail edits, reject stale detail saves with `409 Conflict`, reject overpayment, require pickup override reasons for unpaid pickup bypass, keep workflow rule failures as clear bad requests, enforce shop/deleted-ticket scope, and clean uploaded files if database persistence fails.
- **Ticket detail tests**: integration coverage verifies payment updates, workflow guard failures, final edit locking, approval/payment edge cases, attachment upload/delete, accessory scoping, ticket idempotency, read/write authorization, public tracking token/photo/history visibility and reason-redaction safety, and cross-shop read/mutation blocking.
- **Inventory and reports**: category CRUD, paged/sorted item list, stale item-profile edit protection, item detail, supplier/barcode/secondhand fields, stock movements, movement history export, reservations, usage actions with reservation/consume/release integration coverage, inventory/report authorization regression coverage, low-stock signals, low-stock grouping, cost-gated valuation reports, Excel/CSV import preview/apply, indexed report summaries, and CSV export support.
- **Activity Center**: paged and filterable activity log endpoint backed by entity/action/date indexes, authorization regression coverage, and dashboard recent-feed compatibility.
- **Settings**: shop profile with authorization and stale save protection, reset-default, logo-upload, users-and-roles/print-template/service-template/service-status/notification-template regression coverage, configurable currency (`IDR`/`USD`), inventory pricing/import defaults, operational rule flags for photo/public tracking/approval/payment, reset defaults, ticket numbering, service statuses, paged/searchable service templates, print templates, users/roles/permissions, paged/sorted notification templates, and list indexes for Users & Roles and Notification Templates.
- **Swagger/OpenAPI**: local API exploration with JWT bearer auth support.
- **Hangfire**: MySQL-backed background worker infrastructure for notification and maintenance jobs; the dashboard is development-only.
- **Health and backup**: `/health`, `/health/ready`, MySQL backup script, and restore script.
- **Testing**: integration test setup uses `WebApplicationFactory` and Testcontainers.

## 🛠️ Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Docker & Docker Compose](https://www.docker.com/) (Required for running infrastructure locally and integration tests)

## 🏃‍♂️ Getting Started

### Foundation database workflow

This repo keeps `dotnet-ef` as a local tool:
```bash
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/Servora.Api/Servora.Api.csproj --startup-project src/Servora.Api/Servora.Api.csproj
```

Seed data is available through the startup seeder. Set `SeedData__RunOnStartup=true` to create the initial service-shop roles and permissions while applying migrations.

Database configuration is split into structured keys instead of a single default connection string:

```bash
Database__db_host=localhost
Database__db_port=3306
Database__db_user=root
Database__db_password=password
Database__db_name=service_management
```

The app still accepts `ConnectionStrings__DefaultConnection` as a legacy fallback when the structured `Database` keys are not provided.

### Option 1: Docker Compose (Easiest)

From repo root, spin up API, frontend, and MySQL:
```bash
docker-compose up --build -d
```
The API Swagger page is available at `http://localhost:5000/swagger`.

### Option 2: Local Development Setup

If you want to run the API via your IDE (Visual Studio, Rider, VS Code) while keeping the database in Docker:

1. Start the database dependency:
```bash
docker-compose up mysql -d
```

2. Run the application (using `launchSettings.json`):
```bash
cd src/Servora.Api
dotnet run
```
Navigate to `https://localhost:5001/swagger` (or `http://localhost:5000/swagger`).

Common local launch profile URL from the root README is `http://localhost:5051/swagger`.

Health endpoints:

```text
GET /health
GET /health/ready
```

## 🧪 Testing

The backend uses integration tests to verify feature slices from API to database.

To run the tests:
```bash
dotnet test
```
*Note: Ensure the Docker engine is running on your machine, as Testcontainers will automatically download and start a MySQL container during the test run.*

## 📂 Project Structure

```text
src/
└── Servora.Api/
    ├── Features/
    │   ├── Customers/
    │   ├── Dashboard/
    │   ├── Inventory/
    │   ├── Reports/
    │   ├── ServiceStatuses/
    │   ├── ServiceTickets/
    │   │   ├── ServiceTicketAccessory.cs
    │   │   └── ServiceTicketAttachment.cs
    │   └── Settings/
    ├── Infrastructure/   # Cross-cutting concerns (EF Core, Messaging, Exceptions)
    ├── Properties/       # launchSettings.json
    ├── appsettings.json
    └── Program.cs        # DI Setup and Middleware Pipeline

tests/
├── Servora.Api.UnitTests/
└── Servora.Api.IntegrationTests/
    ├── IntegrationTestWebAppFactory.cs  # Testcontainers setup
    └── Features/
```

## 🧩 Adding a New Feature

1. Create or reuse a folder under `Features/`.
2. Keep request DTOs, validators, handlers, and endpoint mapping close to the feature.
3. Use existing shop context, permission checks, enum standards, and activity/history patterns.
4. Add migrations with standard names such as `AddCustomers` or `AddFullNameToUsers`.
5. Local frontend wording belongs in frontend dictionaries, not backend responses, unless API-localized output is explicitly required.
