-- Garrett's agent brain. Single-writer, local-only.
-- Path: $env:USERPROFILE\.copilot\m-skills\fsx-builder\tools\agent.db
-- Role scope: GitHub Copilot + GHAS milestones only. Azure/M365-chat/PBI are out.

PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;

-- =========================================================
-- Accounts: mirror of accounts.csv + computed priority
-- =========================================================
CREATE TABLE IF NOT EXISTS accounts (
    ms_sales_id       TEXT PRIMARY KEY,
    account_name      TEXT NOT NULL,
    segment           TEXT,
    atu               TEXT,
    territory         TEXT,
    accelerate        TEXT,
    city              TEXT,
    state             TEXT,
    ssp_name          TEXT,
    ssp_alias         TEXT,
    last_ghcp_touch   TEXT,
    last_ghas_touch   TEXT,
    priority_score    REAL,
    priority_reason   TEXT,
    updated_at        TEXT DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_accounts_priority ON accounts(priority_score DESC);

CREATE TABLE IF NOT EXISTS contacts (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    ms_sales_id     TEXT REFERENCES accounts(ms_sales_id),
    first_name      TEXT,
    last_name       TEXT,
    email           TEXT,
    title           TEXT,
    ghcp_role       TEXT,
    source          TEXT,
    linkedin_url    TEXT,
    notes           TEXT,
    first_seen      TEXT DEFAULT (datetime('now')),
    last_touch_at   TEXT,
    UNIQUE(ms_sales_id, email)
);
CREATE INDEX IF NOT EXISTS idx_contacts_account ON contacts(ms_sales_id);
CREATE INDEX IF NOT EXISTS idx_contacts_email ON contacts(lower(email));

CREATE TABLE IF NOT EXISTS targets (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    ms_sales_id     TEXT NOT NULL REFERENCES accounts(ms_sales_id),
    contact_id      INTEGER REFERENCES contacts(id),
    role_wanted     TEXT,
    goal            TEXT,
    milestone_id    TEXT,
    family          TEXT,
    priority_score  REAL,
    status          TEXT DEFAULT 'queued',
    next_nudge_at   TEXT,
    created_at      TEXT DEFAULT (datetime('now')),
    updated_at      TEXT DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_targets_status ON targets(status, next_nudge_at);
CREATE INDEX IF NOT EXISTS idx_targets_account ON targets(ms_sales_id);

CREATE TABLE IF NOT EXISTS outreach (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    contact_id      INTEGER REFERENCES contacts(id),
    target_id       INTEGER REFERENCES targets(id),
    attempted_at    TEXT DEFAULT (datetime('now')),
    channel         TEXT,
    ref_id          TEXT,
    subject         TEXT,
    outcome         TEXT,
    notes           TEXT
);
CREATE INDEX IF NOT EXISTS idx_outreach_contact ON outreach(contact_id, attempted_at DESC);

CREATE TABLE IF NOT EXISTS followups (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    email_thread_id     TEXT,
    email_message_id    TEXT,
    subject             TEXT,
    with_email          TEXT,
    ms_sales_id         TEXT REFERENCES accounts(ms_sales_id),
    due_date            TEXT,
    reason              TEXT,
    status              TEXT DEFAULT 'open',
    created_at          TEXT DEFAULT (datetime('now')),
    updated_at          TEXT DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_followups_status ON followups(status, due_date);

CREATE TABLE IF NOT EXISTS milestone_coverage (
    milestone_id        TEXT PRIMARY KEY,
    ms_sales_id         TEXT REFERENCES accounts(ms_sales_id),
    milestone_name      TEXT,
    family              TEXT,
    opportunity_id      TEXT,
    champion_contact_id INTEGER REFERENCES contacts(id),
    last_activity_date  TEXT,
    has_gap             INTEGER DEFAULT 0,
    gap_reason          TEXT,
    computed_at         TEXT DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_mc_account ON milestone_coverage(ms_sales_id);
CREATE INDEX IF NOT EXISTS idx_mc_gap ON milestone_coverage(has_gap, family);

CREATE TABLE IF NOT EXISTS meetings (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    event_id        TEXT UNIQUE,
    subject         TEXT,
    start_at        TEXT,
    end_at          TEXT,
    ms_sales_id     TEXT REFERENCES accounts(ms_sales_id),
    milestone_id    TEXT,
    attendees       TEXT,
    status          TEXT DEFAULT 'pending',
    msx_activity_id TEXT,
    recap_drafted   INTEGER DEFAULT 0,
    notes           TEXT,
    created_at      TEXT DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_meetings_status ON meetings(status, start_at);

CREATE TABLE IF NOT EXISTS run_log (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    skill       TEXT,
    started_at  TEXT DEFAULT (datetime('now')),
    ended_at    TEXT,
    summary     TEXT,
    rows_written INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS settings (
    key     TEXT PRIMARY KEY,
    value   TEXT,
    updated_at TEXT DEFAULT (datetime('now'))
);
