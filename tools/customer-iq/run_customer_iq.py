from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CONTEXT_DIR = ROOT / "context" / "customer"
OUTPUT_DIR = ROOT / "outputs"


def normalize_customer_name(name: str) -> str:
    normalized = name.strip()
    if normalized.lower().endswith(" overview"):
        normalized = normalized[: -len(" overview")].strip()
    return normalized


def parse_sections(text: str) -> tuple[str, dict[str, list[str]]]:
    title = ""
    sections: dict[str, list[str]] = {}
    current = "Overview"
    sections[current] = []

    for raw_line in text.splitlines():
        line = raw_line.rstrip()
        if line.startswith("# "):
            title = line[2:].strip()
            continue
        if line.startswith("## "):
            current = line[3:].strip()
            sections.setdefault(current, [])
            continue
        if line.strip():
            sections.setdefault(current, []).append(line.strip())

    return title, sections


def bullets(lines: list[str]) -> list[str]:
    extracted: list[str] = []
    for line in lines:
        if line.startswith("- "):
            extracted.append(line[2:].strip())
        else:
            extracted.append(line.strip())
    return [line for line in extracted if line]


def gather_facts(sections: dict[str, list[str]]) -> list[str]:
    facts: list[str] = []
    for name in ("Overview", "Signals", "Opportunities", "Risks", "Actions"):
        facts.extend(bullets(sections.get(name, [])))
    seen: set[str] = set()
    unique: list[str] = []
    for fact in facts:
        key = fact.lower()
        if key not in seen:
            seen.add(key)
            unique.append(fact)
    return unique


def evidence_for(facts: list[str], keywords: tuple[str, ...]) -> list[str]:
    matches = [fact for fact in facts if any(keyword in fact.lower() for keyword in keywords)]
    return matches[:2]


def sentence_list(items: list[str]) -> str:
    if not items:
        return "The available context is limited."
    if len(items) == 1:
        return items[0]
    if len(items) == 2:
        return f"{items[0]} and {items[1]}"
    return f"{', '.join(items[:-1])}, and {items[-1]}"


def build_opportunities(facts: list[str]) -> list[tuple[str, str, list[str]]]:
    candidates = [
        (
            "AI and Copilot adoption",
            "The context references active exploration of AI or Copilot, which suggests room for scoped use-case development.",
            ("ai", "copilot"),
        ),
        (
            "Data modernization",
            "The context references modernization or data-platform signals that could support a broader transformation motion.",
            ("data", "modernization", "databricks", "sap bw"),
        ),
        (
            "Azure expansion",
            "Existing Azure presence can reduce friction for follow-on platform or AI work.",
            ("azure",),
        ),
    ]

    opportunities: list[tuple[str, str, list[str]]] = []
    for title, why, keywords in candidates:
        evidence = evidence_for(facts, keywords)
        if evidence:
            opportunities.append((title, why, evidence))
    return opportunities


def build_risks(facts: list[str], opportunities: list[tuple[str, str, list[str]]]) -> list[tuple[str, str]]:
    risks: list[tuple[str, str]] = []
    if opportunities:
        risks.append(
            (
                "Limited supporting detail",
                "The current context does not include named stakeholders, timelines, or measurable business outcomes for the identified themes.",
            )
        )
    if not any("stakeholder" in fact.lower() for fact in facts):
        risks.append(
            (
                "Stakeholder visibility gap",
                "The available context does not identify customer decision-makers or sponsors.",
            )
        )
    if not any("cost" in fact.lower() or "consumption" in fact.lower() for fact in facts):
        risks.append(
            (
                "Consumption and value gap",
                "There is no cost, usage, or consumption signal in the current context.",
            )
        )
    return risks


def build_report(customer_name: str, facts: list[str], sections: dict[str, list[str]]) -> str:
    summary_facts = facts[:3]
    current_state = facts[:6]
    opportunities = build_opportunities(facts)
    risks = build_risks(facts, opportunities)
    change_log = bullets(sections.get("Change Log", []))

    lines: list[str] = []
    lines.extend(
        [
            "## 1. Executive Summary",
            "",
            f"Based on the available context, {customer_name} is associated with {sentence_list(summary_facts)}.",
            "",
            "## 2. Current State",
            "",
        ]
    )

    for fact in current_state:
        lines.append(f"- {fact}")
    if not current_state:
        lines.append("- No durable customer facts are available yet.")
    lines.extend(
        [
            "- Missing information: named stakeholders, timeline, business outcomes, and usage or cost evidence are not present unless explicitly stated above.",
            "",
            "## 3. Opportunities",
            "",
        ]
    )

    if opportunities:
        for index, (title, why, evidence) in enumerate(opportunities, start=1):
            lines.append(f"{index}. **{title}**")
            lines.append(f"   - Why it matters: {why}")
            lines.append(f"   - Evidence: {sentence_list(evidence)}")
    else:
        lines.append("1. **Opportunity not yet clear**")
        lines.append("   - Why it matters: The current context is too limited to identify a confident opportunity.")
        lines.append("   - Evidence: No specific business, technical, or stakeholder signal is present.")

    lines.extend(["", "## 4. Microsoft Play", ""])
    if opportunities:
        lines.append(
            "Focus on the supported themes in the context, prioritize Azure-connected modernization and AI motions where evidence exists, and avoid recommending unsupported product scope."
        )
    else:
        lines.append("The current context supports discovery and qualification more than a specific Microsoft play.")

    lines.extend(["", "## 5. Risks", ""])
    if risks:
        for index, (title, detail) in enumerate(risks, start=1):
            lines.append(f"{index}. **{title}**")
            lines.append(f"   - {detail}")
    else:
        lines.append("1. **No material risk identified from current context**")
        lines.append("   - The available context is too limited to surface a specific risk.")

    lines.extend(["", "## 6. Next Actions", ""])
    lines.append("1. Validate the highest-priority customer initiative referenced in the current context.")
    lines.append("2. Confirm the customer stakeholders, owners, and decision timeline.")
    if opportunities:
        lines.append("3. Turn the strongest supported opportunity into a scoped follow-up plan with explicit evidence.")
    else:
        lines.append("3. Gather more source material before attempting deeper account strategy.")
    if change_log:
        lines.append(f"4. Review recent change-log entries to understand what changed: {sentence_list(change_log[:2])}.")
    else:
        lines.append("4. Establish a delta baseline so future runs can track account changes over time.")

    return "\n".join(lines).rstrip() + "\n"


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    for context_file in sorted(CONTEXT_DIR.glob("*.md")):
        customer_name, sections = parse_sections(context_file.read_text(encoding="utf-8"))
        if not customer_name:
            customer_name = context_file.stem.replace("-", " ").title()
        customer_name = normalize_customer_name(customer_name)
        facts = gather_facts(sections)
        report = build_report(customer_name, facts, sections)
        output_path = OUTPUT_DIR / f"{context_file.stem}-iq.md"
        output_path.write_text(report, encoding="utf-8")
        print(f"updated {output_path}")


if __name__ == "__main__":
    main()
