CREATE SCHEMA IF NOT EXISTS progress;

CREATE TABLE IF NOT EXISTS progress.user_progress
(
    user_id uuid PRIMARY KEY,
    user_name text NOT NULL,
    total_score integer NOT NULL,
    challenges_completed integer NOT NULL,
    correct_challenges integer NOT NULL,
    shift_reports_completed integer NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_progress_user_progress_leaderboard
    ON progress.user_progress (total_score DESC, correct_challenges DESC, user_name ASC);

CREATE TABLE IF NOT EXISTS progress.crypto_operations
(
    user_id uuid NOT NULL,
    operation_id uuid NOT NULL,
    cipher_code text NOT NULL,
    mode text NOT NULL,
    input text NOT NULL,
    output text NOT NULL,
    processed_at timestamptz NOT NULL,
    PRIMARY KEY (user_id, operation_id),
    CONSTRAINT fk_progress_crypto_operations_user
        FOREIGN KEY (user_id)
        REFERENCES progress.user_progress (user_id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_progress_crypto_operations_processed_at
    ON progress.crypto_operations (processed_at DESC);

CREATE TABLE IF NOT EXISTS progress.achievements
(
    user_id uuid NOT NULL,
    code text NOT NULL,
    title text NOT NULL,
    description text NOT NULL,
    unlocked_at timestamptz NOT NULL,
    PRIMARY KEY (user_id, code),
    CONSTRAINT fk_progress_achievements_user
        FOREIGN KEY (user_id)
        REFERENCES progress.user_progress (user_id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_progress_achievements_unlocked_at
    ON progress.achievements (unlocked_at DESC);
