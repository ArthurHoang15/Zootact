#!/usr/bin/env bash
set -eu
set -o pipefail
umask 077

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
ENV_FILE="${ENV_FILE:-.env.production}"
BACKUP_DIR="${BACKUP_DIR:-/opt/zootact/backups/postgres}"
RETENTION_DAYS="${RETENTION_DAYS:-7}"

mkdir -p "$BACKUP_DIR"

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
output_file="$BACKUP_DIR/zootact-postgres-$timestamp.sql.gz"

docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T postgres \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' \
  | gzip -9 > "$output_file"

find "$BACKUP_DIR" -type f -name 'zootact-postgres-*.sql.gz' -mtime +"$RETENTION_DAYS" -delete

echo "$output_file"
