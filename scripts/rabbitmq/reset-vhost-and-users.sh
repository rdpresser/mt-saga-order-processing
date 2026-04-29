#!/usr/bin/env bash
set -euo pipefail

MQ_HOST="${MQ_HOST:-192.168.0.230}"
SSH_USER="${SSH_USER:-}"
REMOTE_MODE="${REMOTE_MODE:-true}"
NON_INTERACTIVE="${NON_INTERACTIVE:-false}"
REMOTE_RABBITMQCTL="${REMOTE_RABBITMQCTL:-/usr/sbin/rabbitmqctl}"
RUN_REMOTE_SELF="${RUN_REMOTE_SELF:-false}"
STOP_DOTNET="${STOP_DOTNET:-false}"
SSH_MULTIPLEX="${SSH_MULTIPLEX:-true}"
SSH_SOCKET=""

RABBITMQCTL_BIN="${RABBITMQCTL_BIN:-rabbitmqctl}"

VHOST="${VHOST:-/}"
FORCE_CLOSE_CONNECTIONS="${FORCE_CLOSE_CONNECTIONS:-true}"

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

usage() {
  cat <<'EOF'
Usage: ./reset-vhost-and-users.sh [options]

Options:
  --mq-host <host>   RabbitMQ server host/IP (default: 192.168.0.230)
  --ssh-user <user>  SSH user for remote execution (default: current SSH config)
  --local            Execute rabbitmqctl locally instead of via SSH
  --stop-dotnet      Stop running dotnet processes on the target host before reset
  --non-interactive  Require key-based SSH + passwordless sudo
  --remote-self      Force single-session remote execution (advanced)
  --no-remote-self   Disable single-session remote execution
  --no-ssh-multiplex Disable SSH connection reuse (debug only)
  -h, --help         Show this help

Environment variables:
  MQ_HOST                  Same as --mq-host
  SSH_USER                 Same as --ssh-user
  REMOTE_MODE=true|false   Default: true
  NON_INTERACTIVE=true|false  Default: false
  REMOTE_RABBITMQCTL       Default: /usr/sbin/rabbitmqctl
  RUN_REMOTE_SELF=true|false Default: false
  STOP_DOTNET=true|false    Default: false
  SSH_MULTIPLEX=true|false   Default: true
  RABBITMQCTL_BIN          Local rabbitmqctl path override
  VHOST                    Default: /
  FORCE_CLOSE_CONNECTIONS  Default: true
EOF
}

is_true() {
  case "${1,,}" in
    true|1|yes|y|on) return 0 ;;
    *) return 1 ;;
  esac
}

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    printf '[error] Command not found: %s\n' "$1" >&2
    exit 1
  fi
}

resolve_local_rabbitmqctl() {
  if command -v rabbitmqctl >/dev/null 2>&1; then
    RABBITMQCTL_BIN="$(command -v rabbitmqctl)"
    return 0
  fi

  if [[ -x "$REMOTE_RABBITMQCTL" ]]; then
    RABBITMQCTL_BIN="$REMOTE_RABBITMQCTL"
    return 0
  fi

  printf '[error] Could not find rabbitmqctl in PATH or at %s\n' "$REMOTE_RABBITMQCTL" >&2
  exit 1
}

rabbitmq_target() {
  if [[ -n "$SSH_USER" ]]; then
    printf '%s@%s\n' "$SSH_USER" "$MQ_HOST"
  else
    printf '%s\n' "$MQ_HOST"
  fi
}

ssh_exec() {
  local -a ssh_cmd
  ssh_cmd=(ssh)

  if [[ -n "$SSH_SOCKET" ]]; then
    ssh_cmd+=(-o ControlPath="$SSH_SOCKET")
  fi

  if [[ "${EUID:-$(id -u)}" -eq 0 && -n "${SUDO_USER:-}" && "${SUDO_USER}" != "root" ]]; then
    sudo -u "$SUDO_USER" "${ssh_cmd[@]}" "$@"
  else
    "${ssh_cmd[@]}" "$@"
  fi
}

