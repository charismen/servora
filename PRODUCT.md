# Product — Servora

## Platform

Web (Responsive operational console + token-gated public tracking portal)

## Target Users

1. **Commercial Service & Repair Operators:** Multi-industry workshops and field service businesses (laptop/PC repair, mobile phone & tablet repair, motorbike & car workshops, AC/HVAC contractors, appliance repair, and custom equipment servicing).
2. **Corporate Subsidiaries, Multi-Branch Groups & Internal Divisions:** Holding companies operating multiple branches or internal maintenance teams (internal IT support, fleet maintenance, facility/building engineering) seeking dedicated workspaces under a shared organization.
3. **Technicians, Dispatchers & Counter Staff:** Daily ticket/work-order intake, Kanban workflow progression, dynamic checklists, on-site dispatch, parts consumption, preventive maintenance cycles, and attendance logging.
4. **Customers & Clients (Public Tracking):** Token-gated live status updates, scheduled visit details, public custom fields, and phone-verified photo evidence at `/track/[token]`.

## Product Purpose

**Servora** is a configurable, multi-industry service, repair, and maintenance operations platform. It adapts its workflow statuses, terminology, asset fields, intake checklists, and billing rules to match either commercial repair shops or internal corporate maintenance divisions — all under a clean organization-and-workspace hierarchy.

## Core Capabilities

- **Organization & Workspace Hierarchy:** Shared user identity across multiple branches/divisions (`organizations`, `workspaces`, `workspace_users`) with an interactive Workspace Switcher and an Executive Organization Overview.
- **Dual Operating Modes (`Commercial`, `InternalMaintenance`, `Hybrid`):**
  - *Commercial:* External customers, quotes/approvals, billing & tax rounding, payments, unpaid-pickup override policies, receipts, and public tracking.
  - *Internal Maintenance:* Internal departments/employees, cost centers, SLA due targets, and zero-cost internal work orders without retail payment blockers.
  - *Hybrid:* External jobs and internal maintenance orders side-by-side.
- **6 Built-in Industry Blueprints & Setup Wizard:** `it_computer`, `mobile_phone`, `automotive_vehicle`, `hvac_ac`, `facility_internal`, and `custom` — provisioning statuses, typed custom fields (`Text`, `Number`, `Select`, `Boolean`, `Date`, `Multiline`), intake inspection checklists, starter templates, and bilingual `en` + `id` terminology.
- **Field Service & Preventive Maintenance:** `InShop` / `OnSite` / `PickupDelivery` location modes, saved site addresses, visit windows, multi-asset linking, and calendar/meter-based recurring schedules processed by Hangfire.
- **Complete Operational Suite:** Kanban board, spare parts inventory & reservations, internal workshop assets, staff attendance, full-time & freelance technician payroll, reporting, and activity logs.

## Brand & Design Commitments

- Warm neutral & ink design system (`frontend/src/app.css`: `#faf8f5` surface, `#1c1815` ink, `#b45039` claytile accent, `#e6dfd5` stone borders, `#f4f0ea` ash containers, `Inter` typography).
- Consistent `PageHeader.svelte` header layout, collapsible left sidebar, global keyboard search (`⌘K` / `Ctrl+K`), and complete bilingual `en` + `id` i18n coverage.
