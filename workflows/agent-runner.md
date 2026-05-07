# Agent Runner Workflow

## Purpose

Run the full Customer IQ agent chain for one customer or all customers.

## Steps

1. Read `AGENTS.md`.
2. Read `context/customer/<customer>.md` for the selected customer or enumerate all customer context files.
3. Run `ingestion-agent`.
4. Run `intelligence-agent`.
5. Optionally call `interaction-agent` for a supplied question.
6. Write or refresh `outputs/<customer>-iq.md`.

## Command

```powershell
python tools\customer-iq\agent_runner.py --workflow agent-runner --customer all
```
