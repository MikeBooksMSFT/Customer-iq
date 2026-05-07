from __future__ import annotations

import csv
import json
import re
from copy import deepcopy
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable


REPO_ROOT = Path(__file__).resolve().parents[2]
CONTEXT_DIR = REPO_ROOT / "context" / "customer"
OUTPUT_DIR = REPO_ROOT / "outputs"
WORKFLOWS_DIR = REPO_ROOT / "workflows"
INPUTS_DIR = REPO_ROOT / "inputs"
MEETINGS_DIR = INPUTS_DIR / "meetings"
DOCUMENTS_DIR = INPUTS_DIR / "documents"
COSTS_DIR = INPUTS_DIR / "costs"

STANDARD_CONTEXT_SECTIONS = [
    "Overview",
    "Signals",
    "Opportunities",
    "Risks",
    "Actions",
    "Change Log",
    "Sources",
]

DEFAULT_CUSTOMERS: dict[str, dict[str, object]] = {
    "walgreens": {
        "name": "Walgreens",
        "sections": {
            "Overview": [
                "Large retail and pharmacy company.",
                "Customer IQ is currently tracking digital transformation, AI exploration, and data modernization themes.",
            ],
            "Signals": [
                "Existing repo context references SAP BW, Databricks, and Azure in the architecture.",
                "Existing repo context references exploration of AI, Copilot, and data modernization.",
                "Named stakeholders, budget, and timeline are not yet stored in repo context.",
            ],
            "Opportunities": [
                "Define customer-backed Copilot and AI use cases once named sponsors and business outcomes are captured.",
                "Position Azure as a modernization platform adjacent to the currently referenced data estate.",
            ],
            "Risks": [
                "The account does not yet have a documented executive sponsor in repo context.",
                "No measurable cost, usage, or consumption baseline is stored yet.",
            ],
            "Actions": [
                "Add sourced meeting notes, architecture evidence, and cost signals to the repository.",
                "Confirm stakeholders, program owners, and business outcomes for active transformation work.",
            ],
            "Change Log": [
                "2026-05-07: Seeded structured Walgreens context for Customer IQ platform initialization."
            ],
            "Sources": [
                "Repo bootstrap notes created during Customer IQ setup."
            ],
        },
    },
    "boots": {
        "name": "Boots",
        "sections": {
            "Overview": [
                "Placeholder customer record for Boots.",
                "Customer IQ is ready to store sourced customer evidence for this account.",
            ],
            "Signals": [
                "No meeting, document, or cost signals have been ingested yet.",
                "No architecture, stakeholder, or initiative details are currently stored in repo context.",
            ],
            "Opportunities": [
                "Gather initial account evidence before attempting opportunity qualification.",
                "Use the first ingestion pass to identify strategic priorities, architecture signals, and decision-makers.",
            ],
            "Risks": [
                "The account currently has insufficient evidence for confident opportunity or risk assessment.",
                "Without sourced inputs, generated intelligence will remain generic and explicitly incomplete.",
            ],
            "Actions": [
                "Add initial meeting notes, account background, and architecture context for Boots.",
                "Ingest the first set of signals before using the interaction-agent for planning decisions.",
            ],
            "Change Log": [
                "2026-05-07: Seeded placeholder Boots context for multi-customer Customer IQ support."
            ],
            "Sources": [
                "Placeholder repository initialization record."
            ],
        },
    },
}


