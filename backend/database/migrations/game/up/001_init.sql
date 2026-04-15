CREATE SCHEMA IF NOT EXISTS game;

CREATE TABLE IF NOT EXISTS game.training_challenges
(
    id uuid PRIMARY KEY,
    cipher_code text NOT NULL,
    difficulty text NOT NULL,
    prompt text NOT NULL,
    input text NOT NULL,
    expected_mode text NOT NULL,
    parameters_json jsonb NOT NULL,
    expected_answer text NOT NULL,
    base_score integer NOT NULL,
    generated_at timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_game_training_challenges_generated_at
    ON game.training_challenges (generated_at DESC);

CREATE INDEX IF NOT EXISTS ix_game_training_challenges_cipher_difficulty
    ON game.training_challenges (cipher_code, difficulty);

CREATE TABLE IF NOT EXISTS game.shifts
(
    id uuid PRIMARY KEY,
    difficulty text NOT NULL,
    started_at timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_game_shifts_started_at
    ON game.shifts (started_at DESC);

CREATE TABLE IF NOT EXISTS game.shift_messages
(
    shift_id uuid NOT NULL,
    message_id uuid NOT NULL,
    headline text NOT NULL,
    encoded_message text NOT NULL,
    cipher_code text NOT NULL,
    briefing text NOT NULL,
    expected_decision text NOT NULL,
    PRIMARY KEY (shift_id, message_id),
    CONSTRAINT fk_game_shift_messages_shift
        FOREIGN KEY (shift_id)
        REFERENCES game.shifts (id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_game_shift_messages_cipher_code
    ON game.shift_messages (cipher_code);

CREATE TABLE IF NOT EXISTS game.shift_resolutions
(
    shift_id uuid NOT NULL,
    message_id uuid NOT NULL,
    decision text NOT NULL,
    is_correct boolean NOT NULL,
    score_delta integer NOT NULL,
    explanation text NOT NULL,
    PRIMARY KEY (shift_id, message_id),
    CONSTRAINT fk_game_shift_resolutions_message
        FOREIGN KEY (shift_id, message_id)
        REFERENCES game.shift_messages (shift_id, message_id)
        ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS game.daily_challenge
(
    challenge_date date PRIMARY KEY,
    challenge_id uuid NOT NULL,
    theme text NOT NULL,
    CONSTRAINT fk_game_daily_challenge_training
        FOREIGN KEY (challenge_id)
        REFERENCES game.training_challenges (id)
        ON DELETE RESTRICT
);
