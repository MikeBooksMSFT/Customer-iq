# Customer IQ Workflow

## Purpose

Generate refreshed Customer IQ output for one customer or all customers.

## Steps

1. Read the customer context file.
2. Run `ingestion-agent` to refresh signals.
3. Run `intelligence-agent`.
4. Write `outputs/<customer>-iq.md`.

## Commands

```powershell
python tools\customer-iq\run_ingestion.py --customer walgreens
python tools\customer-iq\run_customer_iq.py --customer walgreens
```
