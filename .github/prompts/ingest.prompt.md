# Customer Context Ingestion

Ingest new customer signals into the existing customer context file without losing structure or introducing duplicate information.

## Purpose

Use this prompt to incorporate new signals from meetings, notes, logs, and similar sources into `context/customer/<customer>.md`.

## Inputs

- **Customer name**: The target customer or account.
- **Existing context file**: `context/customer/<customer>.md`
- **New data**: Meetings, notes, logs, or other newly provided customer signals.

## Steps

### 1. Read new data

- Read the newly provided meetings, notes, logs, or other signals.
- Focus only on source material provided for this ingestion pass.

### 2. Extract key facts

- Identify concrete new facts, including:
  - customer activity
  - stakeholder mentions
  - initiatives
  - architecture details
  - product or platform signals
  - risks
  - actions
- Separate confirmed facts from weak signals or partial information.

### 3. Update `context/customer/<customer>.md`

- Read the existing customer context file first.
- Merge in the new facts.
- Preserve a structured format.
- Add new information in the most relevant section instead of appending raw notes blindly.

### 4. Track what changed

- Clearly distinguish newly added information from pre-existing context.
- Record meaningful deltas when the new data changes prior understanding.
- Make it easy to see what is new, updated, or still missing.

## Rules

- No duplication.
- Maintain structured format.
- Highlight new versus existing information.
- Use only the provided source material.
- Do not invent customer facts.
- If information is incomplete or uncertain, label it clearly.
- Prefer concise, durable context over verbose meeting transcripts.
