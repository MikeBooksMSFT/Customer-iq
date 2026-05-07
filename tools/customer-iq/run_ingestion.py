from __future__ import annotations

import argparse

from cost_analysis import analyze_customer_costs
from customer_iq_core import ensure_repo_structure, list_customers, load_customer_context, save_customer_context
from document_ingestion import ingest_customer_documents
from meeting_ingestion import ingest_customer_meetings
from sharepoint_ingestion import ingest_sharepoint_exports


def run_ingestion(customer: str = "all") -> list[str]:
    ensure_repo_structure()
    updated: list[str] = []
    for customer_slug in list_customers(customer):
        context = load_customer_context(customer_slug)
        save_customer_context(context)
        meeting_changed = ingest_customer_meetings(customer_slug)
        document_changed = ingest_customer_documents(customer_slug)
        sharepoint_changed = ingest_sharepoint_exports(customer_slug)
        cost_changed = analyze_customer_costs(customer_slug)
        if meeting_changed or document_changed or sharepoint_changed or cost_changed:
            updated.append(customer_slug)
    return updated


def main() -> None:
    parser = argparse.ArgumentParser(description="Run Customer IQ ingestion for one customer or all customers.")
    parser.add_argument("--customer", default="all", help="Customer slug or all.")
    args = parser.parse_args()
    updated = run_ingestion(args.customer)
    if updated:
        print(f"updated: {', '.join(updated)}")
    else:
        print("no changes")


if __name__ == "__main__":
    main()