def slugify_customer(value: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", value.strip().lower()).strip("-")


def normalize_customer_name(name: str, slug: str | None = None) -> str:
    cleaned = name.strip()
    if cleaned.lower().endswith(" overview"):
        cleaned = cleaned[: -len(" overview")].strip()
    if cleaned:
        return cleaned
    if slug and slug in DEFAULT_CUSTOMERS:
        return str(DEFAULT_CUSTOMERS[slug]["name"])
    return "Unknown Customer"


def ensure_repo_structure() -> None:
    for path in (CONTEXT_DIR, OUTPUT_DIR, WORKFLOWS_DIR, MEETINGS_DIR, DOCUMENTS_DIR, COSTS_DIR):
        path.mkdir(parents=True, exist_ok=True)


def dedupe_preserve(lines: Iterable[str]) -> list[str]:
    seen: set[str] = set()
    result: list[str] = []
    for raw in lines:
        line = raw.strip()
        if not line:
            continue
        key = line.lower()
        if key not in seen:
            seen.add(key)
            result.append(line)
    return result


def bulletize(lines: Iterable[str]) -> list[str]:
    output: list[str] = []
    for raw in lines:
        line = raw.strip()
        if not line:
            continue
        if line.startswith("- "):
            output.append(line[2:].strip())
        else:
            output.append(line)
    return dedupe_preserve(output)


def parse_markdown_sections(text: str) -> tuple[str, dict[str, list[str]]]:
    title = ""
    sections: dict[str, list[str]] = {section: [] for section in STANDARD_CONTEXT_SECTIONS}
    current = "Overview"

    for raw_line in text.splitlines():
        line = raw_line.rstrip()
        if line.startswith("# "):
            title = line[2:].strip()
            continue
        if line.startswith("## "):
            section_name = line[3:].strip()
            current = section_name if section_name in sections else current
            sections.setdefault(current, [])
            continue
        if line.strip():
            sections.setdefault(current, []).append(line.strip())

    normalized = {section: bulletize(sections.get(section, [])) for section in STANDARD_CONTEXT_SECTIONS}
    return title, normalized


def parse_generic_markdown_sections(text: str) -> dict[str, list[str]]:
    sections: dict[str, list[str]] = {}
    current = "Document"
    sections[current] = []

    for raw_line in text.splitlines():
        line = raw_line.rstrip()
        if line.startswith("## "):
            current = line[3:].strip()
            sections.setdefault(current, [])
            continue
        if line.strip() and not line.startswith("# "):
            sections.setdefault(current, []).append(line.strip())

    return {section: bulletize(lines) for section, lines in sections.items()}


def render_context(customer_name: str, sections: dict[str, list[str]]) -> str:
    parts = [f"# {customer_name}", ""]
    for section in STANDARD_CONTEXT_SECTIONS:
        parts.extend([f"## {section}", ""])
        values = bulletize(sections.get(section, []))
        if values:
            parts.extend([f"- {value}" for value in values])
            parts.append("")
    return "\n".join(parts).rstrip() + "\n"


def default_customer_record(customer_slug: str) -> dict[str, object]:
    template = DEFAULT_CUSTOMERS.get(
        customer_slug,
        {
            "name": normalize_customer_name(customer_slug.replace("-", " ").title(), customer_slug),
            "sections": {section: [] for section in STANDARD_CONTEXT_SECTIONS},
        },
    )
    return {
        "slug": customer_slug,
        "name": str(template["name"]),
        "sections": deepcopy(template["sections"]),
    }


def context_file_for(customer_slug: str) -> Path:
    return CONTEXT_DIR / f"{customer_slug}.md"


def output_file_for(customer_slug: str) -> Path:
    return OUTPUT_DIR / f"{customer_slug}-iq.md"


def load_customer_context(customer_slug: str) -> dict[str, object]:
    ensure_repo_structure()
    path = context_file_for(customer_slug)
    if not path.exists():
        record = default_customer_record(customer_slug)
        path.write_text(render_context(str(record["name"]), record["sections"]), encoding="utf-8")
        return record

    title, sections = parse_markdown_sections(path.read_text(encoding="utf-8"))
    defaults = default_customer_record(customer_slug)
    merged_sections: dict[str, list[str]] = {}
    for section in STANDARD_CONTEXT_SECTIONS:
        merged_sections[section] = bulletize(sections.get(section, []) or defaults["sections"].get(section, []))
    return {
        "slug": customer_slug,
        "name": normalize_customer_name(title, customer_slug) or str(defaults["name"]),
        "sections": merged_sections,
    }


def save_customer_context(customer: dict[str, object]) -> Path:
    path = context_file_for(str(customer["slug"]))
    path.write_text(render_context(str(customer["name"]), customer["sections"]), encoding="utf-8")
    return path


def list_customers(customer_arg: str = "all") -> list[str]:
    ensure_repo_structure()
    if customer_arg != "all":
        return [slugify_customer(customer_arg)]

    existing = sorted(path.stem for path in CONTEXT_DIR.glob("*.md"))
    if existing:
        return existing
    return sorted(DEFAULT_CUSTOMERS.keys())


def append_unique_lines(customer: dict[str, object], section: str, new_lines: Iterable[str], change_summary: str | None = None) -> bool:
    sections: dict[str, list[str]] = customer["sections"]  # type: ignore[assignment]
    existing = bulletize(sections.get(section, []))
    additions = [line for line in bulletize(new_lines) if line.lower() not in {item.lower() for item in existing}]
    if not additions:
        return False
    sections[section] = existing + additions
    if change_summary:
        stamped = f"{datetime.now(timezone.utc).strftime('%Y-%m-%d')}: {change_summary}"
        append_unique_lines(customer, "Change Log", [stamped], None)
    return True


def sentence_list(items: list[str]) -> str:
    normalized = [item.rstrip(".") for item in items]
    if not items:
        return "limited repository evidence"
    if len(normalized) == 1:
        return normalized[0]
    if len(normalized) == 2:
        return f"{normalized[0]} and {normalized[1]}"
    return f"{', '.join(normalized[:-1])}, and {normalized[-1]}"


def gather_facts(customer: dict[str, object]) -> list[str]:
    sections: dict[str, list[str]] = customer["sections"]  # type: ignore[assignment]
    facts: list[str] = []
    for name in ("Overview", "Signals", "Opportunities", "Risks", "Actions"):
        facts.extend(bulletize(sections.get(name, [])))
    return dedupe_preserve(facts)


def evidence_for(facts: list[str], keywords: Iterable[str]) -> list[str]:
    keyword_list = [keyword.lower() for keyword in keywords]
    return [fact for fact in facts if any(keyword in fact.lower() for keyword in keyword_list)][:2]


def generate_opportunities(customer: dict[str, object], facts: list[str]) -> list[str]:
    sections: dict[str, list[str]] = customer["sections"]  # type: ignore[assignment]
    explicit = bulletize(sections.get("Opportunities", []))
    if explicit:
        return explicit

    candidates = [
        ("Define AI and Copilot use cases", ("ai", "copilot")),
        ("Modernize the data estate on Azure", ("azure", "data", "modernization", "databricks", "sap")),
        ("Improve account qualification with stronger evidence", ("stakeholder", "budget", "timeline", "missing")),
    ]
    results: list[str] = []
    for title, keywords in candidates:
        matches = evidence_for(facts, keywords)
        if matches:
            results.append(f"{title}. Evidence: {sentence_list(matches)}.")
    return results or ["The current context is too limited for a confident opportunity map."]


def generate_risks(customer: dict[str, object], facts: list[str]) -> list[str]:
    sections: dict[str, list[str]] = customer["sections"]  # type: ignore[assignment]
    explicit = bulletize(sections.get("Risks", []))
    results = explicit[:]
    if not any("stakeholder" in fact.lower() or "sponsor" in fact.lower() for fact in facts):
        results.append("Customer stakeholders and sponsors are not yet captured in repository context.")
    if not any("cost" in fact.lower() or "consumption" in fact.lower() for fact in facts):
        results.append("Cost and consumption evidence is missing, which limits prioritization confidence.")
    return dedupe_preserve(results)


def generate_actions(customer: dict[str, object], facts: list[str]) -> list[str]:
    sections: dict[str, list[str]] = customer["sections"]  # type: ignore[assignment]
    explicit = bulletize(sections.get("Actions", []))
    if explicit:
        return explicit

    actions = [
        "Ingest additional customer evidence into the repository.",
        "Confirm stakeholders, owners, and business outcomes.",
        "Refresh Customer IQ after new evidence is added.",
    ]
    if any("cost" in fact.lower() or "consumption" in fact.lower() for fact in facts):
        actions.append("Review cost and consumption signals for optimization or expansion opportunities.")
    return dedupe_preserve(actions)


def generate_microsoft_play(customer: dict[str, object], facts: list[str]) -> str:
    text = "Use the strongest supported customer signals to align Azure, data modernization, and productivity or AI motions without overreaching beyond the available evidence."
    lower_facts = " ".join(facts).lower()
    if "copilot" in lower_facts and "azure" in lower_facts:
        return "Position a combined Azure plus Copilot motion that connects productivity gains with a broader modernization narrative."
    if "azure" in lower_facts:
        return "Anchor the account strategy on Azure-backed modernization while collecting the missing business and stakeholder evidence needed for expansion."
    if "copilot" in lower_facts:
        return "Use Copilot interest as the lead motion, but require stronger business-owner and outcome evidence before broadening the play."
    return text


def generate_customer_iq(customer_slug: str) -> str:
    customer = load_customer_context(customer_slug)
    facts = gather_facts(customer)
    sections: dict[str, list[str]] = customer["sections"]  # type: ignore[assignment]
    summary_facts = facts[:3]
    current_state = dedupe_preserve(bulletize(sections.get("Overview", [])) + bulletize(sections.get("Signals", [])))[:6]
    opportunities = generate_opportunities(customer, facts)
    risks = generate_risks(customer, facts)
    actions = generate_actions(customer, facts)

    lines = [
        "## 1. Executive Summary",
        "",
        f"Based on the available repository context, {customer['name']} is associated with {sentence_list(summary_facts)}.",
        "",
        "## 2. Current State",
        "",
    ]
    if current_state:
        lines.extend([f"- {item}" for item in current_state])
    else:
        lines.append("- No durable customer state has been recorded yet.")
    lines.extend(
        [
            "- Missing information should be treated as unknown rather than assumed.",
            "",
            "## 3. Opportunities",
            "",
        ]
    )
    lines.extend([f"- {item}" for item in opportunities])
    lines.extend(
        [
            "",
            "## 4. Microsoft Play",
            "",
            generate_microsoft_play(customer, facts),
            "",
            "## 5. Risks",
            "",
        ]
    )
    lines.extend([f"- {item}" for item in risks])
    lines.extend(
        [
            "",
            "## 6. Next Actions",
            "",
        ]
    )
    lines.extend([f"- {item}" for item in actions])
    return "\n".join(lines).rstrip() + "\n"


def save_customer_iq(customer_slug: str) -> Path:
    output_path = output_file_for(customer_slug)
    output_path.write_text(generate_customer_iq(customer_slug), encoding="utf-8")
    return output_path


def iq_sections(customer_slug: str) -> dict[str, list[str]]:
    output_path = output_file_for(customer_slug)
    if not output_path.exists():
        save_customer_iq(customer_slug)
    return parse_generic_markdown_sections(output_path.read_text(encoding="utf-8"))


def answer_customer_question(customer_slug: str, question: str) -> str:
    customer = load_customer_context(customer_slug)
    facts = gather_facts(customer)
    iq = iq_sections(customer_slug)
    prompt = question.lower()

    if "risk" in prompt:
        relevant = bulletize(iq.get("5. Risks", [])) or generate_risks(customer, facts)
        heading = "Top risks"
    elif "opportun" in prompt or "play" in prompt:
        relevant = bulletize(iq.get("3. Opportunities", [])) or generate_opportunities(customer, facts)
        heading = "Top opportunities"
    elif "next" in prompt or "action" in prompt:
        relevant = bulletize(iq.get("6. Next Actions", [])) or generate_actions(customer, facts)
        heading = "Recommended next actions"
    elif "state" in prompt or "status" in prompt or "summary" in prompt:
        relevant = bulletize(iq.get("2. Current State", [])) or facts[:5]
        heading = "Current state"
    else:
        relevant = facts[:5] or ["The repository does not yet contain enough evidence to answer confidently."]
        heading = "Best available answer"

    evidence = facts[:3]
    lines = [
        f"{heading} for {customer['name']}:",
        "",
    ]
    lines.extend([f"- {item}" for item in relevant[:5]])
    lines.extend(
        [
            "",
            "Supporting evidence:",
            "",
        ]
    )
    lines.extend([f"- {item}" for item in evidence])
    if not facts:
        lines.extend(["", "Missing information: no durable customer context is stored yet."])
    elif any("missing" in fact.lower() or "not yet" in fact.lower() for fact in facts):
        lines.extend(["", "Missing information: the stored context still contains explicit evidence gaps."])
    return "\n".join(lines).rstrip()


def discover_input_files(directory: Path, customer_slug: str) -> list[Path]:
    if not directory.exists():
        return []
    matches = sorted(directory.glob(f"{customer_slug}*"))
    return [path for path in matches if path.is_file()]


def read_text_payload(path: Path) -> list[str]:
    suffix = path.suffix.lower()
    if suffix == ".json":
        data = json.loads(path.read_text(encoding="utf-8"))
        if isinstance(data, list):
            return [json.dumps(item, ensure_ascii=True) if isinstance(item, (dict, list)) else str(item) for item in data]
        if isinstance(data, dict):
            return [f"{key}: {value}" for key, value in data.items()]
        return [str(data)]
    if suffix == ".csv":
        with path.open(encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            rows = []
            for row in reader:
                entries = [f"{key}={value}" for key, value in row.items() if value]
                if entries:
                    rows.append(", ".join(entries))
            return rows
    return [line.strip() for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
