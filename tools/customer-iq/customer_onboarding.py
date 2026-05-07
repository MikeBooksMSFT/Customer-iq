from __future__ import annotations

import argparse

from customer_iq_core import save_customer_iq, upsert_customer_profile


def prompt(label: str, default: str = "") -> str:
    suffix = f" [{default}]" if default else ""
    value = input(f"{label}{suffix}: ").strip()
    return value or default


def interactive_values() -> dict[str, str]:
    customer_name = prompt("Customer name")
    return {
        "customer_name": customer_name,
        "tpid": prompt("TPID"),
        "msx_account_name": prompt("MSX account name", customer_name),
        "msxi_key": prompt("MSXi customer key"),
        "msx_hyperlink": prompt("MSX hyperlink"),
        "sharepoint_site": prompt("SharePoint site URL"),
        "managed_sites": prompt("Managed sites (comma-separated URLs or names)"),
        "validation_status": prompt("Validation status", "Needs validation"),
        "aliases": prompt("Aliases"),
        "notes": prompt("Search note or onboarding comment"),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Add or update a Customer IQ customer profile.")
    parser.add_argument("--interactive", action="store_true", help="Prompt for values interactively.")
    parser.add_argument("--customer-name", help="Customer display name.")
    parser.add_argument("--tpid", default="", help="TPID.")
    parser.add_argument("--msx-account-name", default="", help="MSX account name.")
    parser.add_argument("--msxi-key", default="", help="MSXi customer key.")
    parser.add_argument("--msx-hyperlink", default="", help="MSX hyperlink.")
    parser.add_argument("--sharepoint-site", default="", help="SharePoint site URL.")
    parser.add_argument("--managed-sites", default="", help="Managed sites, URLs, or related grounded sources.")
    parser.add_argument("--validation-status", default="", help="Validation status.")
    parser.add_argument("--aliases", default="", help="Aliases.")
    parser.add_argument("--notes", default="", help="Optional note stored in Signals.")
    args = parser.parse_args()

    values = interactive_values() if args.interactive else {
        "customer_name": args.customer_name or "",
        "tpid": args.tpid,
        "msx_account_name": args.msx_account_name,
        "msxi_key": args.msxi_key,
        "msx_hyperlink": args.msx_hyperlink,
        "sharepoint_site": args.sharepoint_site,
        "managed_sites": args.managed_sites,
        "validation_status": args.validation_status,
        "aliases": args.aliases,
        "notes": args.notes,
    }

    if not values["customer_name"]:
        raise SystemExit("--customer-name is required unless --interactive is used")

    customer = upsert_customer_profile(**values)
    save_customer_iq(str(customer["slug"]))
    print(f"updated {customer['name']}")


if __name__ == "__main__":
    main()
