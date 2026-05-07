from __future__ import annotations

from pathlib import Path

from append_context import append_change_log, ensure_sections, insert_into_section
from parse_calendar import parse_input, record_matches_customer, to_markdown, tokenize_customer


ROOT = Path(__file__).resolve().parents[2]
CONTEXT_DIR = ROOT / "context" / "customer"
MEETINGS_DIR = ROOT / "inputs" / "meetings"


def normalize_customer_name(name: str) -> str:
    normalized = name.strip()
    if normalized.lower().endswith(" overview"):
        normalized = normalized[: -len(" overview")].strip()
    return normalized


def customer_name_from_file(path: Path) -> str:
    text = path.read_text(encoding="utf-8") if path.exists() else ""
    for line in text.splitlines():
        if line.startswith("# "):
            return normalize_customer_name(line[2:].strip())
    return normalize_customer_name(path.stem.replace("-", " ").title())


def ingest_meeting_files(customer_name: str, customer_slug: str, text: str) -> str:
    if not MEETINGS_DIR.exists():
        return text

    tokens = tokenize_customer(customer_name)
    matching_files = sorted(MEETINGS_DIR.glob(f"{customer_slug}.*"))
    for meeting_file in matching_files:
        raw = meeting_file.read_text(encoding="utf-8")
        records = parse_input(raw)
        matched = [record for record in records if record_matches_customer(record, tokens)]
        snippet = to_markdown(customer_name, matched).strip()
        text, inserted = insert_into_section(text, "## Signals", snippet)
        if inserted:
            text = append_change_log(text, f"Ingested meeting signals from {meeting_file.name}")
    return text


def main() -> None:
    CONTEXT_DIR.mkdir(parents=True, exist_ok=True)

    for context_file in sorted(CONTEXT_DIR.glob("*.md")):
        original = context_file.read_text(encoding="utf-8")
        customer_name = customer_name_from_file(context_file)
        normalized = ensure_sections(original, customer_name)
        updated = ingest_meeting_files(customer_name, context_file.stem, normalized)
        if updated != original:
            context_file.write_text(updated, encoding="utf-8")
            print(f"updated {context_file}")


if __name__ == "__main__":
    main()
