from __future__ import annotations

import sys
from pathlib import Path

from flask import Flask, jsonify, render_template, request


REPO_ROOT = Path(__file__).resolve().parents[1]
TOOLS_DIR = REPO_ROOT / "tools" / "customer-iq"
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from customer_iq_core import list_customers, output_file_for, save_customer_iq, slugify_customer  # noqa: E402
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


@app.get("/api/customer/<customer>/iq")
def customer_iq(customer: str) -> object:
    slug = slugify_customer(customer)
    output_path = output_file_for(slug)
    if not output_path.exists():
        save_customer_iq(slug)
    return jsonify({"customer": slug, "iq": output_path.read_text(encoding="utf-8")})


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
