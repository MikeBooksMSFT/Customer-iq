from __future__ import annotations

import argparse

from customer_iq_core import answer_customer_question, slugify_customer


def run_interaction(customer: str, question: str) -> str:
    return answer_customer_question(slugify_customer(customer), question)


def main() -> None:
    parser = argparse.ArgumentParser(description="Ask a question of a customer interaction agent.")
    parser.add_argument("--customer", required=True, help="Customer slug or name.")
    parser.add_argument("--question", required=True, help="Question to ask.")
    args = parser.parse_args()
    print(run_interaction(args.customer, args.question))


if __name__ == "__main__":
    main()
