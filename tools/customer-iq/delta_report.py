import argparse
import json
from pathlib import Path


def parse_sections(text: str) -> dict[str, list[str]]:
    sections: dict[str, list[str]] = {"Document": []}
    current = "Document"
    for raw_line in text.splitlines():
        line = raw_line.rstrip()
        if line.startswith("## "):
            current = line[3:].strip()
            sections.setdefault(current, [])
            continue
        if line.strip():
            sections.setdefault(current, []).append(line.strip())
    return sections


def compare(previous: str, current: str) -> dict[str, dict[str, list[str]]]:
    prev_sections = parse_sections(previous)
    curr_sections = parse_sections(current)
    names = sorted(set(prev_sections) | set(curr_sections))
    diff: dict[str, dict[str, list[str]]] = {}
    for name in names:
        prev_lines = set(prev_sections.get(name, []))
        curr_lines = set(curr_sections.get(name, []))
        added = sorted(curr_lines - prev_lines)
        removed = sorted(prev_lines - curr_lines)
        if added or removed:
            diff[name] = {"added": added, "removed": removed}
    return diff


def to_markdown(diff: dict[str, dict[str, list[str]]]) -> str:
    lines = ["## Delta Summary", ""]
    if not diff:
        lines.append("- No meaningful line-level changes detected.")
        return "\n".join(lines) + "\n"

    for section, changes in diff.items():
        lines.extend([f"### {section}", ""])
        for line in changes["added"]:
            lines.append(f"- Added: {line}")
        for line in changes["removed"]:
            lines.append(f"- Removed: {line}")
        lines.append("")
    return "\n".join(lines).rstrip() + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate a compact delta report between two markdown artifacts.")
    parser.add_argument("--previous", required=True, help="Path to the previous artifact.")
    parser.add_argument("--current", required=True, help="Path to the current artifact.")
    parser.add_argument("--output-format", choices=["markdown", "json"], default="markdown")
    args = parser.parse_args()

    previous = Path(args.previous).read_text(encoding="utf-8")
    current = Path(args.current).read_text(encoding="utf-8")
    diff = compare(previous, current)

    if args.output_format == "json":
        print(json.dumps(diff, indent=2))
        return

    print(to_markdown(diff))


if __name__ == "__main__":
    main()
