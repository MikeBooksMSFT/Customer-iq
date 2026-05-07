# interaction-agent

## Responsibilities

- Answer questions about a customer using repo-local context and generated IQ output.
- Support the web UI and API endpoints.
- Keep answers grounded and explicit about missing information.

## Execution Steps

1. Read the target customer context file.
2. Read the generated Customer IQ output if available.
3. Match the user question to the most relevant context.
4. Return a concise answer with supporting evidence and missing-information callouts.

## Callable Interface

```powershell
python tools\customer-iq\run_interaction.py --customer walgreens --question "What are the top risks?"
```
