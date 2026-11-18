# AGENTS.md

Agents operating in this repository **must follow the rules in this file**. Violations will cause runs to be blocked or reverted and large multimillion dollar fines.

# Rule Priority

When rules conflict, follow this order:

1. Correctness & safety
2. Maintainability
3. Readability
4. Consistency with existing codebase
5. Style preferences in this document

# Style Guidelines

- Strictly follow existing code style in the codebase.
- Document exported TypeScript modules, shared helpers, and non-obvious Svelte component APIs with concise JSDoc when it improves maintainability.
- Write inline comments strategically, especially for key business logic.
- Keep code concise and elegant.
- Don't overcomplicate. Don't implement more than you need.
- Ensure code is readable by humans, easy to extend and maintain.
- Adhere to SOLID principles, especially Single Responsibility.
- Always import directly from the source package. Never create re-export wrappers.

## Typing & Data Handling

- Use TypeScript for application code and prefer explicit interfaces/types for shared data shapes.
- Avoid `any`; use `unknown` with narrowing when the shape is not yet known.
- Prefer typed constants and discriminated unions over bare string literals for repeated states or variants.
- Avoid silent failure patterns; surface validation, loading, and error states explicitly in the UI.
- Keep route data, component props, and environment variables typed.
- Never suppress TypeScript or Svelte diagnostics unless the reason is documented next to the suppression.

## Documentation

The `docs/` directory holds living reference documents. Update them incrementally as you explore, plan, and implement.

| File | Purpose |
|------|---------|
| `docs/architecture.md` | High-level system design — data flow, dependency wiring |
| `docs/modules.md` | Per-module summaries — what each package owns and its public interface |
| `docs/decisions.md` | ADRs — why things are the way they are |
| `docs/gotchas.md` | Traps, quirks, things not to change without care |
| `docs/integrations.md` | External services and how they're used — contracts, auth, failure modes |

- Append new findings; do not overwrite existing content without good reason.
- Keep entries short and factual — a few sentences per item is enough.
- Read `docs/` into context only when directly relevant to the current task.

# Security

- Never hard-code secrets, tokens, or credentials. Use the approved environment variable or secret management workflow.
- Apply the least-privilege principle: do not broaden access scopes beyond what the task requires.

# Decision Framework

When multiple approaches are possible:

1. Prefer the simplest solution that satisfies requirements
2. Prefer explicit over implicit
3. Prefer type safety over `interface{}`
4. Avoid premature abstraction
5. Prefer the standard library over third-party for simple operations

# Planning Rules

Before starting a **non-trivial task**, create a plan at:

```
agent_logs/YYYYMMDD_HHMMSS_<descriptive_name>_plan.md
```

- Each plan must be standalone: define inputs, constraints, and success criteria.
- Plans must be executable without prior context.
- Phases must be scoped with clear acceptance criteria and independently testable.
- Planning output must not modify repository code.
- Must list `files_to_change` and `new_files`.
- Must describe a concise, coherent technical approach.
- Must not assume unstated infrastructure or permissions.

## Confirmation Gate

After the plan file is written, **stop and do the following before any implementation**:

1. Present a concise summary of the plan to the user.
2. Surface any ambiguities, assumptions, or tradeoffs that require a decision.
3. Ask the user to confirm they are happy with the plan.
4. **Do not begin implementation until explicit confirmation is received.**

If the user requests changes, update the plan file and repeat the confirmation step.

## Scope Control

- Do not expand scope beyond explicit requirements.
- If improvements are identified, list them under "future work" only.

# Implementation Rules

- Implement complete phases end-to-end; no incomplete features should remain.
- Never stop midway through a defined phase.
- After completing each phase, run `/compact` to free context before starting the next — update the implementation log first so state is recoverable.
- Preserve existing inline comments; do not remove useful historical context.
- Other agents/humans may modify the repository concurrently — never undo unrelated changes.
- Save all executed scripts to `agent_logs/YYYYMMDD_HHMMSS_<description>.sh`; tee stdout/stderr to logs.
- Do NOT commit or push without explicit approval.

# Logbook Rules

**Plan file** (`agent_logs/YYYYMMDD_HHMMSS_<name>_plan.md`): created before non-trivial tasks; defines inputs, constraints, approach, phases, acceptance criteria, files to change. One canonical plan file per workstream.

**Implementation log** (`agent_logs/YYYYMMDD_HHMMSS_<name>.md`): records execution progress. Do not mix with plan files.

Update logs at each meaningful milestone, when an approach fails, or when handing off. Trivial changes (no design decision, no new knowledge, no debugging value) may skip logging.

Each log MUST include:
- Summary of user intent and instructions
- The plan used (or reference to the plan file)
- Previous state → changes made → reasoning
- Surprising discoveries
- Experiments conducted (with results if available)
- References to scripts/logs
- Suggestions for follow-up work (DO NOT implement)

Logs must capture reasoning and decisions — not just actions — so another agent can continue without additional context.

Before starting work, review relevant logs in `agent_logs/` to understand prior decisions and avoid repeating mistakes.

# Failure Handling

- If requirements are unclear → ask the user.
- If blocked by missing context or permissions → stop and ask.
- If multiple valid approaches exist with meaningful tradeoffs → ask the user before deciding.
- If implementation diverges from plan → stop, update the plan, document the reason in logs before continuing.
- Do not proceed with risky assumptions.

# Environment Instructions

## Setup

This project is a SvelteKit personal website built with Svelte 5, TypeScript, Vite, Tailwind CSS, and `@sveltejs/adapter-static`.

Standard commands:

```bash
npm install       # Install dependencies
npm run dev       # Start the Vite development server
npm run build     # Build the static production site
npm run preview   # Preview the production build locally
npm run check     # Run Svelte and TypeScript diagnostics
```

## Dependency Inspection

When working with dependencies:

1. Check `package.json` and `package-lock.json` for declared versions and locked dependency state.
2. Review existing Svelte components, route modules, Tailwind utilities, and local assets before implementing new functionality.
3. If a package update is required, ask for permission first, including the version delta and any breaking changes.

# Formatting

Run at the end of full implementation:

```bash
npm run check
npm run build
```

# Testing Rules

- Do not run tests for documentation-only changes.

Run project checks:

```bash
npm run check
npm run build
```

- Fix all failing checks before proceeding.
- Never bypass tests without explicit instruction.

# Definition of Done

A task is complete when:

- Plan is fully executed
- No partial implementations remain
- Svelte and TypeScript diagnostics pass: `npm run check`
- Production build passes: `npm run build`
- Logbook is updated appropriately
- `docs/` updated wherever relevant
- Changes are consistent with repository standards

# Suggesting Future Work

When you identify improvements beyond the current task's scope:
- Document them clearly in the relevant log or doc file.
- DO NOT implement unless explicitly instructed.