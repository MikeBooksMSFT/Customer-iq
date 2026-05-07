# Customer IQ helper scripts

These scripts automate the mechanical parts of Customer IQ so prompts can focus on interpretation.

## Scripts

- `parse_calendar.py` parses `calendar-week.fsx` output and emits customer-relevant meeting signals.
- `append_context.py` merges a markdown snippet into `context/customer/<customer>.md` using a stable section structure and a simple change log.
- `delta_report.py` compares two markdown artifacts and emits a compact delta summary.
- `run_ingestion.py` normalizes customer context files and ingests matching meeting files from `inputs/meetings/`.
- `run_customer_iq.py` renders `outputs/<customer>-iq.md` from the current customer context.

## Example usage

```powershell
python tools\customer-iq\parse_calendar.py --customer Walgreens --input outputs\walgreens-calendar.json
python tools\customer-iq\append_context.py --customer Walgreens --context-file context\customer\walgreens.md --snippet-file outputs\walgreens-meetings.md --change-summary "Added weekly meeting signals"
python tools\customer-iq\delta_report.py --previous outputs\walgreens-iq-prev.md --current outputs\walgreens-iq.md
python tools\customer-iq\run_ingestion.py
python tools\customer-iq\run_customer_iq.py
```
