# Customer IQ Action Plan

Generate a prioritized action plan from customer context, opportunities, and risks.

## Purpose

Use this prompt when the account team needs concrete next steps rather than only a summary.

## Inputs

- **Customer name**: The account name.
- **Customer context**: `context/customer/<customer>.md`
- **Customer IQ output**: `outputs/<customer>-iq.md`, if available.

## Task

Turn the available evidence into a short, prioritized action plan for the account team.

## Output Format

## Priority Actions

List the most important actions in priority order. For each action, include:

- the action
- why it matters now
- the signal, opportunity, or risk that justifies it

## Suggested Owners

List the likely owner role for each action, if the context supports it.

## Dependencies and Open Questions

Call out missing inputs, blockers, or sequencing constraints.

## Fastest Path Forward

Summarize the smallest set of actions that would materially improve account clarity or momentum.

## Rules

- Recommend only actions supported by the provided context.
- Avoid generic actions such as "follow up with customer" unless the context makes that the right action.
- Prefer actions that reduce uncertainty, advance an opportunity, or mitigate a specific risk.
- Make missing ownership or missing evidence explicit.
