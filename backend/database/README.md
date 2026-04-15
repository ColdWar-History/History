# Database Layer

Backend persistence is organized as schema-per-service in one PostgreSQL database:

- auth
- content
- game
- progress

## Migrations

Migrations are stored in service folders:

- database/migrations/auth
- database/migrations/content
- database/migrations/game
- database/migrations/progress

Each service has:

- up: forward migrations
- down: rollback migrations

Migration runner tracks applied versions in public.schema_migrations.

## MVP Seed Data

Seed migrations include:

- auth: admin user
- content: 15 historical events, 5 cipher cards, curated collections

## Operational Scripts

- database/scripts/backup.sh: create custom-format dump
- database/scripts/restore.sh: restore dump to database
- database/scripts/monitor.sql: DB growth and index usage checks
- database/scripts/prune_progress_history.sql: retention cleanup for progress operations

## ER diagrams

See database/ERD.md.
