import argparse
import csv
import json
import re
from pathlib import Path


def tokenize_customer(name: str) -> list[str]:
    return [part for part in re.split(r"[^a-z0-9]+", name.lower()) if len(part) > 2]


def parse_input(text: str) -> list[dict]:
    stripped = text.strip()
    if not stripped:
        return []

    if stripped.startswith("["):
        data = json.loads(stripped)
        if not isinstance(data, list):
            raise ValueError("Expected a JSON array of meeting records.")
        return [normalize_json_record(item) for item in data]

    lines = [line for line in stripped.splitlines() if line.strip()]
    header = lines[0]
    delimiter = "\t" if "\t" in header else "|"
    reader = csv.DictReader(lines, delimiter=delimiter)
    return [normalize_tabular_record(row) for row in reader]


def normalize_json_record(item: dict) -> dict:
    return {
        "start": str(item.get("start", "")).strip(),
        "subject": str(item.get("subject", "")).strip(),
        "organizer": str(item.get("organizer", "")).strip(),
        "attendees": str(item.get("attendees", "")).strip(),
        "location": str(item.get("location", "")).strip(),
        "external": bool(item.get("external", False)),
        "customer": bool(item.get("customer", False)),
        "id": str(item.get("id", "")).strip(),
    }


def normalize_tabular_record(row: dict) -> dict:
    normalized = {str(k or "").strip().lower(): str(v or "").strip() for k, v in row.items()}
    start_date = normalized.get("date", "")
    start_time = normalized.get("time", "")
    start = f"{start_date} {start_time}".strip()
    return {
        "start": start,
        "subject": normalized.get("subject", ""),
        "organizer": normalized.get("organizer", ""),
        "attendees": normalized.get("attendees", ""),
        "location": normalized.get("location", ""),
        "external": normalized.get("external", "").lower() == "true",
        "customer": normalized.get("customer", "").lower() == "true",
        "id": normalized.get("id", ""),
    }


def record_matches_customer(record: dict, customer_tokens: list[str]) -> bool:
    if record.get("customer"):
        return True
    haystack = " ".join(
        [
            record.get("subject", ""),
            record.get("attendees", ""),
            record.get("organizer", ""),
            record.get("location", ""),
        ]
    ).lower()
    return any(token in haystack for token in customer_tokens)


def subject_to_topic(subject: str) -> str:
    cleaned = re.sub(r"\s+", " ", subject).strip(" -|:")
    if not cleaned:
        return "Topic not visible in meeting subject."
    return cleaned


def to_markdown(customer: str, meetings: list[dict]) -> str:
    lines = [f"### Meeting Signals - {customer}", ""]
    if not meetings:
        lines.append("- No customer meetings were found in the provided calendar data.")
        return "\n".join(lines) + "\n"

    for meeting in meetings:
        subject = meeting.get("subject") or "Untitled meeting"
        start = meeting.get("start") or "Unknown date"
        organizer = meeting.get("organizer") or "Unknown organizer"
        attendees = meeting.get("attendees") or "Unknown attendees"
        topic = subject_to_topic(subject)
        lines.extend(
            [
                f"- **{start}** - {subject}",
                f"  - Organizer: {organizer}",
                f"  - Attendees: {attendees}",
                f"  - Topic signal: {topic}",
            ]
        )
    return "\n".join(lines) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description="Parse calendar-week output into Customer IQ meeting signals.")
    parser.add_argument("--customer", required=True, help="Customer name to match against meetings.")
    parser.add_argument("--input", required=True, help="Path to calendar-week output in JSON, TSV, or pipe-table format.")
    parser.add_argument(
        "--output-format",
        choices=["markdown", "json"],
        default="markdown",
        help="Output format.",
    )
    args = parser.parse_args()

    text = Path(args.input).read_text(encoding="utf-8")
    customer_tokens = tokenize_customer(args.customer)
    records = [record for record in parse_input(text) if record_matches_customer(record, customer_tokens)]

    if args.output_format == "json":
        print(json.dumps(records, indent=2))
        return

    print(to_markdown(args.customer, records))


if __name__ == "__main__":
    main()
