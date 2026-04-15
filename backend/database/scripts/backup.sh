#!/usr/bin/env sh

host="${PGHOST:-localhost}"
port="${PGPORT:-5432}"
user="${PGUSER:-postgres}"
database="${PGDATABASE:-coldwarhistory}"
output="${1:-./runtime/backups/coldwarhistory_$(date +%Y%m%d_%H%M%S).dump}"

mkdir -p "$(dirname "$output")"
pg_dump -h "$host" -p "$port" -U "$user" -d "$database" -Fc -f "$output"
echo "Backup saved to $output"
