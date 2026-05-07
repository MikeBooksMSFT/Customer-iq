# Tools

This directory contains the execution layer for Customer IQ.

## Vision

The tools layer should eventually connect Customer IQ to grounded enterprise systems such as:

- MSX
- MSXi
- SharePoint
- customer documents and notes
- meeting feeds
- cost and consumption sources

Those connections should be anchored on stable customer identifiers like TPID and validated source URLs.

## Current State

Today the tools directory contains two categories of assets:

1. **Customer IQ Python tooling** under `tools/customer-iq/`
2. **Legacy or reference utilities** such as the existing FSX scripts under this folder

The active platform path for Customer IQ is the Python tooling, not the older migration-bundle description that previously lived in this README.

## Customer IQ Tools

Use `tools/customer-iq/` for the current platform:

- onboarding customers
- ingesting meeting, document, and cost signals
- generating Customer IQ
- answering customer questions
- exporting static site data

## Directory Notes

- `customer-iq/` — current Customer IQ platform tooling
- `calendar/`, `calendar-week.fsx`, `email-inbox.fsx`, `teams-scripts/`, `msx-cli.fsx`, `pbi-cli.fsx` — legacy or adjacent utilities that may inform future integrations
- `agent-db/` — local database helper assets retained from earlier tool experiments

## Recommended Direction

When extending the platform:

1. prefer adding new runtime logic under `tools/customer-iq/`
2. keep system integration logic grounded on TPID / account metadata
3. treat older FSX scripts as optional references unless they are explicitly wired into the current flow
