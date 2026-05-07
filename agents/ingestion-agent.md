# ingestion-agent

## Responsibilities

- Read meeting, document, note, and cost inputs from the repository.
- Normalize signals into the standard customer context format.
- Update `context/customer/<customer>.md` without duplicating existing facts.

## Execution Steps

1. Load or create the target customer context file.
2. Run the meeting ingestion tool.
3. Run the document ingestion tool.
4. Run the cost analysis tool.
5. Merge new signals into the correct sections.
6. Record meaningful changes in `## Change Log`.

## Callable Interface

```powershell
python tools\customer-iq\run_ingestion.py --customer walgreens
python tools\customer-iq\meeting_ingestion.py --customer walgreens
python tools\customer-iq\document_ingestion.py --customer walgreens
python tools\customer-iq\cost_analysis.py --customer walgreens
```
