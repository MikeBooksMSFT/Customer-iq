# Customer IQ

Generate structured Customer IQ output for an account team using only the provided customer context.

## Inputs

- **Customer name**: The name of the customer or account.
- **Context files**: The set of files provided for this customer, including notes, meeting summaries, architecture context, cost data, and related materials.

## Task

Review the provided context files and produce a structured Customer IQ summary that is specific, evidence-based, and useful for account planning and execution.

## Output Format

Use the following sections exactly and in this order:

## 1. Executive Summary

Provide a concise summary of the most important takeaways about the customer, grounded in the provided context.

## 2. Current State

Summarize the current customer situation, including relevant business context, technical posture, active initiatives, stakeholders, and consumption or cost signals if available.

## 3. Opportunities

List concrete opportunities revealed by the context. For each opportunity, describe:

- What the opportunity is
- Why it matters now
- What evidence supports it

## 4. Microsoft Play

Describe the most relevant Microsoft-aligned plays based on the provided context. Focus on specific motions, solution alignment, or strategic actions that fit the customer’s current state.

## 5. Risks

List meaningful risks, blockers, or uncertainties. For each risk, describe:

- What the risk is
- Why it matters
- What evidence supports it
- What information is still missing, if applicable

## 6. Next Actions

Recommend concrete next steps for the account team. Each action should be specific, relevant to the context, and tied to an observed opportunity, risk, or gap.

## Rules

- Use only the provided context files.
- Do not invent customer facts, stakeholder views, technical details, timelines, or business priorities.
- Call out missing information explicitly when the context is incomplete.
- Avoid generic statements, boilerplate recommendations, and vague summaries.
- Prefer precise, source-grounded observations over broad strategic language.
- If the context does not support a section strongly, say so clearly and keep the section concise.
- Keep output structured, readable, and repeatable across customers.

## Quality Bar

- Be specific.
- Be evidence-based.
- Be concise.
- Be action-oriented.
- Make uncertainty visible instead of filling gaps with assumptions.
