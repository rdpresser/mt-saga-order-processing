# k6 Performance Tests

Minimal performance package for Saga order creation flow.

## Scripts

- `smoke.js`: fast health check for `POST /orders` and `GET /orders/{id}`
- `load.js`: sustained concurrent create-order load with sampled eventual `GET /orders/{id}` verification
- `run.ps1`: PowerShell runner for Linux/Windows (PowerShell 7+)

## Prerequisites

- Order Service running (default: `http://localhost:5214`)
- k6 installed and available in PATH
- PowerShell 7+

## Configuration (.env)

The `run.ps1` script reads `k6/.env` automatically.

Default file:

```dotenv
BASE_URL=http://localhost:5214
```

Precedence for `BASE_URL`:

1. `-BaseUrl` parameter
2. `k6/.env`
3. existing environment variable `BASE_URL`
4. fallback default `http://localhost:5214`

## Install k6 (Linux)

Ubuntu/Debian (official repository):

```bash
sudo gpg -k
sudo mkdir -p /etc/apt/keyrings
curl -fsSL https://dl.k6.io/key.gpg | sudo gpg --dearmor -o /etc/apt/keyrings/k6-archive-keyring.gpg
echo "deb [signed-by=/etc/apt/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt update
sudo apt install -y k6
```

Alternative (Snap):

```bash
sudo snap install k6
```

Validate installation:

```bash
k6 version
command -v k6
```

## Run

From repository root:

```powershell
pwsh ./k6/run.ps1 -Scenario smoke
pwsh ./k6/run.ps1 -Scenario load
```

From inside `k6/` folder:

```powershell
pwsh ./run.ps1 -Scenario smoke
pwsh ./run.ps1 -Scenario load
```

Custom base URL:

```powershell
pwsh ./k6/run.ps1 -Scenario smoke -BaseUrl http://localhost:5001
```

## Troubleshooting

If you get `k6 is not installed or not available in PATH`:

1. Install k6 using one option above.
2. Open a new terminal session.
3. Validate with:

```bash
command -v k6
k6 version
```

If needed, add k6 manually to PATH (example):

```bash
export PATH="$PATH:/usr/local/bin"
```
