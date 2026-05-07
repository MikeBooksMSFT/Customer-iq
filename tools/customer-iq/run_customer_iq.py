from __future__ import annotations

import argparse

from customer_iq_core import ensure_repo_structure, list_customers, save_customer_iq


def run_customer_iq(customer: str = "all") -> list[str]:
    ensure_repo_structure()
    updated: list[str] = []
    for customer_slug in list_customers(customer):
        save_customer_iq(customer_slug)
        updated.append(customer_slug)
    return updated


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate Customer IQ output for one customer or all customers.")
    parser.add_argument("--customer", default="all", help="Customer slug or all.")
    args = parser.parse_args()
    updated = run_customer_iq(args.customer)
    print(f"updated: {', '.join(updated)}")


if __name__ == "__main__":
    main()
