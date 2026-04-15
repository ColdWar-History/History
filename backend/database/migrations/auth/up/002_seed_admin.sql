INSERT INTO auth.users (id, user_name, email, password_hash)
VALUES ('2ecdc706-2dd5-4d49-ab0b-947b2d7d0d73', 'admin', 'admin@coldwar.local', '3EB3FE66B31E3B4D10FA70B5CAD49C7112294AF6AE4E476A1C405155D45AA121')
ON CONFLICT (id) DO UPDATE
SET user_name = EXCLUDED.user_name,
    email = EXCLUDED.email,
    password_hash = EXCLUDED.password_hash;

INSERT INTO auth.user_roles (user_id, role)
VALUES
    ('2ecdc706-2dd5-4d49-ab0b-947b2d7d0d73', 'admin'),
    ('2ecdc706-2dd5-4d49-ab0b-947b2d7d0d73', 'editor'),
    ('2ecdc706-2dd5-4d49-ab0b-947b2d7d0d73', 'user')
ON CONFLICT DO NOTHING;
