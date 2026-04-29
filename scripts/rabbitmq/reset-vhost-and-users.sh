#!/usr/bin/env bash
set -euo pipefail

VHOST="${VHOST:-/}"

# Users to ensure (user:password)
USERS=(
  "devuser:devuser"
  "guest:guest"
  "proxmox:proxmox"
)

info() {
  printf '[info] %s\n' "$*"
}

warn() {
  printf '[warn] %s\n' "$*" >&2
}

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    printf '[error] Command not found: %s\n' "$1" >&2
    exit 1
  fi
}

vhost_exists() {
  rabbitmqctl list_vhosts --silent | awk '{print $1}' | grep -Fxq "$VHOST"
}

user_exists() {
  local user="$1"
  rabbitmqctl list_users --silent | awk '{print $1}' | grep -Fxq "$user"
}

ensure_user() {
  local user="$1"
  local password="$2"

  if user_exists "$user"; then
    info "User '$user' exists. Updating password and tags."
    rabbitmqctl change_password "$user" "$password"
  else
    info "Creating user '$user'."
    rabbitmqctl add_user "$user" "$password"
  fi

  rabbitmqctl set_user_tags "$user" administrator
}

set_user_permissions() {
  local user="$1"
  info "Setting permissions for user '$user' on vhost '$VHOST'."
  rabbitmqctl set_permissions -p "$VHOST" "$user" ".*" ".*" ".*"
}

main() {
  require_cmd rabbitmqctl

  info "Resetting RabbitMQ vhost '$VHOST' in an idempotent way."

  if vhost_exists; then
    info "Deleting existing vhost '$VHOST'."
    rabbitmqctl delete_vhost "$VHOST"
  else
    info "Vhost '$VHOST' does not exist. Skipping delete."
  fi

  info "Creating vhost '$VHOST'."
  rabbitmqctl add_vhost "$VHOST"

  for entry in "${USERS[@]}"; do
    user="${entry%%:*}"
    password="${entry#*:}"

    ensure_user "$user" "$password"
    set_user_permissions "$user"
  done

  info "Done. Vhost '$VHOST' recreated and permissions applied for: devuser, guest, proxmox."
}

main "$@"
