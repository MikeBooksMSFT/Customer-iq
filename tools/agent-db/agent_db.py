"""
Garrett's agent brain CLI. Single source of structured state for all skills.

Usage:
  python agent_db.py init
  python agent_db.py seed-accounts
  python agent_db.py query "SELECT ..." [args...]
  python agent_db.py json  "SELECT ..." [args...]
  python agent_db.py exec  "INSERT/UPDATE ..." [args...]
  python agent_db.py schema
  python agent_db.py path
"""

import csv
import json
import os
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DB = ROOT / "agent.db"
SCHEMA = Path(__file__).resolve().parent / "schema.sql"
ACCOUNTS_CSV = ROOT / "accounts.csv"


def conn():
    c = sqlite3.connect(str(DB))
    c.row_factory = sqlite3.Row
    c.execute("PRAGMA foreign_keys = ON;")
    return c


def cmd_init():
    sql = SCHEMA.read_text(encoding="utf-8")
    with conn() as c:
        c.executescript(sql)
    print(f"init ok: {DB}")


def cmd_seed_accounts():
    if not ACCOUNTS_CSV.exists():
        sys.exit(f"accounts csv not found: {ACCOUNTS_CSV}")
    rows = list(csv.DictReader(ACCOUNTS_CSV.open(encoding="utf-8-sig")))
    with conn() as c:
        for r in rows:
            c.execute(
                """
                INSERT INTO accounts (ms_sales_id, account_name, segment, atu, territory,
                                      accelerate, city, state, ssp_name, ssp_alias, updated_at)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, datetime('now'))
                ON CONFLICT(ms_sales_id) DO UPDATE SET
                    account_name=excluded.account_name,
                    segment=excluded.segment,
                    atu=excluded.atu,
                    territory=excluded.territory,
                    accelerate=excluded.accelerate,
                    city=excluded.city,
                    state=excluded.state,
                    ssp_name=excluded.ssp_name,
                    ssp_alias=excluded.ssp_alias,
                    updated_at=datetime('now')
                """,
                (
                    (r.get("ms_sales_id") or "").strip(),
                    (r.get("account_name") or "").strip(),
                    (r.get("segment") or "").strip(),
                    (r.get("atu") or "").strip(),
                    (r.get("territory") or "").strip(),
                    (r.get("accelerate") or "").strip(),
                    (r.get("city") or "").strip(),
                    (r.get("state") or "").strip(),
                    (r.get("ssp_name") or "").strip(),
                    (r.get("ssp_alias") or "").strip(),
                ),
            )
    print(f"seeded {len(rows)} accounts")


def _print_table(rows):
    if not rows:
        print("(0 rows)")
        return
    cols = list(rows[0].keys())
    widths = [max(len(c), max(len(str(r[c]) if r[c] is not None else "") for r in rows)) for c in cols]
    sep = " | "
    print(sep.join(c.ljust(w) for c, w in zip(cols, widths)))
    print(sep.join("-" * w for w in widths))
    for r in rows:
        print(sep.join((str(r[c]) if r[c] is not None else "").ljust(w) for c, w in zip(cols, widths)))
    print(f"({len(rows)} rows)")


def cmd_query(sql, args):
    with conn() as c:
        rows = c.execute(sql, args).fetchall()
    _print_table(rows)


def cmd_json(sql, args):
    with conn() as c:
        rows = [dict(r) for r in c.execute(sql, args).fetchall()]
    print(json.dumps(rows, indent=2, default=str))


def cmd_exec(sql, args):
    with conn() as c:
        cur = c.execute(sql, args)
        c.commit()
    print(f"ok: rowcount={cur.rowcount} lastrowid={cur.lastrowid}")


def cmd_schema():
    print(SCHEMA.read_text(encoding="utf-8"))


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    cmd = sys.argv[1]
    if cmd == "init":
        cmd_init()
    elif cmd == "seed-accounts":
        cmd_seed_accounts()
    elif cmd == "query":
        cmd_query(sys.argv[2], sys.argv[3:])
    elif cmd == "json":
        cmd_json(sys.argv[2], sys.argv[3:])
    elif cmd == "exec":
        cmd_exec(sys.argv[2], sys.argv[3:])
    elif cmd == "schema":
        cmd_schema()
    elif cmd == "path":
        print(DB)
    else:
        sys.exit(f"unknown command: {cmd}")


if __name__ == "__main__":
    main()
