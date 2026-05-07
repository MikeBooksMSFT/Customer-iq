# Customer IQ

Customer IQ is a repo-local platform for creating customer-specific intelligence agents for enterprise account teams.

## Vision

The long-term vision is a production-ready account intelligence platform that:

- supports many enterprise customers
- grounds all customer understanding on stable identifiers such as TPID, MSX account name, MSXi key, and SharePoint site
- ingests structured and unstructured customer signals from approved systems
- generates durable, evidence-based Customer IQ outputs
- gives account teams an interaction layer for asking grounded questions by customer
- deploys cleanly through GitHub and Azure

## Current State

The repository already provides a working foundation:

- multi-customer support for **Walgreens** and **Boots**
- structured customer context files under `context/customer/`
- generated Customer IQ outputs under `outputs/`
- three logical agents: ingestion, intelligence, and interaction
- reusable prompt skills under `.github/prompts/`
- CLI tooling for onboarding, ingestion, IQ generation, interaction, and static export
- a local Flask web app
- a hosted static GitHub Pages site
- GitHub Actions workflows for IQ generation, agent running, and Pages deployment
- Azure deployment scaffold for future hosted runtime work

## Hosted Experience

- **Public static site:** `https://mikebooksmsft.github.io/Customer-iq/`
- **Local interactive app:** run the Flask app from `webapp/`

The public site is read-only. Persistent customer creation and validation happen through the local app or CLI.

## Access Right Now

- **Public Pages site:** read-only, repo-backed, no live system connectors
- **Local Flask app:** read/write against repo files, includes onboarding plus candidate search
- **MSX opportunity view:** available in the local app from stored snapshots grounded on current MSX results
- **MSX/MSXi/SharePoint live runtime connectors:** not yet wired into the app runtime
- **Candidate registry:** stored in `context/customer-candidates.json` for search and validation workflow

## Repository Structure

```text
.
├── .github/
│   ├── prompts/
│   └── workflows/
├── agents/
├── context/
│   └── customer/
├── outputs/
├── tools/
│   └── customer-iq/
├── webapp/
├── workflows/
├── AGENTS.md
├── README.md
└── azure.yaml
```

## Logical Agents

### ingestion-agent

- Reads files, scripts, and supported data sources
- Normalizes signals
- Updates `context/customer/<customer>.md`

### intelligence-agent

- Reads customer context
- Generates `outputs/<customer>-iq.md`
- Highlights opportunities, risks, Microsoft play, and next actions

### interaction-agent

- Reads context and generated IQ
- Answers customer-specific questions
- Powers the local web app and API endpoints

## Customer Grounding Model

Each customer should be grounded, when available, on:

- TPID
- MSX account name
- MSX account ID
- MSXi key
- MSX hyperlink
- SharePoint site URL
- managed sites
- aliases
- validation status

These fields are stored in `## Metadata` and `## System Connections` within each customer context file.

## Prompt Skills

Stored under `.github/prompts/`:

- `customer-iq.prompt.md`
- `ingest.prompt.md`
- `update-iq.prompt.md`
- `opportunity-map.prompt.md`

## CLI Commands

### Run ingestion

```powershell
python tools\customer-iq\run_ingestion.py --customer all
```

To improve intelligence from SharePoint, export or copy relevant files into either:

```text
inputs\sharepoint\<customer>\
```

or files prefixed with the customer slug under:

```text
inputs\sharepoint\
```

Then rerun ingestion. The current SharePoint path is **repo-grounded**: Customer IQ reads exported files, contracts, notes, and loose text from that folder and converts them into signals, risks, opportunities, actions, and sources.

Best current formats: `.txt`, `.md`, `.csv`, and `.json`. For `.pdf` or Office files, export the relevant content to text first.

### Run Customer IQ generation

```powershell
python tools\customer-iq\run_customer_iq.py --customer all
```

### Run the orchestrator

```powershell
python tools\customer-iq\agent_runner.py --workflow agent-runner --customer all
```

### Ask a customer agent a question

```powershell
python tools\customer-iq\run_interaction.py --customer walgreens --question "What are the top risks?"
```

### Add or update a customer profile

```powershell
python tools\customer-iq\customer_onboarding.py --interactive
```

Or non-interactively:

```powershell
python tools\customer-iq\customer_onboarding.py --customer-name "New Customer" --tpid 123456 --msx-account-name "New Customer" --msxi-key 123456 --msx-hyperlink "https://..." --sharepoint-site "https://tenant.sharepoint.com/sites/new-customer" --managed-sites "https://..., https://..." --validation-status "Validated with account team"
```

This is the current path for adding a new customer or validating identifiers discovered through search.

### Run the web app

```powershell
pip install -r webapp\requirements.txt
python webapp\app.py
```

Then open `http://127.0.0.1:5000`.

### Refresh the static site payload

```powershell
python tools\customer-iq\export_static_site.py
```

## Supported Customers

- Walgreens
- Boots

## Web App

The Flask web app provides:

- a customer dropdown
- a Customer IQ viewer
- an MSX opportunity snapshot viewer
- a chat interface backed by the interaction-agent
- a customer onboarding form for TPID, SharePoint, MSX, and MSXi grounding

## Customer Onboarding Workflow

Use this flow when adding a customer:

1. Search or identify the likely account in source systems such as MSX/MSXi.
2. Capture the candidate TPID, MSX account name, MSXi key, MSX hyperlink, SharePoint site, and other managed-site URLs.
3. Mark the validation status clearly if any identifier is still provisional.
4. Save the profile through the onboarding CLI or local app.
5. Use the candidate-search surface to review ambiguous MSX matches before confirming account IDs.
6. Run ingestion and Customer IQ generation.
7. Publish or refresh the static site if needed.

## GitHub Actions

### Customer IQ workflow

Manual dispatch workflow that:

1. checks out the repo
2. runs ingestion
3. runs intelligence generation
4. commits updated context and outputs

### Agent runner workflow

Manual dispatch workflow that can run one customer or all customers through the orchestrator.

### Pages workflow

Pushes to `main` automatically refresh the static GitHub Pages site from exported customer data.

## Azure Deployment Scaffold

The repo includes:

- `azure.yaml`
- `webapp/Dockerfile`
- Flask API endpoints under `webapp/app.py`

These files provide a deployment foundation without requiring credentials at this stage.

## What Is Not Implemented Yet

- live authenticated connectors to MSX, MSXi, or SharePoint from the hosted site
- direct authenticated MSX or SharePoint calls from the Flask app runtime
- automatic customer search-and-confirm UX inside the public Pages experience
- production-grade persistence outside repo files
- RBAC, secrets, and operational telemetry

## Development Notes

- All generated output is grounded only in repo-local context.
- Customer onboarding should capture TPID, MSX account name, MSXi key, SharePoint site, and validation status whenever available.
- When available, also capture an MSX hyperlink and any other managed-site URLs that should ground the customer record.
- Missing information is called out explicitly.
- The current implementation is intentionally simple and modular so it can be extended safely.
