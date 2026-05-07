# Clawpilot Workflows Migration Bundle

This bundle contains a portable Clawpilot workflow system: custom skills plus FSX command-line tools for account, Power BI, MSX, email, Teams, calendar, and follow-up workflows.

The operating philosophy is intentionally preserved: risk-first account work, license/seat movement checks, renewal/churn detection, stakeholder gap detection, evidence-first summaries, and concrete next actions.

## Contents

```text
clawpilot-workflows-migration/
  README.md
  install.ps1
  manifest.json
  m-skills/                 # Custom Clawpilot skills
  fsx-tools/                # F# script CLIs and support files
    msx-cli.fsx
    pbi-cli.fsx
    msx-filter.fsx
    calendar-week.fsx
    email-inbox.fsx
    followups-aging.fsx
    teams-scripts/
    agent-db/               # agent_db.py + schema.sql only; no live database
```

## What is intentionally not included

This public-facing bundle excludes live/customer-specific data:

- No `agent.db`
- No account list CSV
- No account aliases file
- No Power BI schema export
- No email, calendar, Teams, token, cache, credential, or OneDrive content

Some tools expect the recipient to provide their own local support files after install, such as an account list CSV. Use your team's approved source of account metadata and permissions.

## Requirements on the recipient machine

1. Clawpilot installed.
2. .NET SDK available so `dotnet fsi` can run `.fsx` files.
3. Azure CLI installed and signed in with access to the required resources:
   ```powershell
   az login
   ```
4. Set `MSX_RESOURCE_URL` if using MSX/Dynamics commands, for example `$env:MSX_RESOURCE_URL="https://YOUR-DYNAMICS-ORG.crm.dynamics.com"`.
5. Python 3 if the recipient wants to initialize the optional local `agent.db`.
6. Microsoft 365 auth/permissions inside Clawpilot for email, calendar, Teams, and file workflows.

## Install

From PowerShell in the unzipped folder:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\install.ps1 -BackupExisting -InitializeAgentDb
```

What the installer does:

1. Copies `m-skills\*` into `%USERPROFILE%\.copilot\m-skills`.
2. Copies `fsx-tools\*` into `%USERPROFILE%\.copilot\m-skills\fsx-builder\tools`.
3. Optionally creates a fresh local `agent.db` under the installed FSX tools folder.

Use `-BackupExisting` if the recipient already has local skills. The backup is written next to their current `m-skills` folder.

## Add your team's account metadata

If your workflows need account matching, place your approved account list at:

```powershell
$env:USERPROFILE\.copilot\m-skills\fsx-builder\tools\accounts.csv
```

If a script expects a differently named file, either update the script to use `accounts.csv` or pass the script's supported `--accounts-csv` option where available.

## Test after install

Run these commands from any PowerShell window:

```powershell
dotnet fsi "$env:USERPROFILE\.copilot\m-skills\fsx-builder\tools\msx-cli.fsx" -- -h
dotnet fsi "$env:USERPROFILE\.copilot\m-skills\fsx-builder\tools\pbi-cli.fsx"
```

If `az` auth is required for a command, run `az login` first.

## Recommended Clawpilot usage

After install, restart Clawpilot or reload skills if the app supports skill refresh. Then use the orchestrator skill:

```text
/run <account name>
```

The account workflows are intentionally risk-first: they check license movement, cancellations, true-downs, renewal risk, usage decay, stakeholder gaps, and concrete next actions before summarizing.

## Updating the bundle later

Update the canonical files first:

```text
%USERPROFILE%\.copilot\m-skills\fsx-builder\tools
%USERPROFILE%\.copilot\m-skills
```

Then recreate this export zip. Do not hand-edit installed recipient copies unless you want configuration drift, which is basically entropy wearing a badge.
