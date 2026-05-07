# Customer Action Plan Workflow

Use this workflow when asked for concrete account actions.

## Goal

Produce a prioritized, evidence-backed action plan and save it to `outputs/<customer>-actions.md`.

## Execution Steps

### 1. Read the system definition

- Open `AGENTS.md`.

### 2. Read the customer state

- Open `context/customer/<customer>.md`.
- Open `outputs/<customer>-iq.md` if it exists.

### 3. Read the planning prompt

- Open `.github/prompts/action-plan.prompt.md`.
- Use it as the output contract.

### 4. Prioritize actions

- Focus on actions that reduce uncertainty, advance a concrete opportunity, or mitigate a supported risk.
- Remove generic actions that are not grounded in evidence.

### 5. Save the output

- Create or overwrite `outputs/<customer>-actions.md`.

## Rules

- Use only the provided context and report files.
- Make missing owners, dependencies, and blockers explicit.
- Keep the final plan short enough to be used by an account team without editing.
