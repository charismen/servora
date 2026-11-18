# Attio — Style Reference
> Precision Digital Toolkit. A design system built on a foundation of high-contrast warm craft neutrals, where soft serif headlines provide a human touch to a clinical, tool-like interface shaped around Claytile (#b45039) and Rustshade (#d1c4b4).

**Theme:** light

The design feels like a meticulously organized, high-end instrument. It operates on an earthy, high-contrast warm axis, where roasted obsidian ink (#1c1815) on warm canvas (#faf8f5) or pure white (#ffffff) is the default state for text and primary actions. The most distinctive choice is the typographic duality: large, inviting headlines are set in the soft serif Tiempos Text, while the entire user interface, from buttons to body copy, uses the neutral sans-serif Inter. This creates a rhythm between approachable storytelling and functional precision. Color is used with disciplined restraint: Claytile (#b45039) commands interactive focus and navigational states, Rustshade (#d1c4b4) structures borders and subtle surfaces, and semantic indicators provide clear feedback without visual fatigue. A consistent 10px radius on buttons provides a soft counterpoint to the otherwise sharp, grid-aligned UI frames.

## Tokens — Colors

| Name | Value | Token | Role |
|------|-------|-------|------|
| White | `#ffffff` | `--color-white` | Primary card and modal surfaces, text on dark surfaces |
| Canvas | `#faf8f5` | `--color-background` | Outer workspace background, canvas |
| Ash | `#f4f0ea` | `--color-ash` | Subtle background panels, table hover rows |
| Stone | `#e6dfd5` | `--color-stone` | Light dividers, card borders |
| Slate (Rustshade) | `#d1c4b4` | `--color-slate` | Default UI borders, active frames, input boundaries |
| Lead | `#b5a897` | `--color-lead` | Placeholder text, disabled UI elements |
| Overcast | `#8c8072` | `--color-overcast` | Secondary body text, timestamps, supporting labels |
| Metal | `#6d6255` | `--color-metal` | Tertiary body text, icons, navigation links |
| Carbon | `#4d443a` | `--color-carbon` | Deep neutral labels, form legends |
| Warm Ink | `#1c1815` | `--color-ink` | Primary text, headlines, primary button background |
| Abyss | `#100d0b` | `--color-abyss` | Deep neutral elements, footer background |
| Claytile | `#b45039` | `--color-claytile` / `--color-action-blue` | Primary interactive accent: links, active nav items, key buttons, and focus rings |
| Rustshade | `#d1c4b4` | `--color-rustshade` | Structural warm neutral: card outlines, dividers, border foundation |
| Focus Glow | `rgba(180, 80, 57, 0.35)` | `--color-focus-blue` | Focus rings and glows on interactive elements |
| Success Green | `#1b5e39` | `--color-success-green` | Status indicators, success notifications |
| Danger Red | `#8f2824` | `--color-danger-red` | Error messages, destructive action indicators |
| Warning Yellow | `#875200` | `--color-warning-yellow` | Warning notifications, status indicators |
| Magic Aura | `linear-gradient(131.88deg, #d1c4b4 7.36%, #b45039 81.74%)` | `--gradient-magic-aura` | Decorative gradient for warm brand accents |

## Tokens — Typography

### Tiempos Text — Used exclusively for large, emotive headlines (28px+) to add a soft, human, and editorial quality that contrasts with the functional UI. The signature `ss03` stylistic set is critical. · `--font-tiempos-text`
- **Substitute:** Newsreader, Lora
- **Weights:** 400, 500
- **Sizes:** 28px, 40px
- **Line height:** 1.10, 1.23
- **OpenType features:** `"ss03"`
- **Role:** Used exclusively for large, emotive headlines (28px+) to add a soft, human, and editorial quality that contrasts with the functional UI. The signature `ss03` stylistic set is critical.

### Inter Display — An optically-sized sans-serif for display text (32px+) where clarity and precision are paramount. Its tighter letter-spacing is key to its compact, authoritative appearance. · `--font-inter-display`
- **Substitute:** Inter
- **Weights:** 500, 600
- **Sizes:** 12px, 20px, 32px, 40px, 56px, 64px
- **Line height:** 1.00, 1.07, 1.10, 1.17, 1.19, 1.30
- **OpenType features:** `"ss03", "calt"`
- **Role:** An optically-sized sans-serif for display text (32px+) where clarity and precision are paramount. Its tighter letter-spacing is key to its compact, authoritative appearance.

### Inter — The workhorse font for all UI elements: body copy, navigation, buttons, and labels. Its neutrality and legibility at all sizes make it the foundation of the user interface. The `ss03` stylistic set is always applied. · `--font-inter`
- **Substitute:** system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif
- **Weights:** 400, 500, 600, 700
- **Sizes:** 10px, 11px, 12px, 13px, 14px, 15px, 16px, 18px, 20px, 32px
- **Line height:** 1.00, 1.17, 1.19, 1.20, 1.23, 1.25, 1.30, 1.33, 1.38, 1.40, 1.42, 1.43, 1.45, 1.47, 1.50, 1.57, 2.20
- **Letter spacing:** -0.0200em, -0.0150em, -0.0130em, -0.0120em, -0.0110em, -0.0100em, -0.0050em
- **OpenType features:** `"ss03"`
- **Role:** The workhorse font for all UI elements: body copy, navigation, buttons, and labels. Its neutrality and legibility at all sizes make it the foundation of the user interface. The `ss03` stylistic set is always applied.

### Type Scale

| Role | Size | Line Height | Letter Spacing | Token |
|------|------|-------------|----------------|-------|
| caption | 12px | 1.5 | -0.14px | `--text-caption` |
| body-sm | 14px | 1.43 | -0.14px | `--text-body-sm` |
| body | 16px | 1.5 | -0.24px | `--text-body` |
| subheading | 20px | 1.3 | -0.4px | `--text-subheading` |
| heading-sm | 28px | 1.23 | -0.42px | `--text-heading-sm` |
| heading | 40px | 1.1 | -0.6px | `--text-heading` |
| heading-lg | 56px | 1.1 | -1.12px | `--text-heading-lg` |
| display | 64px | 1.07 | -1.28px | `--text-display` |

## Tokens — Spacing & Shapes

**Base unit:** 4px

**Density:** compact

### Spacing Scale

| Name | Value | Token |
|------|-------|-------|
| 4 | 4px | `--spacing-4` |
| 8 | 8px | `--spacing-8` |
| 12 | 12px | `--spacing-12` |
| 16 | 16px | `--spacing-16` |
| 20 | 20px | `--spacing-20` |
| 24 | 24px | `--spacing-24` |
| 28 | 28px | `--spacing-28` |
| 32 | 32px | `--spacing-32` |
| 36 | 36px | `--spacing-36` |
| 40 | 40px | `--spacing-40` |
| 44 | 44px | `--spacing-44` |
| 48 | 48px | `--spacing-48` |
| 60 | 60px | `--spacing-60` |
| 80 | 80px | `--spacing-80` |
| 100 | 100px | `--spacing-100` |
| 120 | 120px | `--spacing-120` |

### Border Radius

| Element | Value |
|---------|-------|
| tabs | 0px |
| tags | 4px |
| cards | 8px |
| inputs | 7px |
| buttons | 10px |

### Shadows

| Name | Value | Token |
|------|-------|-------|
| subtle | `rgb(238, 239, 241) 0px 0px 0px 1px inset` | `--shadow-subtle` |
| subtle-2 | `rgba(28, 40, 64, 0.1) 0px 2px 3px -2px, rgba(28, 40, 64, ...` | `--shadow-subtle-2` |
| subtle-3 | `rgba(28, 40, 64, 0.18) 0px 0px 2px 0px, rgba(0, 0, 0, 0.0...` | `--shadow-subtle-3` |

### Layout

- **Page max-width:** 1440px
- **Section gap:** 96px
- **Card padding:** 16px
- **Element gap:** 8px

## Components

### Primary CTA Button
**Role:** The main call-to-action.

Background: Warm Ink (#1c1815). Text: White (#ffffff). Font: 14px Inter weight 500. Padding: 8px 12px. Border: 1px solid #1c1815. Radius: 10px. Hover: #2e2621.

### Secondary CTA Button
**Role:** Secondary call-to-action, or an alternative to the primary.

Background: White (#ffffff). Text: Warm Ink (#1c1815). Font: 14px Inter weight 500. Padding: 8px 12px. Border: 1px solid Rustshade/Slate (#d1c4b4). Radius: 10px. Hover: Ash (#f4f0ea).

### Header Navigation Button
**Role:** Used for navigation links in the main header.

Background: transparent. Text: Metal (#6d6255). Font: 14px Inter weight 500. Padding: 6px 10px. Radius: 10px. Hover state has an Ash (#f4f0ea) background.

### Feature Tab Button
**Role:** Used in a tab group to switch between content views.

Background: transparent. Text: Metal (#6d6255). Font: 15px Inter weight 500. Padding: 8px 16px. Radius: 0px. Active state has Warm Ink (#1c1815) text and a 2px bottom border of Claytile (#b45039).

### UI Frame Card
**Role:** Container for complex UI modules, like the main app demo.

Background: White (#ffffff). Padding: 16px. Border: 1px solid Stone (#e6dfd5). Radius: 8px. Shadow: `rgba(28, 24, 21, 0.08) 0px 2px 3px -2px, rgba(28, 24, 21, 0.04) 0px 4px 6px -2px`.

### Text Input
**Role:** Standard text input field.

Background: White (#ffffff). Text: Warm Ink (#1c1815). Font: 14px Inter. Placeholder text: Lead (#b5a897). Border: 1px solid Rustshade/Slate (#d1c4b4). Radius: 7px. Focus state shows a Claytile glow using `rgba(180, 80, 57, 0.35)`.

### Logo Cloud Item
**Role:** Displays a partner or customer logo.

Grayscale logo fill using Carbon (#4d443a) or Metal (#6d6255). No background or border.

### Page Footer
**Role:** The closing section of the page with site-wide links.

Background: Abyss (#100d0b). Organized into columns. Column titles use White (#ffffff) text at 14px, weight 500. Links use Overcast (#8c8072) text at 14px, weight 400, changing to White on hover.

## Do's and Don'ts

### Do
- Always set display and hero headlines in Tiempos Text.
- Use Inter with the `ss03` font feature setting for all UI copy.
- Apply negative letter-spacing to all text 18px and larger, following the type scale.
- Construct primary CTAs from Warm Ink (#1c1815) backgrounds with White (#ffffff) text.
- Maintain a consistent 10px radius on all major buttons.
- Use borders (1px Rustshade/Slate #d1c4b4 or Stone #e6dfd5) as the primary method for separating UI elements.
- Reserve Claytile (#b45039) for interactive states like links, active nav pills, and focus rings.

### Don't
- Don't use Tiempos Text for body copy or any text smaller than 28px.
- Don't use color in headlines or primary buttons.
- Don't use fill-based colors unless for semantic status indicators.
- Don't apply shadows to buttons, inputs, or simple cards.
- Don't use radii other than 10px for buttons or 8px for cards.
- Don't forget to include the `ss03` font feature when setting type.
- Don't introduce new saturated colors; the palette is intentionally monochrome.

## Elevation

- **UI Frame Card:** `rgba(28, 40, 64, 0.1) 0px 2px 3px -2px, rgba(28, 40, 64, 0.04) 0px 4px 6px -2px`
- **Input Focus:** `0 0 0 3px color-mix(in srgb, #94b9ff 40%, transparent)`

## Imagery

Imagery is functional and abstract, avoiding lifestyle photography. The primary visuals are clean product UI screenshots contained within minimalist browser or app frames. Secondary visuals consist of abstract data visualizations, like the grid of grayscale profile pictures, which serve as atmospheric graphics rather than literal content. All imagery is rendered with sharp edges and presented in a clean, isolated manner.

## Layout

The layout is built on a centered, max-width (1440px) model, creating generous white space on the peripheries. Hero sections are minimal, typically a large, centered headline stack. Page content follows a predictable rhythm of stacked, centered sections or simple two-column layouts. A key structural element is the large, embedded product UI demonstration, which acts as the visual centerpiece. Navigation is contained within a simple, sticky top bar.

## Agent Prompt Guide

### Quick Color Reference
- **Primary Text:** `#1c1815` (Warm Ink)
- **Secondary Text:** `#8c8072` (Overcast)
- **Page Background:** `#faf8f5` (Canvas) / `#ffffff` (White Surface)
- **Primary CTA:** bg `#1c1815` (Warm Ink), text `#ffffff` (White)
- **Border:** `#d1c4b4` (Rustshade / Slate)
- **Interactive Accent:** `#b45039` (Claytile)

### Example Component Prompts
1.  **Hero Section:** Create a hero with a warm canvas (#faf8f5) or pure white background. Main headline: 'Customer relationship magic.', font Tiempos Text, size 64px, weight 500, color Warm Ink (#1c1815), line-height 1.07, letter-spacing -1.28px. Sub-headline below: 'Precision service management platform.', font Inter, size 20px, weight 500, color Metal (#6d6255), letter-spacing -0.4px. Centered layout.

2.  **Primary CTA Button:** Create a button with the text 'Start for free'. Background color Warm Ink (#1c1815), text color White (#ffffff). Font is 14px Inter at weight 500. Padding is 8px top/bottom and 12px left/right. Border-radius is 10px. Hover: #2e2621.

3.  **UI Frame Card:** Create a card with a White (#ffffff) background, 16px padding on all sides, an 8px border-radius, and a 1px solid border of color Stone (#e6dfd5). Apply the shadow style: `rgba(28, 24, 21, 0.08) 0px 2px 3px -2px, rgba(28, 24, 21, 0.04) 0px 4px 6px -2px`.

## Similar Brands

- **Linear** — Shares the stark monochrome palette, application-focused aesthetic, and use of a sans-serif for UI with disciplined restraint.
- **Notion** — Similar typographic approach, blending a serif for headlines with a sans-serif for UI, creating a sophisticated yet functional feel.
- **Vercel** — Matches the developer-tool feel, high-contrast text on white/black backgrounds, and extremely limited use of accent color.
- **Pitch** — Comparable use of a soft, modern serif for headlines against a clean, sans-serif driven interface.

## Quick Start

### CSS Custom Properties

```css
:root {
  /* Colors */
  --color-white: #ffffff;
  --color-ash: #f4f0ea;
  --color-stone: #e6dfd5;
  --color-slate: #d1c4b4;
  --color-lead: #b5a897;
  --color-overcast: #8c8072;
  --color-metal: #6d6255;
  --color-carbon: #4d443a;
  --color-ink: #1c1815;
  --color-abyss: #100d0b;
  --color-claytile: #b45039;
  --color-rustshade: #d1c4b4;
  --color-action-blue: #b45039;
  --color-focus-blue: rgba(180, 80, 57, 0.35);
  --color-success-green: #1b5e39;
  --color-danger-red: #8f2824;
  --color-warning-yellow: #875200;
  --color-magic-aura: #b45039;
  --gradient-magic-aura: linear-gradient(131.88deg, #d1c4b4 7.36%, #b45039 81.74%);

  /* Typography — Font Families */
  --font-tiempos-text: 'Tiempos Text', ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
  --font-inter-display: 'Inter Display', ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
  --font-inter: 'Inter', ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;

  /* Typography — Scale */
  --text-caption: 12px;
  --leading-caption: 1.5;
  --tracking-caption: -0.14px;
  --text-body-sm: 14px;
  --leading-body-sm: 1.43;
  --tracking-body-sm: -0.14px;
  --text-body: 16px;
  --leading-body: 1.5;
  --tracking-body: -0.24px;
  --text-subheading: 20px;
  --leading-subheading: 1.3;
  --tracking-subheading: -0.4px;
  --text-heading-sm: 28px;
  --leading-heading-sm: 1.23;
  --tracking-heading-sm: -0.42px;
  --text-heading: 40px;
  --leading-heading: 1.1;
  --tracking-heading: -0.6px;
  --text-heading-lg: 56px;
  --leading-heading-lg: 1.1;
  --tracking-heading-lg: -1.12px;
  --text-display: 64px;
  --leading-display: 1.07;
  --tracking-display: -1.28px;

  /* Typography — Weights */
  --font-weight-regular: 400;
  --font-weight-medium: 500;
  --font-weight-semibold: 600;
  --font-weight-bold: 700;

  /* Spacing */
  --spacing-unit: 4px;
  --spacing-4: 4px;
  --spacing-8: 8px;
  --spacing-12: 12px;
  --spacing-16: 16px;
  --spacing-20: 20px;
  --spacing-24: 24px;
  --spacing-28: 28px;
  --spacing-32: 32px;
  --spacing-36: 36px;
  --spacing-40: 40px;
  --spacing-44: 44px;
  --spacing-48: 48px;
  --spacing-60: 60px;
  --spacing-80: 80px;
  --spacing-100: 100px;
  --spacing-120: 120px;

  /* Layout */
  --page-max-width: 1440px;
  --section-gap: 96px;
  --card-padding: 16px;
  --element-gap: 8px;

  /* Border Radius */
  --radius-md: 7px;
  --radius-xl: 12px;

  /* Named Radii */
  --radius-tabs: 0px;
  --radius-tags: 4px;
  --radius-cards: 8px;
  --radius-inputs: 7px;
  --radius-buttons: 10px;

  /* Shadows */
  --shadow-subtle: rgb(238, 239, 241) 0px 0px 0px 1px inset;
  --shadow-subtle-2: rgba(28, 40, 64, 0.1) 0px 2px 3px -2px, rgba(28, 40, 64, 0.04) 0px 4px 6px -2px;
  --shadow-subtle-3: rgba(28, 40, 64, 0.18) 0px 0px 2px 0px, rgba(0, 0, 0, 0.04) 0px 1px 3px 0px;
}
```

### Tailwind v4

```css
@theme {
  /* Colors */
  --color-white: #ffffff;
  --color-ash: #f4f0ea;
  --color-stone: #e6dfd5;
  --color-slate: #d1c4b4;
  --color-lead: #b5a897;
  --color-overcast: #8c8072;
  --color-metal: #6d6255;
  --color-carbon: #4d443a;
  --color-ink: #1c1815;
  --color-abyss: #100d0b;
  --color-claytile: #b45039;
  --color-rustshade: #d1c4b4;
  --color-action-blue: #b45039;
  --color-focus-blue: rgba(180, 80, 57, 0.35);
  --color-success-green: #1b5e39;
  --color-danger-red: #8f2824;
  --color-warning-yellow: #875200;
  --color-magic-aura: #b45039;

  /* Typography */
  --font-tiempos-text: 'Tiempos Text', ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
  --font-inter-display: 'Inter Display', ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
  --font-inter: 'Inter', ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;

  /* Typography — Scale */
  --text-caption: 12px;
  --leading-caption: 1.5;
  --tracking-caption: -0.14px;
  --text-body-sm: 14px;
  --leading-body-sm: 1.43;
  --tracking-body-sm: -0.14px;
  --text-body: 16px;
  --leading-body: 1.5;
  --tracking-body: -0.24px;
  --text-subheading: 20px;
  --leading-subheading: 1.3;
  --tracking-subheading: -0.4px;
  --text-heading-sm: 28px;
  --leading-heading-sm: 1.23;
  --tracking-heading-sm: -0.42px;
  --text-heading: 40px;
  --leading-heading: 1.1;
  --tracking-heading: -0.6px;
  --text-heading-lg: 56px;
  --leading-heading-lg: 1.1;
  --tracking-heading-lg: -1.12px;
  --text-display: 64px;
  --leading-display: 1.07;
  --tracking-display: -1.28px;

  /* Spacing */
  --spacing-4: 4px;
  --spacing-8: 8px;
  --spacing-12: 12px;
  --spacing-16: 16px;
  --spacing-20: 20px;
  --spacing-24: 24px;
  --spacing-28: 28px;
  --spacing-32: 32px;
  --spacing-36: 36px;
  --spacing-40: 40px;
  --spacing-44: 44px;
  --spacing-48: 48px;
  --spacing-60: 60px;
  --spacing-80: 80px;
  --spacing-100: 100px;
  --spacing-120: 120px;

  /* Border Radius */
  --radius-md: 7px;
  --radius-xl: 12px;

  /* Shadows */
  --shadow-subtle: rgb(238, 239, 241) 0px 0px 0px 1px inset;
  --shadow-subtle-2: rgba(28, 40, 64, 0.1) 0px 2px 3px -2px, rgba(28, 40, 64, 0.04) 0px 4px 6px -2px;
  --shadow-subtle-3: rgba(28, 40, 64, 0.18) 0px 0px 2px 0px, rgba(0, 0, 0, 0.04) 0px 1px 3px 0px;
}
```

---

# Service Maintenance Web Application Design Extension

> Product UI direction for the Laptop/PC Service Maintenance MVP. This section extends the visual system above into a practical application design guide for ticketing, kanban operations, customer management, inventory, reporting, settings, and deployment-ready daily shop usage.

## Product Design North Star

The application should feel like a precise operational console for a small repair shop: calm, fast, legible, and trustworthy. It should avoid the generic "AI dashboard" look. The interface must look intentionally designed, not over-decorated.

The product is not a marketing website. It is a daily-use operations system. Prioritize:

- Fast ticket intake
- Clear repair status visibility
- Low-friction customer communication
- Strong auditability
- Safe inventory handling
- Reliable printing/export flows
- Minimal visual noise

The UI should feel compact, monochrome, and tool-like, while still being approachable for non-technical shop staff.

## Application Personality

Use the existing Attio-inspired visual direction as the baseline:

- High-contrast monochrome
- Minimal accent color
- Precise borders
- Compact spacing
- Clear typography hierarchy
- Subtle rounded controls
- No heavy shadows
- No saturated dashboard colors
- No unnecessary illustrations
- No glassmorphism
- No gradient cards except rare decorative empty states

The app should feel closer to a premium internal tool than a colorful POS system.

## Core Product Areas

The MVP web application consists of the following primary areas:

```text
Dashboard
Service Tickets
Kanban Board
Customers
Inventory
Reports
Settings
User & Role Management
Public Tracking Page
Print Layouts
```

Recommended navigation order:

```text
Dashboard
Tickets
Kanban
Customers
Inventory
Reports
Settings
```

If the user has a technician role, prioritize:

```text
Kanban
My Tickets
Inventory
```

If the user has an owner/admin role, show all modules.

---

# Application Layout

## Overall Shell

Use a persistent app shell after login.

Recommended desktop layout:

```text
┌─────────────────────────────────────────────────────────────┐
│ Top Header                                                   │
├───────────────┬─────────────────────────────────────────────┤
│ Sidebar       │ Main Content                                │
│ Navigation    │                                             │
│               │                                             │
└───────────────┴─────────────────────────────────────────────┘
```

Recommended mobile/tablet layout:

```text
┌────────────────────────────────────┐
│ Top Header                         │
├────────────────────────────────────┤
│ Main Content                       │
├────────────────────────────────────┤
│ Bottom Navigation / Drawer Trigger │
└────────────────────────────────────┘
```

## Sidebar

### Purpose

The sidebar should provide stable navigation without competing with the work area.

### Visual Design

- Width: 220px to 248px on desktop
- Background: White
- Border-right: 1px solid Stone
- Padding: 12px
- Navigation item radius: 10px
- Navigation item height: 36px to 40px
- Active item background: Ash
- Active item text: Ink
- Inactive item text: Metal
- Icon color: Carbon or Metal
- Avoid colorful icons

### Sidebar Sections

```text
Main
- Dashboard
- Tickets
- Kanban

Operations
- Customers
- Inventory
- Reports

System
- Settings
```

For small screens, replace sidebar with:

- Header menu button
- Slide-over navigation drawer
- Optional bottom navigation for 3 key items: Dashboard, Kanban, Tickets

## Header

### Purpose

The header should provide context, fast actions, search, and user controls.

### Recommended Header Structure

```text
Left:
- Current page title
- Optional breadcrumb

Center:
- Global search input

Right:
- New Ticket button
- Notification/activity button
- User menu
```

### Header Design

- Height: 56px to 64px
- Background: White with subtle bottom border
- Sticky at top
- Border-bottom: 1px solid Stone
- No heavy shadow
- Use compact spacing

### Header Actions

Primary action should usually be:

```text
+ New Ticket
```

Secondary actions depend on page:

```text
Export
Print
Filter
Settings
```

### Breadcrumb

Use breadcrumb only for deep pages:

```text
Tickets / SRV-000123
Customers / Budi Santoso
Inventory / SSD 256GB
```

Do not use breadcrumb on shallow pages like Dashboard or Kanban.

---

# Dashboard Design

## Purpose

The dashboard is not a vanity analytics page. It should answer:

```text
What needs attention today?
What is waiting?
What is blocked?
What is ready to be picked up?
What changed recently?
```

## Recommended Sections

### Top Metrics

Use compact metric cards:

```text
Open Tickets
Waiting Approval
Waiting Sparepart
Ready for Pickup
Overdue Tickets
Today Completed
```

Metric card design:

- White background
- 1px Stone border
- 8px radius
- 16px padding
- Label: 12px Overcast
- Value: 28px or 32px Inter Display
- Optional small trend text: 12px Metal

Avoid colorful metric cards. Use small semantic dots or tiny badges only.

### Attention Queue

A focused list of tickets needing action:

```text
- Waiting customer approval
- Waiting sparepart
- Blocked
- Overdue
- Ready but not picked up
```

### Recent Activity

Show chronological activity:

```text
09:31 — Ticket SRV-000124 moved to In Progress
09:14 — Customer approved estimate
08:42 — New ticket created
```

Activity rows should be compact and readable.

---

# Ticket Management Design

## Ticket List Page

### Purpose

The ticket list is for searching, filtering, and reviewing service jobs.

### Layout

```text
Page Header
Filter Bar
Ticket Table / Ticket List
Pagination
```

### Filter Bar

Recommended filters:

```text
Search by ticket ID, customer name, phone, device
Status
Priority
Technician
Date range
Overdue only
Ready for pickup
```

Design:

- Use compact inputs
- Keep common filters visible
- Move advanced filters into a popover or collapsible section
- Provide a clear "Reset filters" button

### Ticket Table Columns

Recommended desktop columns:

```text
Ticket ID
Customer
Device
Issue Summary
Status
Priority
Technician
Created At
Last Updated
Actions
```

Actions:

```text
View
WhatsApp
Print
```

For mobile, use stacked cards instead of a wide table.

### Ticket Row Design

- Ticket ID should be visually prominent
- Status badge should be visible but not loud
- Customer phone should be one click/tap away
- Long issue text should be truncated
- Row hover: Ash background
- Selected row: subtle inset border or Ash background

---

# Ticket Detail Page

## Purpose

Ticket detail is the operational source of truth for one service job.

## Recommended Layout

Desktop:

```text
┌──────────────────────────────────────────────┬─────────────────────┐
│ Main Ticket Information                      │ Right Panel          │
│ - Device & complaint                         │ - Status             │
│ - Diagnosis                                  │ - Customer           │
│ - Work notes                                 │ - Actions            │
│ - Inventory usage                            │ - QR / Print         │
│ - Attachments                                │ - Timeline Summary   │
├──────────────────────────────────────────────┴─────────────────────┤
│ Full Activity Timeline                                               │
└──────────────────────────────────────────────────────────────────────┘
```

Mobile:

```text
Summary
Actions
Status
Customer
Device
Diagnosis
Inventory
Attachments
Timeline
```

## Ticket Header

Show:

```text
SRV-000123
Asus A455L — Keyboard not working
Status badge
Priority badge
Created date
```

Primary actions:

```text
Update Status
Send WhatsApp
Print Nota
Edit Ticket
```

Danger actions:

```text
Cancel Ticket
Mark Blocked
```

Danger actions must be separated visually and require confirmation.

## Status Panel

Status panel should show:

```text
Current status
Allowed next statuses
Assigned technician
SLA / overdue indicator
Last updated time
```

Status transition button should not expose impossible transitions.

Example:

```text
Current: Waiting Approval
Next:
- Start Repair
- Cancel
- Block
```

Do not allow invalid transitions like:

```text
Received -> Picked Up
```

## Activity Timeline

Each important action should be shown in timeline format:

```text
Status changed
From: Inspection
To: Waiting Approval
By: Admin
At: 2026-06-23 14:30
Note: Estimated repair cost sent to customer.
```

Timeline item design:

- Small timestamp
- Action title
- Actor
- Optional old/new values
- Optional note
- No unnecessary icons

---

# New Ticket Intake Flow

## Design Goal

Creating a ticket must be fast enough for front-desk use while the customer is physically waiting.

Target completion time:

```text
Under 2 minutes for common cases
```

## Recommended Flow

Use a single-page form with clearly grouped sections.

```text
1. Customer
2. Device
3. Complaint & Initial Diagnosis
4. Accessories Left
5. Physical Condition
6. Photos
7. Confirmation & Print
```

Avoid multi-step wizard for MVP unless the form becomes too long. Front desk staff usually needs speed and visibility.

## Form Section 1: Customer

Fields:

```text
Phone number / WhatsApp
Customer name
Customer type
Internal note
Customer flags
```

Behavior:

- Phone number is the primary lookup key
- If phone exists, auto-fill customer data
- If customer is banned, show strong warning
- If customer is star customer, show subtle positive badge
- Prevent duplicate customer creation unless confirmed

Banned customer warning:

```text
This customer is marked as banned.
Reason: Payment dispute on previous ticket.
Only Admin/Owner can continue.
```

## Form Section 2: Device

Fields:

```text
Device type
Brand
Model
Serial number
Device color
Password/PIN note
```

Design notes:

- Device type and brand should be selectable
- Allow custom brand/model entry
- Do not require serial number
- Password/PIN should be optional and treated as sensitive

Sensitive data rule:

- Mask password/PIN by default
- Restrict visibility by role
- Add warning text: "Only store this if required for repair."

## Form Section 3: Complaint & Diagnosis

Fields:

```text
Customer complaint
Initial diagnosis
Estimated completion date
Priority
Assigned technician
```

Complaint field should be textarea with suggested quick chips:

```text
No display
Slow performance
Cannot boot
Keyboard issue
Battery issue
Overheating
Blue screen
No charging
```

## Form Section 4: Accessories Left

Use checkbox chips:

```text
Charger
Bag
Mouse
Keyboard
Dongle
Power cable
Box
Other
```

For each selected accessory, allow optional note:

```text
Charger: original Asus charger, black
Bag: grey backpack
```

## Form Section 5: Physical Condition

Use quick checklist:

```text
Screen cracked
Body dent
Missing screw
Hinge issue
Keyboard key missing
Liquid damage
Scratches
No visible damage
```

Allow additional condition notes.

This protects the shop from disputes.

## Form Section 6: Photos

Photo upload areas:

```text
Front
Back
Keyboard
Screen
Accessories
Other
```

Rules:

- Max 2-5 MB per image
- Compress before upload if possible
- Show thumbnail preview
- Allow remove before submit
- Do not block ticket creation if photos are skipped

## Form Section 7: Confirmation

Before submit, show compact summary:

```text
Customer
Device
Complaint
Accessories
Physical condition
Initial status
```

Actions:

```text
Create Ticket
Create & Print Nota
Cancel
```

Default primary action:

```text
Create & Print Nota
```

because this is the real shop flow.

---

# Form Design System

## General Form Rules

- Labels always visible
- Do not rely only on placeholder text
- Required fields marked with small `Required` label or asterisk
- Error messages appear directly below fields
- Helper text uses Overcast
- Keep forms compact but not cramped
- Group related fields in cards/sections
- Use one primary action per form

## Input Sizes

Recommended:

```text
Default input height: 36px to 40px
Textarea min height: 96px
Select height: 36px to 40px
Checkbox row height: 32px
```

## Field Layout

Desktop:

- Use 2-column grid for short fields
- Use full width for textarea
- Use 3-column grid only for very short fields

Mobile:

- Single column only
- Full-width buttons

## Validation

Validation should be direct and specific.

Bad:

```text
Invalid input
```

Good:

```text
Phone number must start with 08 or +62.
```

Use inline validation and prevent destructive submit errors.

## Empty Fields

Avoid placeholder-only design.

Example:

```text
Label: Customer complaint
Placeholder: Example: Laptop cannot boot after Windows update
```

## Required MVP Fields

New ticket minimum required:

```text
Customer name
Phone number
Device type
Complaint
Accessories confirmation
Initial status
```

---

# Modal and Dialog Design

## Modal Usage

Use modals for focused, interruptive tasks:

```text
Change status
Send WhatsApp message
Confirm cancellation
Add inventory item to ticket
Print nota preview
Create customer note
```

Do not use modals for complex full-page forms unless necessary.

## Modal Sizes

```text
Small: confirmation dialogs
Medium: status update, WhatsApp preview
Large: print preview, inventory selection
Fullscreen/mobile: new ticket form if opened from mobile
```

## Modal Structure

```text
Header
Description
Body
Footer actions
```

Header:

- Title: 18px or 20px Inter Display/Inter
- Optional subtitle: 14px Metal

Footer:

- Secondary button on the left or first
- Primary action on the right
- Destructive action should use Danger styling

## Modal Behavior

- Close with Esc
- Close with overlay click only for non-destructive modals
- Do not close destructive modal accidentally
- Return focus to triggering button
- Trap focus inside modal
- Show loading state on submit
- Prevent double submit

## Confirmation Dialogs

Use explicit language.

Bad:

```text
Are you sure?
```

Good:

```text
Cancel ticket SRV-000123?
This will mark the ticket as cancelled and require a cancellation reason.
```

For destructive actions, require reason when relevant.

---

# Button Design System

## Button Hierarchy

### Primary Button

Use for the main page action.

Examples:

```text
Create Ticket
Save Changes
Create & Print Nota
Update Status
```

Design:

- Background: Ink
- Text: White
- Border: Ink
- Radius: 10px
- Height: 36px to 40px
- Font: 14px Inter Medium

### Secondary Button

Use for supporting actions.

Examples:

```text
Cancel
Back
Preview
Export
Print
```

Design:

- Background: White
- Text: Ink
- Border: Slate
- Radius: 10px

### Tertiary/Ghost Button

Use for low-emphasis actions.

Examples:

```text
Reset filters
View details
Copy link
```

Design:

- Background: transparent
- Text: Metal
- Hover background: Ash

### Destructive Button

Use for destructive actions only.

Examples:

```text
Cancel Ticket
Delete Attachment
Remove Inventory Item
```

Design:

- Background: White or Danger Red depending severity
- Text: Danger Red or White
- Border: Danger Red
- Require confirmation

## Button Rules

- One primary button per section/modal
- Avoid multiple black buttons in one view
- Use icons only when they improve scanability
- Buttons must have loading state
- Disabled state must include reason when not obvious
- Do not use color-filled buttons for statuses

---

# Status, Badge, and Tag Design

## Status Badge Design

Use restrained semantic color.

Recommended style:

```text
Background: very light semantic tint or Ash
Text: semantic dark color or Ink
Border: subtle semantic border or Stone
Radius: 4px
Font: 12px Inter Medium
```

## Suggested Status Colors

Keep colors muted and limited:

```text
Received: Ash / Ink
Inspection: Ash / Carbon
Waiting Approval: Warning Yellow
In Progress: Action Blue
Waiting Sparepart: Warning Yellow
Done: Success Green
Picked Up: Success Green
Cancelled: Danger Red
Blocked: Danger Red
```

Avoid bright backgrounds. Use small colored dot + monochrome badge if possible.

## Priority Badges

```text
Low
Normal
High
Urgent
```

Use priority carefully. If every ticket is urgent, the system becomes useless.

---

# Kanban Board Design

## Purpose

The kanban board is the primary operational view.

It should answer:

```text
Where is every device right now?
What needs action?
Which jobs are blocked?
Which jobs are ready for pickup?
```

## Layout

Desktop:

```text
Horizontal columns by status
Cards inside each column
Sticky column headers
```

Mobile:

```text
Status tabs
Single-column ticket list per selected status
```

## Column Design

Column header:

```text
Status name
Ticket count
Optional SLA/attention count
```

Column card:

```text
Ticket ID
Customer name
Device
Short complaint
Priority
Technician
Age / last updated
Small action row
```

## Kanban Card Design

Card style:

- Background: White
- Border: 1px solid Stone
- Radius: 8px
- Padding: 12px
- Hover border: Slate
- Avoid heavy shadows

Important visual hierarchy:

```text
Ticket ID
Device + customer
Issue summary
Status metadata
```

## Drag and Drop

MVP can start without drag-and-drop. Use status update button/menu first.

If drag-and-drop is implemented:

- Validate transition rules
- Show confirmation if note/reason required
- Revert card if transition fails
- Record audit history
- Do not allow dragging into invalid status

## Quick Actions on Card

Recommended:

```text
Open
WhatsApp
Update status
```

Avoid too many actions on cards.

---

# Customer Management Design

## Customer List

Columns:

```text
Name
Phone
Flags
Total Tickets
Last Service
Last Contact
Actions
```

Customer flags:

```text
Star Customer
Banned
Has unpaid ticket
Repeat customer
```

## Customer Detail

Recommended sections:

```text
Profile
Flags & internal notes
Devices
Service history
Payment/invoice references
Activity timeline
```

## Banned Customer Design

Banned customer should be highly visible but not visually chaotic.

Show:

```text
Banned customer
Reason
Banned by
Date
Admin-only action to remove ban
```

Use Danger Red sparingly.

## Star Customer Design

Use a small monochrome star icon or subtle label.

Avoid making the UI look like a gamified loyalty app.

---

# Inventory Design

## Inventory List

Columns:

```text
SKU
Item Name
Category
Condition
Stock
Reserved
Available
Minimum Stock
Selling Price
Actions
```

## Inventory Item Detail

Sections:

```text
Item summary
Stock movement ledger
Linked service tickets
Supplier notes
Price history
```

## Stock Movement Ledger

This is more important than a simple stock number.

Movement types:

```text
IN
OUT
RESERVED
RELEASED
ADJUSTMENT
RETURNED
```

Ledger row:

```text
Date
Type
Quantity
Reference
Actor
Note
```

## Low Stock Warning

Use subtle warning badge:

```text
Low stock
Available: 1
Minimum: 3
```

Do not use large red banners unless stock is zero and required for active repair.

---

# Reports Design

## Report Pages

MVP reports:

```text
Daily Service Report
Weekly Service Report
Monthly Service Report
Ticket Status Summary
Inventory Movement Report
Technician Activity Report
```

## Report Layout

```text
Filter Bar
Summary Cards
Table
Export Actions
```

Export actions:

```text
Export Excel
Export PDF
Export CSV
```

Excel is more important than PDF for MVP.

## Report Visuals

Avoid overusing charts.

Use charts only for:

```text
Ticket count over time
Status distribution
Revenue estimate over time
```

For operational reporting, tables are usually more useful.

---

# Settings Design

## Settings Navigation

Use grouped settings pages:

```text
General
Service Workflow
Ticket Numbering
Print Templates
WhatsApp Templates
Inventory
Users & Roles
Integrations
Backup & System
```

## General Settings

Fields:

```text
Store name
Store logo
Address
Phone/WhatsApp
Timezone
Default language
```

## Service Workflow Settings

Fields:

```text
Statuses
Allowed transitions
Default status
Final statuses
Required note rules
SLA rules
```

Use a table/list editor instead of raw JSON.

## Ticket Numbering Settings

Fields:

```text
Prefix
Number padding
Reset rule
Example preview
```

Example:

```text
Prefix: SRV
Padding: 6
Preview: SRV-000123
```

## Print Template Settings

Fields:

```text
Nota size
Header text
Footer text
Terms & conditions
Show QR code
Show accessories
Show physical condition
```

## WhatsApp Template Settings

Fields:

```text
Template name
Trigger event
Message body
Variables
Enabled
```

Variables:

```text
{{customer_name}}
{{ticket_number}}
{{device_name}}
{{status}}
{{tracking_url}}
{{store_name}}
```

## Integration Settings

MVP:

```text
WhatsApp link mode
```

Future:

```text
WhatsApp API provider
Paper.id API
Cloud storage
```

Do not expose raw API secrets after saving. Show masked value.

---

# Print and Document Design

## Service Receipt / Nota Servis

The printed receipt must be clear and legally useful.

Recommended sections:

```text
Store identity
Ticket ID + QR Code
Customer information
Device information
Complaint
Accessories left
Physical condition
Initial diagnosis
Terms & conditions
Received by
Customer signature area
```

## Print Layout

Support:

```text
A4
Thermal 58mm
Thermal 80mm
```

MVP can start with A4 and 80mm thermal.

## Visual Style

Printed document should be mostly monochrome.

Use:

- Clear headings
- Thin dividers
- Compact tables
- QR code
- No decorative graphics
- No heavy background blocks

---

# Public Tracking Page Design

## Purpose

The public tracking page allows customers to scan QR and check service progress without login.

## URL Pattern

```text
/track/{ticketNumber}
/track/{publicToken}
```

Prefer public token if possible to avoid exposing sequential IDs.

## Visible Information

Show only safe information:

```text
Ticket number
Device type/brand/model
Current status
Last updated
General diagnosis/status note
Store WhatsApp button
Pickup readiness
```

Do not show:

```text
Internal notes
Technician private notes
Cost breakdown unless approved
Customer sensitive data
Device password/PIN
Inventory cost
```

## Public Page Design

- Minimal header with store name
- Large current status
- Simple timeline
- WhatsApp contact button
- Footer with store address/contact

This page should be mobile-first.

---

# Empty States

## Empty State Design

Use calm, useful empty states.

Example for no tickets:

```text
No service tickets yet.
Create the first ticket when a customer leaves a device for inspection.
[Create Ticket]
```

Example for no inventory:

```text
No inventory items yet.
Add spareparts, second-hand parts, accessories, or PC packages.
[Add Inventory Item]
```

Avoid cute illustrations and generic AI-looking graphics.

---

# Loading, Error, and Offline States

## Loading States

Use:

- Skeleton rows for tables
- Skeleton cards for kanban
- Button spinner for submit
- Avoid full-page spinners except initial app load

## Error States

Errors should explain what happened and what to do next.

Example:

```text
Failed to update ticket status.
The selected transition is not allowed from the current status.
```

## Offline/Connection Issue

Show persistent top banner:

```text
Connection problem. Changes may not be saved.
```

If offline support is not implemented, do not pretend changes are saved.

---

# Responsive Design Rules

## Desktop

Target:

```text
1366px and above
```

Use:

- Sidebar
- Dense tables
- Multi-column forms
- Full kanban board

## Tablet

Target:

```text
768px to 1024px
```

Use:

- Collapsible sidebar
- 1-2 column forms
- Kanban with horizontal scroll or status tabs

## Mobile

Target:

```text
360px to 767px
```

Use:

- Single-column layout
- Cards instead of tables
- Full-width buttons
- Bottom sticky action bar for critical forms
- Status tabs instead of full kanban

---

# Accessibility Rules

Minimum requirements:

- Keyboard navigable
- Visible focus states
- Proper labels for all inputs
- Dialog focus trap
- Color is not the only status indicator
- Error messages linked to fields
- Buttons have descriptive labels
- Tables have headers
- QR page readable on mobile

Focus ring should use Focus Blue from the token set.

---

# Data Density Guidelines

This app should be compact, but not cramped.

Use compact density for:

```text
Tables
Kanban cards
Activity timeline
Settings lists
```

Use more breathing room for:

```text
New ticket form
Ticket detail page
Print preview
Public tracking page
```

Do not make the dashboard look like a crypto trading terminal.

---

# Recommended Page Inventory

## Auth

```text
/login
/forgot-password
/reset-password
```

## App

```text
/dashboard
/tickets
/tickets/new
/tickets/{id}
/kanban
/customers
/customers/{id}
/inventory
/inventory/{id}
/reports
/settings
```

## Public

```text
/track/{token}
```

---

# Agent Implementation Guide for UI

When generating UI code, follow these instructions:

1. Use the design tokens defined in this document.
2. Use monochrome-first design.
3. Use Action Blue only for links, focus, and selected interactive states.
4. Use semantic colors only for status, warning, success, and destructive indicators.
5. Avoid decorative gradients in operational screens.
6. Prefer borders and spacing over shadows.
7. Prefer real data tables and forms over generic cards.
8. Create reusable components before composing pages.
9. Every form must have loading, error, disabled, and empty states.
10. Every destructive action must have confirmation.
11. Every status update must create an audit event.
12. Every public tracking page must hide internal/sensitive data.

## Suggested Component Tree

```text
src/lib/components
├── layout
│   ├── AppShell.svelte
│   ├── Sidebar.svelte
│   ├── Header.svelte
│   ├── PageHeader.svelte
│   └── Footer.svelte
├── ui
│   ├── Button.svelte
│   ├── Input.svelte
│   ├── Textarea.svelte
│   ├── Select.svelte
│   ├── Checkbox.svelte
│   ├── Modal.svelte
│   ├── Badge.svelte
│   ├── Card.svelte
│   ├── Table.svelte
│   ├── Tabs.svelte
│   ├── DropdownMenu.svelte
│   ├── Toast.svelte
│   └── EmptyState.svelte
├── tickets
│   ├── TicketForm.svelte
│   ├── TicketSummaryCard.svelte
│   ├── TicketStatusBadge.svelte
│   ├── TicketTimeline.svelte
│   ├── TicketPrintPreview.svelte
│   └── StatusTransitionModal.svelte
├── kanban
│   ├── KanbanBoard.svelte
│   ├── KanbanColumn.svelte
│   └── KanbanCard.svelte
├── customers
│   ├── CustomerForm.svelte
│   ├── CustomerFlags.svelte
│   └── CustomerHistory.svelte
├── inventory
│   ├── InventoryForm.svelte
│   ├── InventoryMovementLedger.svelte
│   └── StockBadge.svelte
└── reports
    ├── ReportFilters.svelte
    ├── ReportSummaryCards.svelte
    └── ExportActions.svelte
```

---

# Visual Anti-Patterns

Do not create:

- Neon gradients
- Random pastel cards
- Oversized icons
- Fake glass panels
- Floating blobs
- Colorful sidebars
- Unnecessary charts
- Huge empty dashboards
- Placeholder-heavy forms
- Modal inside modal flows
- Button rows with many primary buttons
- Unclear destructive actions
- Tables without pagination
- Public pages exposing internal data

---

# MVP Design Acceptance Criteria

The MVP UI is considered ready when:

```text
[ ] Staff can create a service ticket in under 2 minutes
[ ] Ticket receipt can be printed clearly
[ ] Kanban shows all active service work by status
[ ] Staff can update ticket status safely
[ ] Invalid status transitions are prevented
[ ] Ticket timeline shows important history
[ ] Staff can contact customer via WhatsApp link
[ ] Customer list shows flags and service history
[ ] Inventory list shows stock, reserved, and available quantities
[ ] Reports can be filtered and exported
[ ] Settings allow workflow/status/template configuration
[ ] Public tracking page is mobile-friendly
[ ] UI works on desktop, tablet, and mobile
[ ] Forms have validation, loading, and error states
[ ] Destructive actions require confirmation
[ ] Sensitive data is hidden from public pages
```

---

# Final Design Direction

This system should feel like a calm command center for a repair shop.

The best UI is not the flashiest one. The best UI is the one where staff can receive a laptop, print the receipt, track the work, contact the customer, and close the ticket without thinking too hard.

Design for trust, speed, and operational clarity.

