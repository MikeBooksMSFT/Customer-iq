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
SHAREPOINT_DIR = INPUTS_DIR / "sharepoint"
MSX_SNAPSHOTS_DIR = OUTPUT_DIR / "msx"
CUSTOMER_CANDIDATES_FILE = REPO_ROOT / "context" / "customer-candidates.json"

STANDARD_CONTEXT_SECTIONS = [
    "Metadata",
    "System Connections",
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
            "Metadata": [
                "TPID: 639155",
                "MSX account name: Walgreens",
                "MSX account ID: pending validation",
                "MSXi customer key: 639155",
                "MSX hyperlink: pending validation",
                "SharePoint site: pending validation",
                "Managed sites: pending validation",
                "Validation status: MSX candidate identified; SharePoint site still required.",
            ],
            "System Connections": [
                "MSX: candidate matched to Walgreens (TPID 639155).",
                "MSXi: expected to use TPID 639155 as the grounding identifier.",
                "SharePoint: site URL not yet captured in repository context.",
                "Managed sites: no validated managed-site URLs are stored yet.",
            ],
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
            "Metadata": [
                "TPID: 1197953",
                "MSX account name: Boots UK",
                "MSX account ID: cb0983b7-c5e4-4aa4-8e94-17332ff7d7dc",
                "MSXi customer key: 1197953",
                "MSX hyperlink: pending validation",
                "SharePoint site: pending validation",
                "Managed sites: pending validation",
                "Validation status: Boots UK selected from MSX search candidates; confirm TPID and site URL with the account team.",
            ],
            "System Connections": [
                "MSX: candidate matched to Boots UK (TPID 1197953).",
                "MSXi: expected to use TPID 1197953 as the grounding identifier after validation.",
                "SharePoint: site URL not yet captured in repository context.",
                "Managed sites: no validated managed-site URLs are stored yet.",
            ],
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

DEFAULT_CUSTOMER_CANDIDATES = {
    "generatedAt": "2026-05-07",
    "customers": [
        {
            "query": "Walgreens",
            "status": "candidate-review-required",
            "notes": [
                "Exact TPID lookup for 639155 returned I-TRAX, so Walgreens still needs explicit validation before treating that TPID as confirmed.",
                "Name-based search returned multiple Walgreens account records."
            ],
            "candidates": [
                {"source": "MSX Dataverse", "name": "Walgreens", "accountId": "ce321577-979f-4a29-80e2-003e4500f14c", "tpid": "", "selected": False},
                {"source": "MSX Dataverse", "name": "WALGREENS", "accountId": "fefb9ce9-7b9a-44aa-a3ca-01461a2053fa", "tpid": "", "selected": False},
                {"source": "MSX Dataverse", "name": "Walgreens", "accountId": "6bf19d34-bc45-4569-ab9b-01d93b4a33ba", "tpid": "", "selected": False},
                {"source": "MSX Dataverse", "name": "Walgreens", "accountId": "05ff8b5d-3027-47b4-be3e-04e52e9bd532", "tpid": "", "selected": False},
                {"source": "MSX Dataverse", "name": "Walgreens", "accountId": "4028cc78-0345-4856-86ae-0746d8d49838", "tpid": "", "selected": False}
            ]
        },
        {
            "query": "Boots",
            "status": "candidate-selected-pending-site-validation",
            "notes": [
                "Boots UK was selected from MSX search candidates and exact lookup returned TPID 1197953 with an account record.",
                "SharePoint and managed-site URLs still need validation."
            ],
            "candidates": [
                {"source": "MSX Account Overview", "name": "Boots UK", "accountId": "cb0983b7-c5e4-4aa4-8e94-17332ff7d7dc", "tpid": "1197953", "selected": True},
                {"source": "MSX Dataverse", "name": "Boots", "accountId": "7577f257-b5b2-f011-bbd3-7c1e520bce7a", "tpid": "", "selected": False}
            ]
        }
    ]
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
    for path in (CONTEXT_DIR, OUTPUT_DIR, WORKFLOWS_DIR, MEETINGS_DIR, DOCUMENTS_DIR, COSTS_DIR, SHAREPOINT_DIR, MSX_SNAPSHOTS_DIR):
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


def build_customer_section_lines(
    *,
    tpid: str = "",
    msx_account_name: str = "",
    msx_account_id: str = "",
    msxi_key: str = "",
    msx_hyperlink: str = "",
    sharepoint_site: str = "",
    managed_sites: str = "",
    validation_status: str = "",
    aliases: str = "",
) -> tuple[list[str], list[str]]:
    metadata: list[str] = []
    connections: list[str] = []

    if tpid:
        metadata.append(f"TPID: {tpid}")
        if not msxi_key:
            msxi_key = tpid
    if msx_account_name:
        metadata.append(f"MSX account name: {msx_account_name}")
        connections.append(f"MSX: grounded to {msx_account_name}{f' (TPID {tpid})' if tpid else ''}.")
    metadata.append(f"MSX account ID: {msx_account_id}" if msx_account_id else "MSX account ID: pending validation")
    if msxi_key:
        metadata.append(f"MSXi customer key: {msxi_key}")
        connections.append(f"MSXi: grounded to customer key {msxi_key}.")
    metadata.append(f"MSX hyperlink: {msx_hyperlink}" if msx_hyperlink else "MSX hyperlink: pending validation")
    if msx_hyperlink:
        connections.append(f"MSX hyperlink: {msx_hyperlink}.")
    metadata.append(f"SharePoint site: {sharepoint_site}" if sharepoint_site else "SharePoint site: pending validation")
    if sharepoint_site:
        connections.append(f"SharePoint: source site configured as {sharepoint_site}.")
    else:
        connections.append("SharePoint: site URL not yet captured in repository context.")
    metadata.append(f"Managed sites: {managed_sites}" if managed_sites else "Managed sites: pending validation")
    if managed_sites:
        connections.append(f"Managed sites: {managed_sites}.")
    else:
        connections.append("Managed sites: no validated managed-site URLs are stored yet.")
    if aliases:
        metadata.append(f"Aliases: {aliases}")
    if validation_status:
        metadata.append(f"Validation status: {validation_status}")

    return dedupe_preserve(metadata), dedupe_preserve(connections)


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


def msx_snapshot_file_for(customer_slug: str) -> Path:
    return MSX_SNAPSHOTS_DIR / f"{customer_slug}.json"


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
    for name in ("Metadata", "System Connections", "Overview", "Signals", "Opportunities", "Risks", "Actions"):
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
    current_state = dedupe_preserve(
        bulletize(sections.get("Metadata", []))
        + bulletize(sections.get("System Connections", []))
        + bulletize(sections.get("Overview", []))
        + bulletize(sections.get("Signals", []))
    )[:8]
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
    elif "tpid" in prompt or "sharepoint" in prompt or "msx" in prompt or "msxi" in prompt or "site" in prompt:
        relevant = bulletize(customer["sections"].get("Metadata", [])) + bulletize(customer["sections"].get("System Connections", []))  # type: ignore[index]
        heading = "Customer system profile"
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


def discover_customer_files(directory: Path, customer_slug: str) -> list[Path]:
    direct = discover_input_files(directory, customer_slug)
    nested_dir = directory / customer_slug
    nested = sorted(path for path in nested_dir.rglob("*") if path.is_file()) if nested_dir.exists() else []
    return sorted({path.resolve(): path for path in (direct + nested)}.values(), key=lambda path: str(path).lower())


def read_text_payload(path: Path) -> list[str]:
    suffix = path.suffix.lower()
    if suffix in {".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx"}:
        return [f"Unsupported binary document: {path.name}. Export the relevant content to .txt, .md, .csv, or .json before ingestion."]
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


def load_customer_candidates() -> dict[str, object]:
    ensure_repo_structure()
    if not CUSTOMER_CANDIDATES_FILE.exists():
        CUSTOMER_CANDIDATES_FILE.write_text(json.dumps(DEFAULT_CUSTOMER_CANDIDATES, indent=2), encoding="utf-8")
    return json.loads(CUSTOMER_CANDIDATES_FILE.read_text(encoding="utf-8"))


def search_customer_candidates(query: str) -> dict[str, object]:
    registry = load_customer_candidates()
    q = query.strip().lower()
    matches = []
    for record in registry.get("customers", []):
        record_query = str(record.get("query", "")).lower()
        candidate_names = " ".join(str(item.get("name", "")) for item in record.get("candidates", []))
        if not q or q in record_query or q in candidate_names.lower():
            matches.append(record)
    return {"query": query, "matches": matches}


def metadata_value(customer_slug: str, label: str) -> str:
    customer = load_customer_context(customer_slug)
    for line in bulletize(customer["sections"].get("Metadata", [])):  # type: ignore[index]
        if ":" not in line:
            continue
        key, value = line.split(":", 1)
        if key.strip().lower() == label.strip().lower():
            return value.strip()
    return ""


def load_msx_snapshot(customer_slug: str) -> dict[str, object]:
    ensure_repo_structure()
    path = msx_snapshot_file_for(customer_slug)
    if not path.exists():
        return {
            "customer": customer_slug,
            "status": "missing",
            "message": "No MSX opportunity snapshot has been stored for this customer yet.",
            "opportunities": [],
        }
    data = json.loads(path.read_text(encoding="utf-8"))
    data.setdefault("customer", customer_slug)
    data.setdefault("status", "available")
    data.setdefault("opportunities", [])
    return data


def access_summary() -> dict[str, object]:
    return {
        "publicStaticSite": {
            "enabled": True,
            "mode": "read-only",
            "url": "https://mikebooksmsft.github.io/Customer-iq/",
        },
        "localFlaskApp": {
            "enabled": True,
            "mode": "read-write",
            "notes": "Can create and update repo-backed customer profiles, view stored MSX opportunity snapshots, and generate Customer IQ locally.",
        },
        "liveSystemConnectors": {
            "msx": "snapshot-backed view only; live MCP auth is not wired into the app runtime yet",
            "msxi": "not wired into app runtime yet",
            "sharepoint": "ingestion is repo-grounded from exported files, not a direct live connector yet",
        },
        "groundingFields": [
            "TPID",
            "MSX account name",
            "MSX account ID",
            "MSXi key",
            "MSX hyperlink",
            "SharePoint site",
            "Managed sites",
            "Validation status",
        ],
    }


def upsert_customer_profile(
    *,
    customer_name: str,
    tpid: str = "",
    msx_account_name: str = "",
    msx_account_id: str = "",
    msxi_key: str = "",
    msx_hyperlink: str = "",
    sharepoint_site: str = "",
    managed_sites: str = "",
    validation_status: str = "",
    aliases: str = "",
    notes: str = "",
) -> dict[str, object]:
    slug = slugify_customer(customer_name)
    customer = load_customer_context(slug)
    customer["name"] = normalize_customer_name(customer_name, slug)

    metadata_lines, connection_lines = build_customer_section_lines(
        tpid=tpid,
        msx_account_name=msx_account_name,
        msx_account_id=msx_account_id,
        msxi_key=msxi_key,
        msx_hyperlink=msx_hyperlink,
        sharepoint_site=sharepoint_site,
        managed_sites=managed_sites,
        validation_status=validation_status,
        aliases=aliases,
    )

    sections: dict[str, list[str]] = customer["sections"]  # type: ignore[assignment]
    sections["Metadata"] = metadata_lines or sections.get("Metadata", [])
    sections["System Connections"] = connection_lines or sections.get("System Connections", [])

    timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    append_unique_lines(customer, "Change Log", [f"{timestamp}: Updated customer onboarding profile"])
    if notes:
        append_unique_lines(customer, "Signals", [notes])

    save_customer_context(customer)
    return customer

