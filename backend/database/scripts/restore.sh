#!/usr/bin/env sh

input="$1"

if [ -z "$input" ]; then
  echo "Usage: ./database/scripts/restore.sh <backup-file.dump>"
  exit 1
fi

host="${PGHOST:-localhost}"
port="${PGPORT:-5432}"
user="${PGUSER:-postgres}"
database="${PGDATABASE:-coldwarhistory}"

pg_restore -h "$host" -p "$port" -U "$user" -d "$database" --clean --if-exists "$input"
echo "Restore completed from $input"
