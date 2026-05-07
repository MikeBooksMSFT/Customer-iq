from __future__ import annotations

import argparse
from pathlib import Path

from customer_iq_core import (
    COSTS_DIR,
    append_unique_lines,
    discover_input_files,
    ensure_repo_structure,
    load_customer_context,
    read_text_payload,
    save_customer_context,
)


def build_cost_signals(path: Path) -> list[str]:
    raw_lines = read_text_payload(path)
    return [f"Cost signal from {path.name}: {line}" for line in raw_lines[:10]]


def analyze_customer_costs(customer_slug: str, input_files: list[str] | None = None) -> bool:
    ensure_repo_structure()
    customer = load_customer_context(customer_slug)
    files = [Path(value) for value in input_files] if input_files else discover_input_files(COSTS_DIR, customer_slug)
    changed = False
    for file_path in files:
        if not file_path.exists():
            continue
        signals = build_cost_signals(file_path)
        if append_unique_lines(customer, "Signals", signals, f"Analyzed cost signals from {file_path.name}"):
            append_unique_lines(customer, "Sources", [f"Cost source: {file_path.name}"])
            changed = True
    if changed:
        save_customer_context(customer)
    return changed


def main() -> None:
    parser = argparse.ArgumentParser(description="Analyze cost signals for a customer.")
    parser.add_argument("--customer", required=True, help="Customer slug.")
    parser.add_argument("--input", action="append", help="Optional input file path. Can be repeated.")
    args = parser.parse_args()
    changed = analyze_customer_costs(args.customer, args.input)
    print("updated" if changed else "no changes")


if __name__ == "__main__":
    main()
