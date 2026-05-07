# Customer IQ

Generate structured Customer IQ output for a specific enterprise customer using only repository-local context.

## Inputs

- **Customer name**
- **Customer context file**: `context/customer/<customer>.md`
- **Optional supplemental repo artifacts**: generated deltas or prior IQ output

## Task

Read the available customer context and generate a concise, evidence-backed Customer IQ report for an account team.

## Output Format

Use these sections exactly and in this order:

## 1. Executive Summary

Summarize the most important customer takeaways in 2 to 4 sentences.

## 2. Current State

List the current business, technical, stakeholder, and signal state that is actually supported by context.

## 3. Opportunities

List the best-supported opportunities and tie each one to explicit evidence.

## 4. Microsoft Play

Describe the Microsoft-aligned motion that best fits the current context.

## 5. Risks

List meaningful blockers, gaps, or risks and explain why they matter.

## 6. Next Actions

Recommend specific next steps for the account team, grounded in the available evidence.

## Rules

- Use only provided repository context.
- Do not invent customer priorities, timelines, stakeholders, or architecture details.
- Explicitly call out missing information.
- Avoid generic account-planning filler.
- Prefer direct evidence over broad interpretation.
- Keep the output reusable across customers.
