# Ingest

Use this prompt to merge new customer signals into `context/customer/<customer>.md`.

## Inputs

- customer name
- existing context file
- new meeting, document, note, or cost signals

## Steps

1. Read the new source material.
2. Extract durable facts, not raw transcript noise.
3. Merge facts into the correct context sections.
4. Record what changed in `## Change Log`.

## Rules

- Do not duplicate existing facts.
- Keep the standard context structure intact.
- Distinguish new information from already-known information.
- Use only source material provided inside the repo.
- Call out incomplete or low-confidence evidence.
