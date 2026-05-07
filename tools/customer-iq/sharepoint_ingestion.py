from __future__ import annotations

import argparse
from pathlib import Path

from customer_iq_core import (
    SHAREPOINT_DIR,
    append_unique_lines,
    discover_customer_files,
    ensure_repo_structure,
    load_customer_context,
    read_text_payload,
    save_customer_context,
)


RISK_KEYWORDS = ("risk", "issue", "delay", "blocker", "renewal", "security", "redline", "termination", "liability")
OPPORTUNITY_KEYWORDS = ("opportunity", "expansion", "copilot", "azure", "ai", "agent", "migration", "renewal", "savings")
ACTION_KEYWORDS = ("next step", "action", "follow up", "owner", "due", "decision", "contract review", "approval")
CONTRACT_KEYWORDS = ("msa", "sow", "contract", "pricing", "terms", "commitment", "renewal", "obligation")


def classify_lines(lines: list[str], file_name: str) -> dict[str, list[str]]:
    buckets = {"Signals": [], "Risks": [], "Opportunities": [], "Actions": [], "Sources": []}
    for raw in lines[:40]:
        line = raw.strip()
        if len(line) < 12:
            continue
        lower = line.lower()
        buckets["Signals"].append(f"SharePoint signal from {file_name}: {line}")
        if any(keyword in lower for keyword in RISK_KEYWORDS):
            buckets["Risks"].append(f"SharePoint evidence suggests a risk: {line}")
        if any(keyword in lower for keyword in OPPORTUNITY_KEYWORDS):
            buckets["Opportunities"].append(f"SharePoint evidence suggests an opportunity: {line}")
        if any(keyword in lower for keyword in ACTION_KEYWORDS):
            buckets["Actions"].append(f"SharePoint follow-up: {line}")
        if any(keyword in lower for keyword in CONTRACT_KEYWORDS):
            buckets["Signals"].append(f"Contract-related SharePoint evidence from {file_name}: {line}")
    buckets["Sources"].append(f"SharePoint export: {file_name}")
    return buckets


def ingest_sharepoint_exports(customer_slug: str, input_files: list[str] | None = None) -> bool:
    ensure_repo_structure()
    customer = load_customer_context(customer_slug)
    files = [Path(value) for value in input_files] if input_files else discover_customer_files(SHAREPOINT_DIR, customer_slug)
    changed = False
    for file_path in files:
        if not file_path.exists():
            continue
        classified = classify_lines(read_text_payload(file_path), file_path.name)
        file_changed = False
        for section in ("Signals", "Risks", "Opportunities", "Actions"):
            if append_unique_lines(customer, section, classified[section]):
                file_changed = True
        if file_changed:
            append_unique_lines(customer, "Sources", classified["Sources"])
            append_unique_lines(customer, "Change Log", [f"Imported SharePoint export content from {file_path.name}"])
            changed = True
    if changed:
        save_customer_context(customer)
    return changed


def main() -> None:
    parser = argparse.ArgumentParser(description="Ingest exported SharePoint content for a customer.")
    parser.add_argument("--customer", required=True, help="Customer slug, for example boots.")
    parser.add_argument("--input", action="append", help="Optional input file path. Can be repeated.")
    args = parser.parse_args()
    changed = ingest_sharepoint_exports(args.customer, args.input)
    print("updated" if changed else "no changes")


if __name__ == "__main__":
    main()
