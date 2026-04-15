CREATE SCHEMA IF NOT EXISTS auth;

CREATE TABLE IF NOT EXISTS auth.users
(
    id uuid PRIMARY KEY,
    user_name text NOT NULL,
    email text NOT NULL,
    password_hash text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_auth_users_user_name_ci
    ON auth.users ((lower(user_name)));

CREATE UNIQUE INDEX IF NOT EXISTS ux_auth_users_email_ci
    ON auth.users ((lower(email)));

CREATE TABLE IF NOT EXISTS auth.user_roles
(
    user_id uuid NOT NULL,
    role text NOT NULL,
    PRIMARY KEY (user_id, role),
    CONSTRAINT fk_auth_user_roles_user
        FOREIGN KEY (user_id)
        REFERENCES auth.users (id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_auth_user_roles_role
    ON auth.user_roles (role);

CREATE TABLE IF NOT EXISTS auth.refresh_sessions
(
    user_id uuid NOT NULL,
    token text NOT NULL,
    expires_at timestamptz NOT NULL,
    PRIMARY KEY (token),
    CONSTRAINT fk_auth_refresh_sessions_user
        FOREIGN KEY (user_id)
        REFERENCES auth.users (id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_auth_refresh_sessions_user_id
    ON auth.refresh_sessions (user_id);

CREATE INDEX IF NOT EXISTS ix_auth_refresh_sessions_expires_at
    ON auth.refresh_sessions (expires_at);

CREATE TABLE IF NOT EXISTS auth.access_sessions
(
    user_id uuid NOT NULL,
    token text NOT NULL,
    expires_at timestamptz NOT NULL,
    PRIMARY KEY (token),
    CONSTRAINT fk_auth_access_sessions_user
        FOREIGN KEY (user_id)
        REFERENCES auth.users (id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_auth_access_sessions_user_id
    ON auth.access_sessions (user_id);

CREATE INDEX IF NOT EXISTS ix_auth_access_sessions_expires_at
    ON auth.access_sessions (expires_at);
