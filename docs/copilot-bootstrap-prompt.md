## Copilot bootstrap prompt for Customer IQ

Paste the block below into GitHub Copilot Chat (VS Code or github.com). It assumes the chat has access to this repo. Use the `@workspace` participant in VS Code, or assign the issue to Copilot Coding Agent on github.com.

---

@workspace I want you to act as a senior engineer onboarding to this repo. Read these files first, in order, before suggesting any code: `AGENTS.md`, `README.md`, `agents/customer-iq.md`, `agents/ingestion-agent.md`, `agents/intelligence-agent.md`, `agents/interaction-agent.md`, every file under `.github/prompts/`, `tools/customer-iq/customer_iq_core.py`, `webapp/app.py`, and the two seed customers under `context/customer/`.

Operating contract (do not violate):

1. Treat `AGENTS.md` as the system contract. Treat `.github/prompts/*.prompt.md` as the prompt library.
2. Never invent customer facts. Every customer claim must trace to a file under `context/customer/` or `inputs/`. If evidence is missing, write "missing information" rather than guessing.
3. Preserve the standard context section order (Metadata, System Connections, Overview, Signals, Opportunities, Risks, Actions, Change Log, Sources) and the standard IQ output order (Executive Summary, Current State, Opportunities, Microsoft Play, Risks, Next Actions).
4. Python is 3.12. Use `from __future__ import annotations`, type hints, and standard library where possible. Do not add network or auth dependencies without a CLI flag and a README note.
5. Keep the repo as the source of truth. No external services, no secrets, no production data.

Tasks for this session, in this order. Open a single PR per task with a clear title and description.

Task 1 (line endings, blocking other work). The repo currently shows 66 files as "modified" because the index is LF and the working tree is CRLF. Add a `.gitattributes` that enforces `* text=auto eol=lf` with a Windows-friendly exception for `*.ps1` (CRLF) and binary markers for images. Run `git add --renormalize .` and commit as a single normalization commit titled `chore: normalize line endings`. Verify `git status` is clean afterward.

Task 2. Create `.github/copilot-instructions.md`. It must (a) summarize the repo purpose in two sentences, (b) point Copilot at AGENTS.md and `.github/prompts/`, (c) restate the no-hallucination rule, (d) pin the section orders from rule 3 above, (e) state the Python style rules. Keep it under 80 lines.

Task 3. Add a `tests/` folder with pytest. Include: a smoke test that imports `customer_iq_core`, runs `list_customers("all")`, and asserts walgreens and boots are present; a test that `save_customer_iq("walgreens")` writes a file containing all six required IQ section headers; and a test that the standard context sections in `STANDARD_CONTEXT_SECTIONS` match the order in AGENTS.md. Wire pytest into a new GitHub Actions job in a `ci.yml` workflow that runs on pull request.

Task 4. Audit calendar ingestion. `agents/customer-iq.md` step 3 references `tools/calendar/calendar-week.fsx`, but the GitHub Actions runners do not install `dotnet fsi`. Either add a `Setup .NET` step to the Customer IQ and Pages workflows, or replace the F# script with a Python equivalent that produces the same JSON contract that `parse_calendar.py` already expects. Recommend one approach in the PR description and implement it.

Task 5. Decouple Pages publish from data refresh. Today `.github/workflows/pages.yml` runs ingestion + intelligence + export on every push to main. Split it: one workflow refreshes data on schedule and on `workflow_dispatch`, commits to a `data-refresh` branch, and opens a PR against main. The Pages workflow only builds and deploys `docs/` from main. This stops bad runs from corrupting the published site.

Task 6. Repo hygiene. Add a LICENSE (MIT unless I tell you otherwise), a one-line repo description, GitHub topics (`customer-intelligence`, `azure`, `copilot`, `account-planning`), and a CODEOWNERS file pointing to `@MikeBooksMSFT`.

For each PR, include a short checklist of what you changed, why, and how you verified it. Do not run `git push --force`. Do not delete files outside the listed scope. Stop and ask if you find ambiguity in AGENTS.md.

---

Tips for using this prompt:

- In VS Code, open the relevant files in tabs before sending. Copilot weights open editors heavily.
- For Copilot Coding Agent on github.com, paste the block as the issue body and assign the issue to Copilot. The agent will work through the tasks in PRs.
- If you only want one task, delete the others before sending.
