from __future__ import annotations

import sys
from pathlib import Path

from flask import Flask, jsonify, render_template, request


REPO_ROOT = Path(__file__).resolve().parents[1]
TOOLS_DIR = REPO_ROOT / "tools" / "customer-iq"
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from customer_iq_core import (  # noqa: E402
    access_summary,
    list_customers,
    load_customer_context,
    load_msx_snapshot,
    output_file_for,
    save_customer_iq,
    search_customer_candidates,
    slugify_customer,
    upsert_customer_profile,
)
from run_interaction import run_interaction  # noqa: E402


app = Flask(__name__, template_folder="templates", static_folder="static")


def customer_payload() -> list[dict[str, str]]:
    return [{"slug": slug, "name": slug.replace("-", " ").title()} for slug in list_customers("all")]


@app.get("/")
def index() -> str:
    return render_template("index.html", customers=customer_payload())


@app.get("/api/customers")
def customers() -> object:
    return jsonify(customer_payload())


@app.get("/api/access")
def access() -> object:
    return jsonify(access_summary())


@app.get("/api/customer-search")
def customer_search() -> object:
    query = str(request.args.get("q", "")).strip()
    return jsonify(search_customer_candidates(query))


@app.get("/api/customer/<customer>/iq")
def customer_iq(customer: str) -> object:
    slug = slugify_customer(customer)
    output_path = output_file_for(slug)
    if not output_path.exists():
        save_customer_iq(slug)
    context = load_customer_context(slug)
    return jsonify(
        {
            "customer": slug,
            "iq": output_path.read_text(encoding="utf-8"),
            "context_sections": context["sections"],
            "name": context["name"],
        }
    )


@app.get("/api/customer/<customer>/msx-opportunities")
def customer_msx_opportunities(customer: str) -> object:
    return jsonify(load_msx_snapshot(slugify_customer(customer)))


@app.post("/api/customers")
def create_customer() -> object:
    payload = request.get_json(silent=True) or {}
    name = str(payload.get("customerName", "")).strip()
    if not name:
        return jsonify({"error": "customerName is required"}), 400

    customer = upsert_customer_profile(
        customer_name=name,
        tpid=str(payload.get("tpid", "")).strip(),
        msx_account_name=str(payload.get("msxAccountName", "")).strip(),
        msx_account_id=str(payload.get("msxAccountId", "")).strip(),
        msxi_key=str(payload.get("msxiKey", "")).strip(),
        msx_hyperlink=str(payload.get("msxHyperlink", "")).strip(),
        sharepoint_site=str(payload.get("sharepointSite", "")).strip(),
        managed_sites=str(payload.get("managedSites", "")).strip(),
        validation_status=str(payload.get("validationStatus", "")).strip(),
        aliases=str(payload.get("aliases", "")).strip(),
        notes=str(payload.get("notes", "")).strip(),
    )
    save_customer_iq(str(customer["slug"]))
    return jsonify({"customer": customer["slug"], "name": customer["name"]})


@app.post("/api/customer/<customer>/chat")
def customer_chat(customer: str) -> object:
    payload = request.get_json(silent=True) or {}
    question = str(payload.get("question", "")).strip()
    if not question:
        return jsonify({"error": "question is required"}), 400
    response = run_interaction(customer, question)
    return jsonify({"customer": slugify_customer(customer), "response": response})


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=False)
