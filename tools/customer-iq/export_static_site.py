from __future__ import annotations

import json

from customer_iq_core import (
    REPO_ROOT,
    ensure_repo_structure,
    iq_sections,
    list_customers,
    load_customer_context,
    load_msx_snapshot,
    output_file_for,
    save_customer_iq,
)


DOCS_DIR = REPO_ROOT / "docs"
DATA_DIR = DOCS_DIR / "data"


def export_site_data(customer: str = "all") -> list[str]:
    ensure_repo_structure()
    DATA_DIR.mkdir(parents=True, exist_ok=True)

    exported: list[str] = []
    index_payload: list[dict[str, str]] = []

    for slug in list_customers(customer):
        context = load_customer_context(slug)
        output_path = output_file_for(slug)
        if not output_path.exists():
          save_customer_iq(slug)
        payload = {
            "slug": slug,
            "name": context["name"],
            "context_sections": context["sections"],
            "sections": iq_sections(slug),
            "iq_markdown": output_path.read_text(encoding="utf-8"),
            "msx_snapshot": load_msx_snapshot(slug),
        }
        (DATA_DIR / f"{slug}.json").write_text(json.dumps(payload, indent=2), encoding="utf-8")
        index_payload.append({"slug": slug, "name": str(context["name"])})
        exported.append(slug)

    (DATA_DIR / "customers.json").write_text(json.dumps(index_payload, indent=2), encoding="utf-8")
    return exported


def main() -> None:
    exported = export_site_data("all")
    print(f"exported: {', '.join(exported)}")


if __name__ == "__main__":
    main()
