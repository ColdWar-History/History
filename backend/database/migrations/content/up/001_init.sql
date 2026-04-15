CREATE SCHEMA IF NOT EXISTS content;

CREATE TABLE IF NOT EXISTS content.ciphers
(
    id uuid PRIMARY KEY,
    code text NOT NULL,
    name text NOT NULL,
    category text NOT NULL,
    era text NOT NULL,
    difficulty integer NOT NULL,
    summary text NOT NULL,
    description text NOT NULL,
    example text NOT NULL,
    publication_status text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_content_ciphers_code_ci
    ON content.ciphers ((lower(code)));

CREATE INDEX IF NOT EXISTS ix_content_ciphers_category_era
    ON content.ciphers (category, era);

CREATE INDEX IF NOT EXISTS ix_content_ciphers_difficulty
    ON content.ciphers (difficulty);

CREATE INDEX IF NOT EXISTS ix_content_ciphers_name_ci
    ON content.ciphers ((lower(name)));

CREATE INDEX IF NOT EXISTS ix_content_ciphers_search_tsv
    ON content.ciphers
    USING gin (to_tsvector('simple', coalesce(name, '') || ' ' || coalesce(code, '') || ' ' || coalesce(summary, '')));

CREATE TABLE IF NOT EXISTS content.historical_events
(
    id uuid PRIMARY KEY,
    title text NOT NULL,
    event_date date NOT NULL,
    region text NOT NULL,
    topic text NOT NULL,
    summary text NOT NULL,
    description text NOT NULL,
    publication_status text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_content_historical_events_date
    ON content.historical_events (event_date);

CREATE INDEX IF NOT EXISTS ix_content_historical_events_region
    ON content.historical_events (region);

CREATE INDEX IF NOT EXISTS ix_content_historical_events_topic
    ON content.historical_events (topic);

CREATE TABLE IF NOT EXISTS content.cipher_related_events
(
    cipher_id uuid NOT NULL,
    event_id uuid NOT NULL,
    PRIMARY KEY (cipher_id, event_id),
    CONSTRAINT fk_content_cipher_related_events_cipher
        FOREIGN KEY (cipher_id)
        REFERENCES content.ciphers (id)
        ON DELETE CASCADE,
    CONSTRAINT fk_content_cipher_related_events_event
        FOREIGN KEY (event_id)
        REFERENCES content.historical_events (id)
        ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_content_cipher_related_events_event_id
    ON content.cipher_related_events (event_id);

CREATE TABLE IF NOT EXISTS content.cipher_versions
(
    cipher_id uuid NOT NULL,
    version_number integer NOT NULL,
    edited_by text NOT NULL,
    updated_at timestamptz NOT NULL,
    change_summary text NOT NULL,
    PRIMARY KEY (cipher_id, version_number),
    CONSTRAINT fk_content_cipher_versions_cipher
        FOREIGN KEY (cipher_id)
        REFERENCES content.ciphers (id)
        ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS content.event_participants
(
    event_id uuid NOT NULL,
    participant text NOT NULL,
    PRIMARY KEY (event_id, participant),
    CONSTRAINT fk_content_event_participants_event
        FOREIGN KEY (event_id)
        REFERENCES content.historical_events (id)
        ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS content.event_cipher_codes
(
    event_id uuid NOT NULL,
    cipher_code text NOT NULL,
    PRIMARY KEY (event_id, cipher_code),
    CONSTRAINT fk_content_event_cipher_codes_event
        FOREIGN KEY (event_id)
        REFERENCES content.historical_events (id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_content_event_cipher_codes_cipher_code_ci
    ON content.event_cipher_codes ((lower(cipher_code)));

CREATE TABLE IF NOT EXISTS content.collections
(
    id uuid PRIMARY KEY,
    title text NOT NULL,
    theme text NOT NULL,
    summary text NOT NULL,
    publication_status text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_content_collections_theme
    ON content.collections (theme);

CREATE TABLE IF NOT EXISTS content.collection_events
(
    collection_id uuid NOT NULL,
    event_id uuid NOT NULL,
    PRIMARY KEY (collection_id, event_id),
    CONSTRAINT fk_content_collection_events_collection
        FOREIGN KEY (collection_id)
        REFERENCES content.collections (id)
        ON DELETE CASCADE,
    CONSTRAINT fk_content_collection_events_event
        FOREIGN KEY (event_id)
        REFERENCES content.historical_events (id)
        ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS content.collection_cipher_codes
(
    collection_id uuid NOT NULL,
    cipher_code text NOT NULL,
    PRIMARY KEY (collection_id, cipher_code),
    CONSTRAINT fk_content_collection_cipher_codes_collection
        FOREIGN KEY (collection_id)
        REFERENCES content.collections (id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_content_collection_cipher_codes_cipher_code_ci
    ON content.collection_cipher_codes ((lower(cipher_code)));
