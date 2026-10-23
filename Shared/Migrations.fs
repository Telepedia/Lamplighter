module Lamplighter.Shared.Migrations

open System.Data
open Donald

// We don't really need a full migration manager for this; so we just try and run all of the
// migrations at app start up
// @NOTE: as above, therefore every migration MUST be safe to re-run without corrupting data
let private migrations = [
    """
    CREATE TABLE IF NOT EXISTS edits (
        id          INTEGER PRIMARY KEY AUTOINCREMENT,
        wiki        TEXT NOT NULL,
        wiki_id     TEXT NOT NULL,
        page        TEXT NOT NULL,
        url         TEXT NOT NULL,
        editor      TEXT NOT NULL,
        summary     TEXT NOT NULL DEFAULT '',
        diff        TEXT NOT NULL,
        received    TEXT NOT NULL,
        config_version_id INTEGER NOT NULL REFERENCES config_versions (id),
        score       REAL CHECK (score BETWEEN 0 AND 1),
        scoring_status    TEXT NOT NULL DEFAULT 'pending' CHECK (scoring_status IN ('pending', 'scored', 'failed')),
        scoring_error   TEXT,
        scoring_attempts    INTEGER NOT NULL DEFAULT 0,
        review_status   TEXT NOT NULL DEFAULT 'unreviewed' CHECK (review_status IN ('unreviewed', 'approved', 'reverted')),
        reviewed_at     TEXT,
        reviewed_by     TEXT,
        claimed_at      TEXT,
        next_attempt_at TEXT;
    )
    """

    "CREATE INDEX IF NOT EXISTS idx_edits_scoring_status ON edits (scoring_status)"
    "CREATE INDEX IF NOT EXISTS idx_edits_review_queue ON edits (review_status, score)"
    
    """
    CREATE TABLE IF NOT EXISTS config_versions (
        id              INTEGER PRIMARY KEY AUTOINCREMENT,
        model           TEXT NOT NULL,
        prompt_template TEXT,
        temperature     REAL,
        note            TEXT NOT NULL DEFAULT '',
        is_active       INTEGER NOT NULL DEFAULT 0 CHECK (is_active IN (0, 1)),
        created_at      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now')),
        created_by      TEXT
    );
    """
    
    "CREATE UNIQUE INDEX IF NOT EXISTS idx_config_versions_one_active ON config_versions (is_active) WHERE is_active = 1;"
    
    """
    CREATE TABLE IF NOT EXISTS config_questions (
        id          INTEGER PRIMARY KEY AUTOINCREMENT,
        version_id  INTEGER NOT NULL REFERENCES config_versions (id),
        position    INTEGER NOT NULL,
        text        TEXT NOT NULL,
        UNIQUE (version_id, position)
    );
    """
    
    """
    CREATE TABLE IF NOT EXISTS edit_answers (
        edit_id     INTEGER NOT NULL REFERENCES edits (id),
        question_id INTEGER NOT NULL REFERENCES config_questions (id),
        probability REAL NOT NULL CHECK (probability BETWEEN 0 AND 1),
        PRIMARY KEY (edit_id, question_id)
    );
    """
    
    """
    CREATE TABLE IF NOT EXISTS settings (
        key   TEXT PRIMARY KEY,
        value TEXT NOT NULL
    );
    """
    
    """
    CREATE TRIGGER IF NOT EXISTS config_versions_immutable
    BEFORE UPDATE OF model, prompt_template, temperature, note, created_at, created_by
    ON config_versions
    BEGIN
        SELECT RAISE(ABORT, 'you cannot edit previous configuration; create a new config row instead');
    END;
    """
    
    """
    CREATE TRIGGER IF NOT EXISTS config_questions_immutable
    BEFORE UPDATE ON config_questions
    BEGIN
        SELECT RAISE(ABORT, 'you cannot edit previous questions. create new ones instead');
    END;
    """
]

// do the migrations, in a transaction so any errors rollback
let run (createConnection: unit -> IDbConnection) =
    use conn = createConnection ()
    conn
    |> Db.batch (fun tran ->
        for sql in migrations do
            Db.newCommandForTransaction sql tran |> Db.exec)
