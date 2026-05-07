from __future__ import annotations

import argparse
from pathlib import Path

from customer_iq_core import (
    DOCUMENTS_DIR,
    append_unique_lines,
    discover_input_files,
    ensure_repo_structure,
    load_customer_context,
    read_text_payload,
    save_customer_context,
)


def build_document_signals(path: Path) -> list[str]:
    raw_lines = read_text_payload(path)
    return [f"Document signal from {path.name}: {line}" for line in raw_lines[:10]]


def ingest_customer_documents(customer_slug: str, input_files: list[str] | None = None) -> bool:
    ensure_repo_structure()
    customer = load_customer_context(customer_slug)
    files = [Path(value) for value in input_files] if input_files else discover_input_files(DOCUMENTS_DIR, customer_slug)
    changed = False
    for file_path in files:
        if not file_path.exists():
            continue
        signals = build_document_signals(file_path)
        if append_unique_lines(customer, "Signals", signals, f"Ingested document signals from {file_path.name}"):
            append_unique_lines(customer, "Sources", [f"Document source: {file_path.name}"])
            changed = True
    if changed:
        save_customer_context(customer)
    return changed


def main() -> None:
    parser = argparse.ArgumentParser(description="Ingest document signals for a customer.")
    parser.add_argument("--customer", required=True, help="Customer slug, for example boots.")
    parser.add_argument("--input", action="append", help="Optional input file path. Can be repeated.")
    args = parser.parse_args()
    changed = ingest_customer_documents(args.customer, args.input)
    print("updated" if changed else "no changes")


if __name__ == "__main__":
    main()