start_ssh_master() {
  if ! is_true "$REMOTE_MODE" || ! is_true "$SSH_MULTIPLEX"; then
    return 0
  fi

  local target
  target="$(rabbitmq_target)"
  SSH_SOCKET="/tmp/rmq-reset-${USER}-$$.sock"

  # Open one authenticated master connection so subsequent commands reuse it.
  ssh_exec -o ControlMaster=yes -o ControlPersist=120 -o ConnectTimeout=10 -o ControlPath="$SSH_SOCKET" -Nf "$target"
}

stop_ssh_master() {
  if [[ -z "$SSH_SOCKET" ]]; then
    return 0
  fi

  local target
  target="$(rabbitmq_target)"
  ssh_exec -o ControlPath="$SSH_SOCKET" -O exit "$target" >/dev/null 2>&1 || true
  rm -f "$SSH_SOCKET" >/dev/null 2>&1 || true
  SSH_SOCKET=""
}

rabbitmqctl_cmd() {
  if is_true "$REMOTE_MODE"; then
    local target
    local remote_cmd
    local -a ssh_opts
    target="$(rabbitmq_target)"
    remote_cmd="sudo $REMOTE_RABBITMQCTL"
    ssh_opts=(-o ConnectTimeout=10 -o LogLevel=ERROR)

    if is_true "$NON_INTERACTIVE"; then
      ssh_opts+=(-o BatchMode=yes)
    else
      # TTY allows password-based sudo when needed.
      ssh_opts+=(-tt)
    fi

    for arg in "$@"; do
      remote_cmd+=" $(printf '%q' "$arg")"
    done

    ssh_exec "${ssh_opts[@]}" "$target" "$remote_cmd"
  else
    if [[ "${EUID:-$(id -u)}" -eq 0 ]]; then
      "$RABBITMQCTL_BIN" "$@"
    else
      sudo "$RABBITMQCTL_BIN" "$@"
    fi
  fi
}

run_remote_self() {
  local target
  local -a ssh_opts
  target="$(rabbitmq_target)"
  ssh_opts=(-o ConnectTimeout=10)

  if is_true "$NON_INTERACTIVE"; then
    ssh_opts+=(-o BatchMode=yes)
  else
    ssh_opts+=(-tt)
  fi

  info "Executing reset directly on remote shell: $target"

  ssh_exec "${ssh_opts[@]}" "$target" \
    "sudo env REMOTE_MODE=false NON_INTERACTIVE=$NON_INTERACTIVE VHOST=$(printf '%q' "$VHOST") FORCE_CLOSE_CONNECTIONS=$FORCE_CLOSE_CONNECTIONS REMOTE_RABBITMQCTL=$(printf '%q' "$REMOTE_RABBITMQCTL") RABBITMQCTL_BIN=$(printf '%q' "$REMOTE_RABBITMQCTL") RUN_REMOTE_SELF=false bash -s -- --local --no-remote-self" \
    < "$0"
}

stop_dotnet_processes() {
  if ! is_true "$STOP_DOTNET"; then
    return 0
  fi

  if is_true "$REMOTE_MODE"; then
    local target
    target="$(rabbitmq_target)"
    info "Stopping dotnet processes on remote host: $target"
    ssh_exec -o ConnectTimeout=10 -o LogLevel=ERROR "$target" "sudo pkill -f '[d]otnet' || true"
    return 0
  fi

  info "Stopping dotnet processes on local host."
  if [[ "${EUID:-$(id -u)}" -eq 0 ]]; then
    pkill -f '[d]otnet' || true
  else
    sudo pkill -f '[d]otnet' || true
  fi
}

