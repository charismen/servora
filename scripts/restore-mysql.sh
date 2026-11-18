#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 path/to/backup.sql" >&2
  exit 64
fi

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
DB_CONTAINER="${DB_CONTAINER:-servora_mysql}"
BACKUP_FILE="$1"

if [ ! -f "$BACKUP_FILE" ]; then
  echo "Backup file not found: $BACKUP_FILE" >&2
  exit 66
fi

if command -v mysql >/dev/null 2>&1; then
  MYSQL_PWD="$DB_PASSWORD" mysql \
    --host="$DB_HOST" \
    --port="$DB_PORT" \
    --user="$DB_USER" \
    "$DB_NAME" < "$BACKUP_FILE"
elif [ -n "$DB_CONTAINER" ] && command -v docker >/dev/null 2>&1; then
  MYSQL_PWD="$DB_PASSWORD" docker exec -i \
    -e MYSQL_PWD \
    "$DB_CONTAINER" \
    mysql \
    --host=127.0.0.1 \
    --port=3306 \
    --user="$DB_USER" \
    "$DB_NAME" < "$BACKUP_FILE"
else
  echo "mysql not found. Install mysql-client or set DB_CONTAINER to a MySQL Docker container name." >&2
  exit 69
fi

echo "Restored $BACKUP_FILE into $DB_NAME"
