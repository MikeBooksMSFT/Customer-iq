# Customer IQ

Customer IQ is a repo-local platform for creating customer-specific intelligence agents for enterprise account teams.

It supports multiple customers, stores customer knowledge in versioned markdown context files, generates structured Customer IQ outputs, and exposes a simple web UI for selecting a customer and chatting with that customer's interaction agent.

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
python tools\customer-iq\customer_onboarding.py --customer-name "New Customer" --tpid 123456 --msx-account-name "New Customer" --msxi-key 123456 --sharepoint-site "https://tenant.sharepoint.com/sites/new-customer" --validation-status "Validated with account team"
```

### Run the web app

```powershell
pip install -r webapp\requirements.txt
python webapp\app.py
```

Then open `http://127.0.0.1:5000`.

## Supported Customers

- Walgreens
- Boots

## Web App

The Flask web app provides:

- a customer dropdown
- a Customer IQ viewer
- a chat interface backed by the interaction-agent
- a customer onboarding form for TPID, SharePoint, MSX, and MSXi grounding

## GitHub Actions

### Customer IQ workflow

Manual dispatch workflow that:

1. checks out the repo
2. runs ingestion
3. runs intelligence generation
4. commits updated context and outputs

### Agent runner workflow

Manual dispatch workflow that can run one customer or all customers through the orchestrator.

## Azure Deployment Scaffold

The repo includes:

- `azure.yaml`
- `webapp/Dockerfile`
- Flask API endpoints under `webapp/app.py`

These files provide a deployment foundation without requiring credentials at this stage.

## Development Notes

- All generated output is grounded only in repo-local context.
- Customer onboarding should capture TPID, MSX account name, MSXi key, SharePoint site, and validation status whenever available.
- Missing information is called out explicitly.
- The current implementation is intentionally simple and modular so it can be extended safely.
