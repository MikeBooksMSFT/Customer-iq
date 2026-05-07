import argparse
from datetime import datetime, timezone
from pathlib import Path


SECTIONS = [
    "## Overview",
    "## Signals",
    "## Opportunities",
    "## Risks",
    "## Actions",
    "## Change Log",
    "## Sources",
]


def build_template(customer: str) -> str:
    parts = [f"# {customer}", ""]
    for section in SECTIONS:
        parts.extend([section, ""])
    return "\n".join(parts).rstrip() + "\n"


def ensure_sections(text: str, customer: str) -> str:
    base = text.strip()
    if not base:
        return build_template(customer)

    if not base.startswith("# "):
        base = f"# {customer}\n\n{base}"

    for section in SECTIONS:
        if section not in base:
            base = base.rstrip() + f"\n\n{section}\n"

    return base.rstrip() + "\n"


def insert_into_section(text: str, section: str, block: str) -> tuple[str, bool]:
    if block.strip() in text:
        return text, False

    marker = section
    start = text.index(marker) + len(marker)
    remainder = text[start:]
    next_index = remainder.find("\n## ")
    insertion = "\n\n" + block.strip() + "\n"

    if next_index == -1:
        section_body = remainder.rstrip()
        updated = text[:start] + section_body + insertion + "\n"
    else:
        section_body = remainder[:next_index].rstrip()
        updated = text[:start] + section_body + insertion + remainder[next_index:]
    return updated, True


def append_change_log(text: str, summary: str) -> str:
    timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    entry = f"- {timestamp}: {summary}"
    updated, inserted = insert_into_section(text, "## Change Log", entry)
    return updated if inserted else text


def main() -> None:
    parser = argparse.ArgumentParser(description="Merge a markdown snippet into a Customer IQ context file.")
    parser.add_argument("--customer", required=True, help="Customer name.")
    parser.add_argument("--context-file", required=True, help="Path to the customer context file.")
    parser.add_argument("--snippet-file", required=True, help="Path to a markdown snippet to merge.")
    parser.add_argument("--change-summary", required=True, help="Short description for the change log.")
    parser.add_argument(
        "--section",
        default="## Signals",
        choices=SECTIONS,
        help="Section to update with the snippet.",
    )
    args = parser.parse_args()

    context_path = Path(args.context_file)
    snippet = Path(args.snippet_file).read_text(encoding="utf-8").strip()
    existing = context_path.read_text(encoding="utf-8") if context_path.exists() else ""

    text = ensure_sections(existing, args.customer)
    text, inserted = insert_into_section(text, args.section, snippet)
    if inserted:
        text = append_change_log(text, args.change_summary)

    context_path.parent.mkdir(parents=True, exist_ok=True)
    context_path.write_text(text, encoding="utf-8")
    print("updated" if inserted else "no changes")


if __name__ == "__main__":
    main()
