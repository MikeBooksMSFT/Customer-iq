# Customer IQ Delta Analysis

Compare two versions of customer intelligence and surface only meaningful changes.

## Purpose

Use this prompt when a new ingestion pass or report generation changes the known state of a customer.

## Inputs

- **Customer name**: The account name.
- **Previous artifact**: Prior context file, prior output, or prior snapshot.
- **Current artifact**: Newly updated context file or output.

## Task

Identify what is new, what changed, what is no longer supported, and what account actions should change as a result.

## Output Format

## Net New Signals

List facts or signals that appear in the current artifact but not the previous one.

## Changed Understanding

List changes in interpretation, priority, risk posture, or opportunity shape.

## Removed or Invalidated Signals

List facts or assumptions that are no longer present or no longer supported.

## Impact on Opportunities, Risks, and Actions

Explain how the changes should affect account strategy, risk posture, or follow-up actions.

## Missing Information

Call out the gaps that still prevent a confident view.

## Rules

- Compare only the provided artifacts.
- Do not treat wording changes as substantive changes unless meaning changed.
- Be explicit when there is no meaningful delta.
- Keep the output focused on account impact, not document formatting.
