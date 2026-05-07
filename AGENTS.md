# Customer IQ Agent System

## Purpose

Customer IQ is a modular platform for building customer-specific intelligence agents inside this repository.

The system ingests enterprise customer knowledge, stores it in structured context files, generates Customer IQ outputs, and exposes a lightweight interaction layer so account teams can ask grounded questions about each customer.

## Platform Components

- `context/customer/<customer>.md` stores durable customer knowledge.
- `outputs/<customer>-iq.md` stores generated Customer IQ output.
- `agents/*.md` defines logical agents and their operating contracts.
- `workflows/*.md` defines executable repo workflows.
- `.github/prompts/*.prompt.md` stores reusable prompt skills.
- `tools/customer-iq/*.py` provides CLI automation for ingestion, intelligence generation, orchestration, and interaction.
- `webapp/` hosts the UI and HTTP endpoints for customer selection, IQ viewing, and chat.
- `.github/workflows/*.yml` provides GitHub Actions automation.
- `azure.yaml` and `webapp/Dockerfile` provide deployment scaffolding for Azure.

## Logical Agents

### ingestion-agent

Owns the collection and normalization of customer signals from meetings, documents, notes, and cost inputs.

### intelligence-agent

Owns the production of Customer IQ output, including opportunities, Microsoft play, risks, and next actions.

### interaction-agent

Owns customer-facing question answering for the web app and API layer using stored context plus generated IQ output.

## Data Model

### Customer

- name
- aliases
- account profile
- strategic priorities
- key stakeholders
- architecture summary
- cost and consumption summary
- sources
- last updated timestamp

### Signals

- signal type
- source artifact
- date or ingestion timestamp
- evidence snippet
- confidence
- customer association

### Opportunities

- title
- why it matters
- supporting evidence
- confidence
- owner or owner role

### Risks

- title
- impact
- supporting evidence
- mitigation need
- missing information

### Actions

- action
- rationale
- supporting signal
- owner or owner role
- status

## Standard Customer Context Format

Every `context/customer/<customer>.md` file should use this structure:

```md
# <Customer Name>

## Metadata
## System Connections
## Overview
## Signals
## Opportunities
## Risks
## Actions
## Change Log
## Sources
```

The file is the primary source of truth for the interaction and intelligence layers.

## Customer Onboarding Requirements

Every customer profile should capture, when available:

- TPID
- MSX account name
- MSXi key or grounding identifier
- SharePoint site URL
- validation status
- aliases or alternate names

If a system identifier comes from search rather than confirmed account data, mark it as pending validation.

## Standard Customer IQ Output Format

Each generated report must use these sections in order:

1. Executive Summary
2. Current State
3. Opportunities
4. Microsoft Play
5. Risks
6. Next Actions

Outputs are written to `outputs/<customer>-iq.md`.

## Signal Sources

Approved signal sources include:

- customer markdown context
- TPID-grounded MSX or MSXi references stored in repo context
- SharePoint site references stored in repo context
- meeting ingestion files
- document ingestion files
- cost analysis inputs
- notes and manually added signals stored in repo

If a signal is not present in repo artifacts, it is not a valid source for generated output.

## Operating Principles

- Do not hallucinate customer data.
- Keep all logic inside the repository.
- Prefer simple, working implementations over incomplete complexity.
- Keep the system modular so ingestion, intelligence, interaction, UI, and deployment can evolve independently.
- Preserve provenance and distinguish sourced facts from assumptions.
- Track deltas over time in the customer context change log.
- Avoid duplicating facts when merging new signals.
- Make missing information explicit.
- Ground system connections on stable identifiers such as TPID and SharePoint site URL whenever possible.

## Execution Flow

1. Ingestion updates `context/customer/<customer>.md`.
2. Intelligence generation reads customer context and writes `outputs/<customer>-iq.md`.
3. Interaction reads both context and generated IQ output to answer questions.
4. Workflows and GitHub Actions orchestrate those steps for one customer or all customers.
