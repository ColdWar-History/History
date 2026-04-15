CREATE TABLE IF NOT EXISTS progress.retention_policy
(
    id integer PRIMARY KEY,
    operations_retention_days integer NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT now()
);

INSERT INTO progress.retention_policy (id, operations_retention_days)
VALUES (1, 180)
ON CONFLICT (id) DO UPDATE
SET operations_retention_days = EXCLUDED.operations_retention_days,
    updated_at = now();
