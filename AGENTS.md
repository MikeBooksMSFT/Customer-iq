# Customer IQ Agents

## Purpose

Customer IQ is an agentic system for turning fragmented customer signals into reliable, structured intelligence for account teams.

It ingests signals such as meetings, notes, architecture artifacts, and cost data; maintains a durable view of customer state; and produces actionable insights that support planning, execution, and follow-up.

## Primary Artifacts

- `context/customer/<customer>.md` is the durable source of truth for account context.
- `outputs/<customer>-iq.md` is the structured account intelligence output for account teams.
- `.github/prompts/*.prompt.md` are reusable prompt-based skills for ingestion, analysis, delta tracking, and action planning.
- `agents/*.md` are executable workflows for Copilot agents.
- `tools/customer-iq/*` contains helper scripts that automate parsing, context updates, and delta generation.

## Data Model

### Customer

Represents the current known state of an account.

**Core fields**

- Name
- Account identifiers
- Industry or segment
- Strategic priorities
- Architecture context
- Cost and consumption context
- Key stakeholders
- Source references
- Last updated timestamp

### Signals

Represents raw or lightly normalized evidence that informs customer understanding.

**Core fields**

- Signal type
- Source file or tool
- Source timestamp
- Customer match
- Evidence snippet
- Confidence
- Last ingested timestamp

### Opportunities

Represents potential areas for growth, acceleration, or engagement.

**Core fields**

- Opportunity title
- Description
- Associated customer
- Supporting evidence
- Stage or status
- Priority
- Owner
- Next step
- Expected impact
- Last updated timestamp

### Risks

Represents blockers, gaps, or threats to success.

**Core fields**

- Risk title
- Description
- Associated customer or opportunity
- Supporting evidence
- Severity
- Likelihood
- Mitigation plan
- Owner
- Status
- Last updated timestamp

### Actions

Represents concrete follow-up work for the account team.

**Core fields**

- Action title
- Description
- Associated customer, opportunity, or risk
- Triggering evidence
- Owner
- Due date
- Status
- Priority
- Last updated timestamp

### Deltas

Represents meaningful change over time between two account states.

**Core fields**

- Compared artifact pair
- New signals
- Changed understanding
- Removed or invalidated signals
- Impacted opportunities, risks, or actions
- Comparison timestamp

## Signal Sources

Approved signal sources include:

- customer context files
- meeting data from `tools/calendar/calendar-week.fsx`
- notes and summaries
- architecture artifacts
- cost and consumption data
- approved logs or operational exports

When a source is tool-generated, preserve the raw output long enough to support traceability and re-processing.

## Customer Context Format

Each `context/customer/<customer>.md` file should converge toward this structure:

```md
# <Customer Name>

## Overview
## Signals
## Opportunities
## Risks
## Actions
## Change Log
## Sources
```

Agents may add subsections, but should preserve this overall layout so ingestion and delta tooling can work consistently.

## Workflow

### 1. Ingest

- Collect raw customer signals from approved inputs such as meetings, notes, architecture documents, and cost data.
- Use automation where possible, especially for recurring feeds such as calendar exports.
- Preserve source metadata, timestamps, and provenance for every ingested item.
- Store raw inputs without inventing missing facts.

### 2. Normalize

- Convert raw signals into consistent structured records.
- Resolve entities across sources, including customers, stakeholders, opportunities, and risks.
- Link every structured fact back to its originating context files or source references.
- Deduplicate repeated facts before writing context updates.
- Update `context/customer/<customer>.md` in a structured format rather than appending raw dumps.

### 3. Analyze

- Identify trends, opportunities, risks, blockers, and recommended actions from normalized data.
- Compare new inputs to prior state to detect meaningful deltas over time.
- Separate confirmed facts from inferred insights and keep both traceable.
- Prefer automation for mechanical comparisons and use prompts for interpretation.

### 4. Output

- Produce concise, actionable outputs for account teams.
- Summarize current customer state, open opportunities, active risks, and recommended actions.
- Include source-backed reasoning and highlight what changed since the last update.
- When helpful, generate companion action plans or delta summaries alongside the main Customer IQ report.

## Recommended Prompt Skills

- `customer-iq.prompt.md` for the core account report
- `ingest.prompt.md` for merging new signals into customer context
- `meeting-signals.prompt.md` for converting meeting output into reusable account signals
- `delta-analysis.prompt.md` for change tracking
- `action-plan.prompt.md` for prioritized follow-up actions

## Rules

- Never hallucinate customer data.
- Always use context files as the grounding source for customer understanding.
- Track deltas over time so outputs show what changed, not just the current snapshot.
- Preserve provenance for every important fact, insight, risk, and action.
- When evidence is incomplete, state uncertainty explicitly instead of filling gaps.
- Do not duplicate existing context when ingesting new signals.
- Keep customer context structured so humans and scripts can both update it safely.
- Prefer actionable insights tied to explicit evidence over generic account planning language.
