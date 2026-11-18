#!/usr/bin/env bash
set -euo pipefail

# ponytail: auto-load .env from root directory if it exists to retrieve production passwords
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -f "$SCRIPT_DIR/../.env" ]; then
  export $(grep -v '^#' "$SCRIPT_DIR/../.env" | xargs)
fi

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-3306}"
DB_USER="${DB_USER:-root}"
DB_PASSWORD="${DB_PASSWORD:-password}" # Will use DB_PASSWORD exported from .env if present
DB_NAME="${DB_NAME:-servora}"
BACKUP_DIR="${BACKUP_DIR:-$SCRIPT_DIR/../backups}"
DB_CONTAINER="${DB_CONTAINER:-servora_mysql}"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_FILE="$BACKUP_DIR/$DB_NAME-$TIMESTAMP.sql"

mkdir -p "$BACKUP_DIR"

if command -v mysqldump >/dev/null 2>&1; then
  MYSQL_PWD="$DB_PASSWORD" mysqldump \
    --host="$DB_HOST" \
    --port="$DB_PORT" \
    --user="$DB_USER" \
    --single-transaction \
    --routines \
    --triggers \
    "$DB_NAME" > "$BACKUP_FILE"
elif [ -n "$DB_CONTAINER" ] && command -v docker >/dev/null 2>&1; then
  MYSQL_PWD="$DB_PASSWORD" docker exec -i \
    -e MYSQL_PWD \
    "$DB_CONTAINER" \
    mysqldump \
    --host=127.0.0.1 \
    --port=3306 \
    --user="$DB_USER" \
    --single-transaction \
    --routines \
    --triggers \
    "$DB_NAME" > "$BACKUP_FILE"
else
  echo "mysqldump not found. Install mysql-client or set DB_CONTAINER to a MySQL Docker container name." >&2
  exit 69
fi

echo "$BACKUP_FILE"
