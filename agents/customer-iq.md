# Customer IQ Agent Workflow

Use this workflow when asked to generate Customer IQ output for a customer.

## Goal

Produce a structured, evidence-based Customer IQ report and save it to `outputs/<customer>-iq.md`.

## Execution Steps

### 1. Read the system definition

- Open `AGENTS.md`.
- Use it as the operating contract for purpose, data model, workflow, and rules.

### 2. Read customer context

- Read all files under `context/customer/`.
- Treat those files as the only approved source of customer facts unless the user explicitly provides additional context files.
- Preserve file names and source references while reading.

### 3. Ingest meeting signals

- Execute `tools/calendar/calendar-week.fsx`.
- Prefer JSON output when possible so downstream parsing is reliable.
- Parse the output with `tools/customer-iq/parse_calendar.py`.
- Apply `.github/prompts/meeting-signals.prompt.md`.
- Merge the resulting meeting insights into `context/customer/<customer>.md` with `.github/prompts/ingest.prompt.md` and `tools/customer-iq/append_context.py`.

### 4. Identify relevant signals

- Extract concrete signals from the context, including:
  - customer profile details
  - stakeholder information
  - active initiatives
  - architecture signals
  - cost or consumption signals
  - opportunities
  - risks
  - action items
- Note missing, conflicting, or outdated information.
- Track meaningful deltas over time when current context shows change from prior context.

### 5. Track deltas

- If a prior customer context file revision or prior output exists, compare it to the current state.
- Use `tools/customer-iq/delta_report.py` for mechanical change detection.
- Apply `.github/prompts/delta-analysis.prompt.md` to summarize only meaningful changes.

### 6. Apply the Customer IQ prompt

- Open `.github/prompts/customer-iq.prompt.md`.
- Use that prompt structure exactly.
- Ground every statement in the provided context files.
- Do not invent facts, priorities, timelines, or recommendations without evidence.
- Strengthen the `Next Actions` section with `.github/prompts/action-plan.prompt.md` when additional action prioritization is needed.

### 7. Generate the output

- Produce the report with these sections in this order:
  1. Executive Summary
  2. Current State
  3. Opportunities
  4. Microsoft Play
  5. Risks
  6. Next Actions
- Keep the writing specific, concise, and actionable.
- Explicitly call out missing information where the context is incomplete.
- Avoid generic statements.

### 8. Save the output

- Create or overwrite `outputs/<customer>-iq.md`.
- Replace `<customer>` with a filesystem-safe version of the customer name.
- Ensure the saved file contains only the final structured Customer IQ report.

## Rules

- Always read `AGENTS.md` first.
- Always use the files in `context/customer/` as the primary grounding source.
- Never hallucinate customer data.
- Always call out missing information.
- Track deltas over time when evidence supports change.
- Keep all output traceable to the provided context.
- Prefer the helper scripts in `tools/customer-iq/` for parsing, merge, and diff operations.
