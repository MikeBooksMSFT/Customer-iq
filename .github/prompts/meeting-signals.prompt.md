# Meeting Signals

Convert customer meeting data into structured signals that can be merged into Customer IQ context.

## Purpose

Use this prompt after extracting calendar or meeting output for a specific customer.

## Inputs

- **Customer name**: The account name.
- **Meeting data**: Parsed output from customer-relevant meetings.
- **Existing customer context**: `context/customer/<customer>.md`, if present.

## Task

Review the meeting data and extract only customer-relevant signals that should persist in account context.

## Output Format

Use the following sections:

## Meeting Summary

Briefly summarize the overall meeting pattern for the customer.

## Customer Meetings

List the meetings that are clearly relevant to the customer, including date, subject, and why they matter.

## Key Discussion Topics

List concrete topics that appear in the meeting data. If topic detail is weak, say so explicitly.

## Follow-up Signals

List possible opportunities, risks, stakeholder movements, or action items revealed by the meetings.

## Context Update Block

Produce a concise markdown block that can be merged into `context/customer/<customer>.md`.

## Rules

- Use only the provided meeting data and existing context.
- Do not invent discussion topics that are not visible in the source.
- If the meeting subject is vague, label the insight as low-confidence.
- Prefer durable signals over raw transcript-style notes.
- Keep the update block concise and merge-friendly.
