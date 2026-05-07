# Customer IQ helper scripts

These scripts provide the execution layer for the Customer IQ platform.

## Vision

The Customer IQ tools should become the grounded orchestration layer that:

- onboards customers with stable identifiers
- connects repo workflows to systems such as MSX, MSXi, and SharePoint
- ingests durable customer signals
- generates Customer IQ outputs
- powers interaction surfaces such as the local app and static site export

## Current State

The current implementation is working but intentionally simple:

- customer profiles are stored in repo markdown files
- onboarding is supported through CLI and the local Flask app
- meeting, document, and cost ingestion scripts are callable placeholders
- Customer IQ generation and question answering run locally from repo context
- static site export publishes read-only customer data to GitHub Pages

## Scripts

- `customer_iq_core.py` contains shared repo paths, context handling, IQ generation, and interaction logic.
- `customer_onboarding.py` adds or updates customer grounding metadata such as TPID, MSX account name, MSXi key, MSX hyperlink, SharePoint site, and managed sites.
- `meeting_ingestion.py` ingests meeting signals.
- `document_ingestion.py` ingests document signals.
- `cost_analysis.py` ingests cost signals.
- `run_ingestion.py` runs the ingestion-agent for one customer or all customers.
- `run_customer_iq.py` runs the intelligence-agent.
- `run_interaction.py` runs the interaction-agent for a single question.
- `agent_runner.py` orchestrates ingestion and intelligence generation.
- `append_context.py`, `delta_report.py`, and `parse_calendar.py` remain available as lower-level helpers.

## Example usage

```powershell
python tools\customer-iq\customer_onboarding.py --interactive
python tools\customer-iq\run_ingestion.py
python tools\customer-iq\run_customer_iq.py
python tools\customer-iq\run_interaction.py --customer walgreens --question "What are the next actions?"
python tools\customer-iq\agent_runner.py --workflow agent-runner --customer all
python tools\customer-iq\export_static_site.py
```

## Grounding Guidance

Whenever possible, onboard and operate customers using:

- TPID
- MSX account name
- MSXi key
- MSX hyperlink
- SharePoint site URL
- managed-site URLs
- validation status

If a value comes from search rather than direct confirmation, store it as a candidate and mark it clearly for validation.
