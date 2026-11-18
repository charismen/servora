# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

- **Front Desk / Service Intake Staff**: Operating the shop service counter in fast-paced retail and repair environments. Job: Greet customers, inspect incoming laptop/PC devices, record physical cosmetic condition, catalog accessories (chargers, dongles, sleeves), capture mandatory intake condition photos, issue printed intake receipts, handle pickup handovers, and process payments.
- **Repair Technicians**: Operating at repair workbenches. Job: Diagnose hardware and software faults, log diagnostic findings and technical notes, consume repair parts from inventory, complete service checklists, and progress tickets across repair lifecycle stages.
- **Shop Owner / Service Manager**: Overseeing daily operations, technician workload distribution, stock valuation, inventory restock alerts, service status configurations, staff roles and permissions, and operational reporting.
- **Device Owners / Customers** (External): Device owners dropping off equipment for repair. Job: Track real-time repair progress via public status URL, inspect photo evidence of diagnosis/damage, review cost estimates, and receive WhatsApp notifications.

## Product Purpose

A high-precision daily operational console for independent laptop and PC repair shops. It replaces fragile paper intake slips, fragmented WhatsApp chats, and disconnected spreadsheets with a disciplined, auditable service lifecycle. Success means zero customer disputes over pre-existing device damage or missing accessories, zero untracked inventory leakage at technician benches, and fast, transparent turnaround for customers.

## Positioning

Unlike generic CRM or generic helpdesk ticketing platforms, this platform is purpose-engineered around physical device custody and workbench integrity:
- **Intake Custody & Evidence**: Mandatory intake condition photo logging with in-app inspection viewer, paired with structured accessory logging tied directly to the customer receipt.
- **First-Class Device Records**: Devices are persistent entities with full repeat service histories across customer visits, not disposable text strings on a ticket.
- **Workbench-to-Inventory Sync**: Direct technician parts consumption tied to live inventory stock movements and ticket cost accounting.
- **Zero-Friction Customer Transparency**: Public tokenized tracking links showing live status milestones and settings-controlled photo evidence without requiring customer login.

## Operating Context

- **Physical Environment**: Active service counters and technician repair benches. Workstations primarily use desktop or laptop web browsers; mobile tablets/phones may be used at intake counters or QA benches.
- **Critical Workflows**:
  - *Counter Intake*: Customer hands over device; staff inspects cosmetic flaws, checks serial numbers, logs power cables/accessories, captures condition photos, and prints thermal or A4 receipts.
  - *Workbench Diagnosis & Repair*: Technician opens ticket, reviews intake notes, tests device, requests customer approval for parts/costs if needed, consumes inventory parts, and marks status changes.
  - *Pickup & Settlement*: Customer presents claim ticket or code; front desk verifies payment balance, records payment transaction, prints final invoice/receipt, and releases the device.
- **Language & Locale**: Fully bilingual operation (English and Bahasa Indonesia `id-ID`). Dual currency formatting (`IDR`/Rp and `USD`/$).

## Capabilities and Constraints

- **Confirmed Capabilities**:
  - Comprehensive service ticket lifecycle (Intake -> Diagnosis -> Approval -> In Progress -> Ready for Pickup -> Closed / Cancelled).
  - Configurable database-driven service statuses with custom color badges and transition rules.
  - Interactive Kanban board with technician and date filters.
  - Customer & device relationship management with per-device repeat repair history.
  - Inventory management with categories, pricing tiers (cost vs retail), stock movement tracking, barcode support, Excel/CSV import preview, and low-stock alerts.
  - WhatsApp notification template integration.
  - Additive role-based permissions (RBAC) across shop users.
  - Concurrency conflict detection: Stale-save timestamp warnings preventing accidental overwrites when multiple staff edit tickets or inventory simultaneously.
  - Thermal / A4 receipt printing with dedicated print-only stylesheets.
- **Technical Constraints**:
  - Currently runs as a single-shop deployment, but all database entities and APIs are scoped with `shop_id` for SaaS readiness.
  - Backend authorization is authoritative; frontend permissions only gate UX visibility.
  - SvelteKit server-side proxy handles JWT authentication and media downloads.

## Brand Commitments

- **Design Language**: Attio-inspired precision monochrome digital toolkit design system (documented in `DESIGN.md`).
- **Typography & Tone**: Humanist editorial elegance (Newsreader/Tiempos serif for major headlines) paired with clinical tool-like precision (Inter sans-serif with `ss03` stylistic alternates for all UI controls and body text).
- **Color Discipline**: High-contrast monochrome axis (ink `#1c1d1f` on white `#ffffff`) with color reserved strictly for deliberate interactions (`Action Blue` `#407ff2`) and semantic statuses (`Success Green`, `Danger Red`, `Warning Yellow`).
- **Voice**: Professional, factual, direct, and reassuring.

## Evidence on Hand

- Active codebase: SvelteKit 2 + Svelte 5 frontend in `frontend/`, .NET 10 Minimal API monolith in `backend/`.
- Working localized dictionaries in `frontend/src/lib/i18n/` for `en` and `id`.
- Complete design specification in `frontend/DESIGN.md`.
- Architectural specifications and implementation roadmaps in `service-maintenance-mvp-implementation-plan.md` and `service-maintenance-implementation-plan-phase-5-plus-updated.md`.
- *Note on Absences*: No public SaaS self-sign-up or multi-tenant billing flow currently implemented (intentionally deferred per Phase 5+ architecture).

## Product Principles

1. **Intake Integrity Over Speed**: Never compromise physical device custody. Always record accessories and cosmetic conditions at intake to eliminate disputes.
2. **Every Part Accounted For**: Inventory and ticket costs must strictly reflect reality at the technician's bench; parts are never consumed off-the-books.
3. **Transparent to the Customer**: Keep customer communication clear and visual, sharing repair status and photographic evidence without requiring account creation.
4. **Clinical Precision, Zero Clutter**: Operational screens must maximize scanability and focus. Information density should serve the technician and counter staff without cognitive noise.
5. **Fail-Safe Operational Guardrails**: Protect unsaved drafts, warn on concurrent staff edits, and confirm destructive actions to prevent real-world operational errors.

## Accessibility & Inclusion

- High contrast default palette (WCAG AA compliant contrast ratios for monochrome text and semantic badges).
- Full keyboard accessibility on tables, modals, dropdowns, and forms.
- Bilingual interface (English and Bahasa Indonesia) across 100% of UI elements and validation messages.
