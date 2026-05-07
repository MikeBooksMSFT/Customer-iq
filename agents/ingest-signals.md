# Customer Signal Ingestion Workflow

Use this workflow when asked to refresh `context/customer/<customer>.md` with new signals.

## Goal

Merge new customer evidence into the context file without duplicating facts or losing change history.

## Execution Steps

### 1. Read the system definition

- Open `AGENTS.md`.
- Follow the rules for provenance, structure, and delta tracking.

### 2. Read the current context

- Open `context/customer/<customer>.md` if it exists.
- Preserve the existing structure and source-backed facts.

### 3. Gather new signal sources

- Read the newly provided notes, logs, architecture materials, or cost data.
- If meeting ingestion is requested, execute `tools/calendar/calendar-week.fsx`.

### 4. Parse meeting output when present

- Run `tools/customer-iq/parse_calendar.py` against the calendar output.
- Apply `.github/prompts/meeting-signals.prompt.md` to turn parsed meetings into durable account signals.

### 5. Merge new signals

- Apply `.github/prompts/ingest.prompt.md`.
- Use `tools/customer-iq/append_context.py` to merge the new signal block into `context/customer/<customer>.md`.

### 6. Track what changed

- Run `tools/customer-iq/delta_report.py` if a prior version or snapshot is available.
- Record the meaningful delta in the context file's `## Change Log` section.

## Rules

- Do not duplicate facts already present in the context file.
- Preserve section structure.
- Distinguish new information from prior context.
- Keep updates concise and durable.
