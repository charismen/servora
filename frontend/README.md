# Service Management Frontend

SvelteKit frontend for laptop/PC repair shop operations.

## Stack

- SvelteKit 2
- Svelte 5
- TypeScript
- Tailwind CSS 4
- Server-side API proxy through SvelteKit load/actions
- English and Bahasa Indonesia dictionaries in `src/lib/i18n`

## Current Scope

- Login and guarded authenticated workspace.
- Permission-based navigation.
- Dashboard activity, operational summary, and a paged/filterable Activity Center.
- Customers with search/filter/sort/page-size controls, devices, and customer service history.
- Service ticket list with server-side search/filter/sort/page-size controls, create, detail, update, soft delete, receipt, public tracking with safe not-found handling and settings-controlled photo evidence, staff picker, structured accessories, condition/photo notes, settings-driven required intake photos with submitted photo count, local photo-evidence thumbnail cards, approvals, payments, notes, status flow, service-template checklist application, and inventory usage.
- Create Ticket visible labels and validation messages use the EN/ID i18n dictionaries.
- Kanban board backed by ticket workflow endpoints, including technician and date filters.
- Inventory screen with category management, pricing fields, supplier/barcode/secondhand fields, paged/sorted item list, stale edit warnings, item detail, printable item labels, movement filters/export, Excel/CSV import preview/apply workflow, and report/export screens backed by API calls.
- Reports include low-stock grouping by category/type, cost-gated inventory valuation, table page-size controls, per-section sorting, and drill-down links to ticket/inventory detail.
- Settings for shop profile with stale-save warnings, currency, inventory pricing defaults, operational rule flags, reset defaults, service statuses, paged/searchable service templates, ticket numbering, print templates with live receipt preview, paged/searchable users with roles/permissions, and notification templates.

## UI Standards

- Use shared `PageHeader` for standard app pages with breadcrumb, title, subtitle, and top-right actions.
- Breadcrumb appears above title.
- Top-right actions belong in `PageHeader` action slot so page buttons align across Tickets, Kanban, Customers, Settings, Inventory, and Reports.
- App sidebar supports expanded mode and icon-only collapsed mode. Header/content offsets follow sidebar width.
- Sidebar text should stay readable; greyed-out styling is reserved for disabled content.
- Risky settings changes use centered in-app confirmation dialogs, not browser `confirm`.
- Destructive delete actions use centered in-app confirmation dialogs, not browser `confirm`.
- Ticket attachment downloads use SvelteKit proxy routes so JWT stays server-side.
- Ticket Detail side-panel actions should preserve visible form controls, refresh route data on success, and show accessible floating action feedback.
- Ticket Detail saves include the loaded update timestamp and show a localized conflict warning when another user changed the ticket first.
- Ticket Detail payment form keeps submitted values on validation failure, blocks negative amounts, warns when paid amount exceeds total, and surfaces backend overpayment validation.
- Ticket Detail status panel shows transition requirements, disables unavailable transitions, and preserves failed-submit values.
- Ticket Detail photo evidence uses thumbnail cards, upload pending state, file validation hints, size metadata, and broken-preview fallback.
- Clicking a Ticket Detail photo thumbnail opens an in-app photo viewer with previous/next controls and a scrollable thumbnail stack for multiple photos.
- Ticket Detail accessories use card-style add/list/delete flow with validation, failed-submit value preservation, and focus reset after successful add.
- Ticket Detail assignment, notes, approvals, pickup override, inventory, and WhatsApp panels refresh their own data and keep the main detail draft intact.
- Ticket Detail service-template checklist actions apply copied checklist rows and toggle completion without clearing unsaved main-detail edits.
- Ticket Detail protects unsaved main-detail edits with an app-level leave dialog for internal navigation and browser unload fallback for tab/window exits.
- Frontend API errors extract ASP.NET validation problem messages so Ticket Detail forms show actionable backend validation text.
- Ticket Detail displays localized enum/status labels, attachment type labels, WhatsApp controls, and form feedback while preserving backend codes in API calls.
- Dashboard summary, attention table, status distribution, and workload copy use localization dictionaries.
- Inventory list/detail/label metrics, table/card labels, stock actions, movement empty state, stale-edit conflict warnings, and action feedback use localization dictionaries.
- Customers list filters, table headers, empty state, status labels, and action titles use localization dictionaries.
- Customer create/edit forms use localization dictionaries for labels, placeholders, buttons, local validation feedback, and stale-edit conflict warnings.
- Customer Detail uses localization dictionaries for contact, devices, operational status, metadata, and device form copy.
- Activity, Reports, and Notification Templates use localization dictionaries for remaining breadcrumb, placeholder, and feedback copy.
- Settings General and Print Templates use localization dictionaries for breadcrumb and local logo validation feedback.
- Settings ticket numbering date-format options use localization dictionaries for readable EN/ID labels.
- Shared Breadcrumbs, Pagination, and searchable Dropdown receive localized labels.
- Users & Roles admin feedback, status labels, empty states, and search placeholders use localization dictionaries.
- Users & Roles users and Notification Templates have page-size, pagination, and sort controls.
- Service Statuses admin headings, form labels, table headers, behavior flags, and feedback use localization dictionaries.
- App Shell notifications, menu aria labels, logout overlay, and Kanban validation feedback use localization dictionaries.
- Login and root landing/navigation aria, placeholder, feature, and footer copy use localization dictionaries.
- Logo alt text, public tracking timeline fallbacks, and export-route unauthorized errors use localization dictionaries.
- Customer Detail shows repeat service history plus collapsed per-device service history using the device service-history API.
- Ticket Detail manual QA checklist lives in `../ticket-detail-implementation-plan.md` Phase 11 for draft preservation, floating feedback, attachments, accessories, and mobile layout.
- Receipt print views must print receipt content only, without app sidebar/header chrome.
- Mobile release QA follows `../mobile-regression-checklist.md`.

## Localization

All visible UI copy must use `src/lib/i18n/index.ts`.

Rules:

- Add English (`en`) and Bahasa Indonesia (`id`) keys in same change.
- Avoid hardcoded headings, labels, placeholders, button text, modal copy, table headers, empty states, validation text, and toast text.
- Use shared format helpers for dates, numbers, relative time, and money.
- Money display follows configured shop currency, currently `IDR`/Rp and `USD`/$.

## Developing

```sh
npm install
npm run dev
```

Default dev URL:

```text
http://localhost:5173
```

Set API base URL in `.env` or `.env.local` when backend is not using the default local URL.

## Checks

```sh
npm run check
npm run build
```

## Project Layout

```text
src/
├── lib/
│   ├── api/          # shared client helpers
│   ├── auth/         # permission helpers
│   ├── components/   # shared UI components
│   ├── i18n/         # dictionaries and format helpers
│   ├── server/       # server-side API helpers
│   └── types/        # frontend API types
└── routes/
    ├── app/          # authenticated workspace
    ├── login/        # auth page
    └── track/        # public tracking page
```
