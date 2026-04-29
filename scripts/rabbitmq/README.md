# RabbitMQ Reset Script

This folder provides an idempotent reset script for RabbitMQ vhost and users.

File:
- `reset-vhost-and-users.sh`

## What it does

- Resets a target vhost (default: `/`)
- Recreates users and permissions:
  - `devuser:devuser`
  - `guest:guest`
  - `proxmox:proxmox`
- Optionally closes active connections first
- Runs a final cleanup pass to remove residual non-default queues/exchanges
- If custom exchanges remain (and broker CLI cannot delete them), performs one hard vhost recreation fallback
- Verifies if non-default topology remains
- Keeps users present and updates password/tags/permissions (does not delete users)

## Default target

By default, the script targets the remote mq-server:
- `MQ_HOST=192.168.0.230`
- `REMOTE_MODE=true`

## Usage

```bash
./reset-vhost-and-users.sh [options]
```

Options:
- `--mq-host <host>` RabbitMQ host/IP (default `192.168.0.230`)
- `--ssh-user <user>` SSH user for remote execution
- `--local` run locally instead of SSH
- `--stop-dotnet` stop `dotnet` processes on target host before reset
- `--non-interactive` require key-based SSH + passwordless sudo
- `--remote-self` force single-session remote execution (advanced)
- `--no-remote-self` disable single-session remote execution
- `--no-ssh-multiplex` disable SSH connection reuse (debug only)
- `-h`, `--help` show help

Environment variables:
- `MQ_HOST`
- `SSH_USER`
- `REMOTE_MODE=true|false`
- `STOP_DOTNET=true|false`
- `NON_INTERACTIVE=true|false`
- `REMOTE_RABBITMQCTL` (default `/usr/sbin/rabbitmqctl`)
- `RUN_REMOTE_SELF=true|false` (default `false`)
- `SSH_MULTIPLEX=true|false` (default `true`)
- `RABBITMQCTL_BIN` (local rabbitmqctl path override)
- `VHOST` (default `/`)
- `FORCE_CLOSE_CONNECTIONS=true|false` (default `true`)

## Common examples

Run against mq-server (recommended):

```bash
./reset-vhost-and-users.sh --mq-host 192.168.0.230 --ssh-user devuser
```

Run local RabbitMQ host:

```bash
./reset-vhost-and-users.sh --local
```

Reset and stop dotnet on target host first:

```bash
./reset-vhost-and-users.sh --mq-host 192.168.0.230 --ssh-user devuser --stop-dotnet
```

Non-interactive (CI/CD):

```bash
./reset-vhost-and-users.sh --mq-host 192.168.0.230 --ssh-user devuser --non-interactive
```

Force single-session remote mode:

```bash
./reset-vhost-and-users.sh --mq-host 192.168.0.230 --ssh-user devuser --remote-self
```

## Notes

- If topology reappears after reset, an application likely reconnected and auto-declared queues/exchanges.
- `--stop-dotnet` uses `pkill -f '[d]otnet'` on the target host; use with care.
- Default mode (`RUN_REMOTE_SELF=false`) is cleaner and easier to debug.
- SSH multiplexing is enabled by default to avoid repeated password prompts during one script run.
- SSH log noise is minimized (`LogLevel=ERROR`) for cleaner output.
