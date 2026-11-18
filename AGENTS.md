# AGENTS.md

This file defines project-scope instructions for agentic AI working in the **Servora** repository.

## Scope

- **Project:** Servora — Multi-Industry Service, Repair, and Maintenance Operations Platform
- **Backend:** `backend/src/Servora.Api/` (.NET 10, ASP.NET Core Minimal/Feature Endpoints, MediatR, FluentValidation, EF Core 9 + Pomelo MySQL 8.0, Hangfire)
- **Frontend:** `frontend/` (SvelteKit 2, Svelte 5, Tailwind CSS v4, TypeScript)
- **Tests:** `backend/tests/Servora.Api.UnitTests/` and `backend/tests/Servora.Api.IntegrationTests/`

## Database Standards

### Table Naming
- Use generic, business-readable, lowercase `snake_case` plural physical table names:
  - `organizations`, `organization_users`, `workspaces`, `workspace_users`, `workspace_user_roles`
  - `users`, `roles`, `permissions`, `role_permissions`, `settings`
  - `customers`, `customer_locations`, `service_assets`, `asset_field_definitions`, `asset_field_values`, `ticket_field_values`
  - `inspection_checklist_templates`, `ticket_inspection_results`
  - `service_tickets`, `service_ticket_assets`, `service_statuses`, `service_templates`, `maintenance_schedules`
  - `inventory_items`, `inventory_movements`, `workspace_assets`, `attendances`, `payroll_adjustments`, `activity_logs`
- Explicitly map framework tables such as ASP.NET Core Identity tables instead of accepting framework defaults.

### Primary & Foreign Keys
- Use `int` auto-increment primary keys for application tables (no UUID/GUID PKs on standard domain entities).
- Foreign keys must use `int` to match referenced primary keys.

### Migration Naming
- Keep migration names concrete and schema-oriented, not phase-oriented:
  - New tables: `AddOrganizations`, `AddMaintenanceSchedules`
  - Added columns: `AddSlaDueAtToServiceTickets`
  - Removed columns: `RemoveLegacyFieldFromWorkspaces`

### Organization & Workspace Scoping
- Business tables must include `workspace_id INT NOT NULL` unless data is global or organization-level.
- Always resolve the active `workspace_id` authoritatively on the backend via `IShopContext` (`ShopContext`), validating active DB membership in `workspace_users` for every request (never trust raw frontend-provided workspace IDs as an authorization boundary).
- Scope all operational queries by `workspace_id`.

### Permissions & RBAC
- Use `workspaces`, `workspace_users`, `workspace_user_roles`, `permissions`, and `role_permissions` for workspace-level additive RBAC, and `organization_users` for optional organization-level Owner/Admin/Executive visibility.
- Backend authorization (`Permissions.*`) is authoritative. Frontend permission checks only control navigation and UI visibility.

### Domain & Enum Standards
- `service_assets` are first-class reusable records across industries (devices, vehicles, AC units, facility equipment). Ticket intake may create an asset inline, but must persist a real `service_assets` record.
- Store backend-controlled workflow/domain states as enums mapped to strings via EF Core conversions (`OperatingMode`, `RequesterType`, `ServiceLocationType`, `CustomFieldScope`, `CustomFieldType`, `InspectionItemStatus`, `MaintenanceTriggerType`, `MaintenanceScheduleStatus`, `PaymentStatus`, etc.). Never compare states against raw string literals.
- Keep configurable business data (service statuses, custom field definitions, checklists, terminology labels) database-driven.

## Frontend Standards

### Localization (`en` + `id`)
- All visible UI copy must reside in `frontend/src/lib/i18n/index.ts` with both English (`en`) and Bahasa Indonesia (`id`) entries.
- Do not hardcode user-facing strings directly in Svelte components unless displaying dynamic backend data or workspace-configured terminology (`resolveTerminology`).

### Layout & Design System
- Standard app pages with breadcrumb, title, subtitle, and top-right actions must use `frontend/src/lib/components/PageHeader.svelte`.
- Retain the warm neutral & ink design tokens in `frontend/src/app.css` (`#faf8f5` surface, `#1c1815` ink, `#b45039` claytile accent, `#e6dfd5` stone borders, `#f4f0ea` ash containers).
- Keep backend and frontend DTO contracts aligned through TypeScript definitions in `frontend/src/lib/types/`.