validate_runtime_requirements() {
  if is_true "$REMOTE_MODE"; then
    require_cmd ssh
    local target
    target="$(rabbitmq_target)"
    info "Using remote RabbitMQ server: $target"

    if [[ "${EUID:-$(id -u)}" -eq 0 && -n "${SUDO_USER:-}" && "${SUDO_USER}" != "root" ]]; then
      warn "Script invoked with sudo locally; SSH will run as user '$SUDO_USER' to reuse known_hosts and SSH credentials."
    fi

    if is_true "$NON_INTERACTIVE"; then
      if ! ssh_exec -o BatchMode=yes -o ConnectTimeout=10 -o LogLevel=ERROR "$target" "sudo -n $REMOTE_RABBITMQCTL status >/dev/null"; then
        printf '[error] Could not execute non-interactive "sudo %s" on %s.\n' "$REMOTE_RABBITMQCTL" "$target" >&2
        printf '[error] Ensure key-based SSH and passwordless sudo, or set NON_INTERACTIVE=false.\n' >&2
        exit 1
      fi
    else
      if ! ssh_exec -o ConnectTimeout=10 -o LogLevel=ERROR "$target" "test -x $REMOTE_RABBITMQCTL"; then
        printf '[error] Could not find executable %s on %s.\n' "$REMOTE_RABBITMQCTL" "$target" >&2
        printf '[error] Verify RabbitMQ installation or set REMOTE_RABBITMQCTL accordingly.\n' >&2
        exit 1
      fi
      info "Interactive remote mode enabled; SSH/sudo may prompt for password."
    fi
  else
    info "Using local RabbitMQ server."
    resolve_local_rabbitmqctl
  fi
}

cleanup_remaining_topology() {
  local queue_name
  local attempts
  attempts=0

  # Defensive pass with retries: remove residual topology that may reappear during reset window.
  while [[ "$attempts" -lt 3 ]]; do
    attempts=$((attempts + 1))

    while IFS= read -r queue_name; do
      queue_name="$(printf '%s' "$queue_name" | sed 's/\r$//')"
      [[ -z "$queue_name" ]] && continue

      warn "Removing residual queue '$queue_name' (attempt $attempts/3)."
      if ! rabbitmqctl_cmd delete_queue "$queue_name" >/dev/null 2>&1; then
        warn "Failed to remove queue '$queue_name'."
      fi
    done < <(rabbitmqctl_cmd list_queues -p "$VHOST" --silent name | awk 'NF')

    # Some RabbitMQ versions do not support rabbitmqctl delete_exchange.
    # We only detect residual exchanges here; fallback cleanup happens via vhost recreation.
    while IFS= read -r queue_name; do
      queue_name="$(printf '%s' "$queue_name" | sed 's/\r$//')"
      [[ -z "$queue_name" ]] && continue
      warn "Residual exchange '$queue_name' detected (attempt $attempts/3)."
    done < <(list_custom_exchanges)

    # Break early when cleanup converges.
    if [[ "$(rabbitmqctl_cmd list_queues -p "$VHOST" --silent name | awk 'NF' | wc -l | tr -d ' ')" -eq 0 ]] \
      && [[ -z "$(list_custom_exchanges)" ]]; then
      break
    fi
  done
}

list_custom_exchanges() {
  rabbitmqctl_cmd list_exchanges -p "$VHOST" --silent name \
    | awk 'NF' \
    | sed 's/\r$//' \
    | grep -Ev '^(amq\.direct|amq\.fanout|amq\.headers|amq\.match|amq\.rabbitmq\.log|amq\.rabbitmq\.trace|amq\.topic)$' \
    || true
}

reset_vhost() {
  info "Deleting vhost '$VHOST' (if it exists)."
  if ! rabbitmqctl_cmd delete_vhost "$VHOST" >/dev/null 2>&1; then
    info "Vhost '$VHOST' was not present or could not be deleted cleanly. Continuing."
  fi

  info "Creating vhost '$VHOST'."
  rabbitmqctl_cmd add_vhost "$VHOST"
}

parse_args() {
  while (($#)); do
    case "$1" in
      --mq-host)
        MQ_HOST="$2"
        shift 2
        ;;
      --ssh-user)
        SSH_USER="$2"
        shift 2
        ;;
      --local)
        REMOTE_MODE=false
        shift
        ;;
      --stop-dotnet)
        STOP_DOTNET=true
        shift
        ;;
      --non-interactive)
        NON_INTERACTIVE=true
        shift
        ;;
      --remote-self)
        RUN_REMOTE_SELF=true
        shift
        ;;
      --no-remote-self)
        RUN_REMOTE_SELF=false
        shift
        ;;
      --no-ssh-multiplex)
        SSH_MULTIPLEX=false
        shift
        ;;
      -h|--help)
        usage
        exit 0
        ;;
      *)
        printf '[error] Unknown argument: %s\n' "$1" >&2
        usage
        exit 1
        ;;
    esac
  done
}

