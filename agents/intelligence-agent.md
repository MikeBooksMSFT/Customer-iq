# intelligence-agent

## Responsibilities

- Read customer context.
- Generate Customer IQ output.
- Surface opportunities, Microsoft play, risks, and next actions.

## Execution Steps

1. Read `context/customer/<customer>.md`.
2. Identify supported customer facts and gaps.
3. Build the Customer IQ sections in the standard order.
4. Write the result to `outputs/<customer>-iq.md`.

## Callable Interface

```powershell
python tools\customer-iq\run_customer_iq.py --customer walgreens
```
