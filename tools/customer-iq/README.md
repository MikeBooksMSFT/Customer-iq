# Customer IQ helper scripts

These scripts provide the execution layer for the Customer IQ platform.

## Scripts

- `customer_iq_core.py` contains shared repo paths, context handling, IQ generation, and interaction logic.
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
python tools\customer-iq\run_ingestion.py
python tools\customer-iq\run_customer_iq.py
python tools\customer-iq\run_interaction.py --customer walgreens --question "What are the next actions?"
python tools\customer-iq\agent_runner.py --workflow agent-runner --customer all
```
