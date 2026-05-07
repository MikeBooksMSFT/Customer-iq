from __future__ import annotations

import argparse

from customer_iq_core import ensure_repo_structure, list_customers
from run_customer_iq import run_customer_iq
from run_ingestion import run_ingestion
from run_interaction import run_interaction


def main() -> None:
    parser = argparse.ArgumentParser(description="Run Customer IQ workflows and agents.")
    parser.add_argument("--workflow", default="agent-runner", choices=["agent-runner", "customer-iq", "ingestion", "intelligence", "interaction"])
    parser.add_argument("--customer", default="all", help="Customer slug or all.")
    parser.add_argument("--question", help="Optional question for interaction workflow.")
    args = parser.parse_args()

    ensure_repo_structure()

    if args.workflow in ("agent-runner", "customer-iq"):
        run_ingestion(args.customer)
        updated = run_customer_iq(args.customer)
        print(f"generated: {', '.join(updated)}")
        return

    if args.workflow == "ingestion":
        updated = run_ingestion(args.customer)
        print(f"ingested: {', '.join(updated) if updated else 'no changes'}")
        return

    if args.workflow == "intelligence":
        updated = run_customer_iq(args.customer)
        print(f"generated: {', '.join(updated)}")
        return

    if args.workflow == "interaction":
        if not args.question:
            raise SystemExit("--question is required for interaction workflow")
        customers = list_customers(args.customer)
        if len(customers) != 1:
            raise SystemExit("interaction workflow requires a single customer")
        print(run_interaction(customers[0], args.question))
        return


if __name__ == "__main__":
    main()