vhost_exists() {
  rabbitmqctl_cmd list_vhosts --silent | awk '{print $1}' | grep -Fxq "$VHOST"
}

user_exists() {
  local user="$1"
  rabbitmqctl_cmd list_users --silent | awk '{print $1}' | grep -Fxq "$user"
}

ensure_user() {
  local user="$1"
  local password="$2"

  if user_exists "$user"; then
    info "User '$user' exists. Updating password and tags."
    rabbitmqctl_cmd change_password "$user" "$password"
  else
    info "Creating user '$user'."
    rabbitmqctl_cmd add_user "$user" "$password"
  fi

  rabbitmqctl_cmd set_user_tags "$user" administrator
}

set_user_permissions() {
  local user="$1"
  info "Setting permissions for user '$user' on vhost '$VHOST'."
  rabbitmqctl_cmd set_permissions -p "$VHOST" "$user" ".*" ".*" ".*"
}

count_connections_on_vhost() {
  rabbitmqctl_cmd list_connections --silent vhost 2>/dev/null \
    | awk -v vhost="$VHOST" '$1 == vhost { count++ } END { print count + 0 }'
}

close_connections_on_vhost() {
  local count
  count="$(count_connections_on_vhost)"

  if [[ "$count" -gt 0 ]]; then
    warn "Found $count active connection(s) on vhost '$VHOST'."

    if is_true "$FORCE_CLOSE_CONNECTIONS"; then
      info "Closing active connections on vhost '$VHOST' to avoid immediate topology recreation."
      rabbitmqctl_cmd close_all_connections --vhost "$VHOST" "Resetting vhost '$VHOST'"
    else
      warn "FORCE_CLOSE_CONNECTIONS=false; active clients may recreate exchanges/queues right after reset."
    fi
  fi
}

verify_clean_vhost() {
  local queue_count
  local custom_exchanges
  local user_count

  queue_count="$(rabbitmqctl_cmd list_queues -p "$VHOST" --silent name | awk 'NF' | wc -l | tr -d ' ')"

  custom_exchanges="$(list_custom_exchanges)"

  if [[ "$queue_count" -gt 0 || -n "$custom_exchanges" ]]; then
    warn "Vhost '$VHOST' still has non-default topology after reset."
    warn "Queues remaining: $queue_count"
    if [[ -n "$custom_exchanges" ]]; then
      warn "Custom exchanges remaining:"
      printf '%s\n' "$custom_exchanges" >&2
    fi
    warn "Likely cause: connected applications auto-declared topology immediately after reset."
    warn "Active connections on vhost '$VHOST' now:"
    (rabbitmqctl_cmd list_connections --silent user vhost peer_host peer_port state | awk -v vhost="$VHOST" '$2 == vhost') || true
  else
    info "Verification passed: no queues and no custom exchanges remain on vhost '$VHOST'."
  fi

  user_count="$(rabbitmqctl_cmd list_users --silent | awk 'NF' | wc -l | tr -d ' ')"
  info "Users currently present on broker: $user_count"
}

main() {
  parse_args "$@"
  trap stop_ssh_master EXIT

  if is_true "$REMOTE_MODE" && is_true "$RUN_REMOTE_SELF"; then
    validate_runtime_requirements
    run_remote_self
    exit $?
  fi

  validate_runtime_requirements
  start_ssh_master

  if ! is_true "$REMOTE_MODE"; then
    resolve_local_rabbitmqctl
  fi

  stop_dotnet_processes

  info "Resetting RabbitMQ vhost '$VHOST' in an idempotent way."
  close_connections_on_vhost
  reset_vhost

  for entry in "${USERS[@]}"; do
    user="${entry%%:*}"
    password="${entry#*:}"

    ensure_user "$user" "$password"
    set_user_permissions "$user"
  done

  cleanup_remaining_topology

  if [[ -n "$(list_custom_exchanges)" ]]; then
    warn "Custom exchanges still present. Performing one hard vhost recreation fallback."
    reset_vhost
    for entry in "${USERS[@]}"; do
      user="${entry%%:*}"
      set_user_permissions "$user"
    done
  fi

  verify_clean_vhost
  info "Done. Vhost '$VHOST' recreated and permissions applied for: devuser, guest, proxmox."
}

main "$@"
