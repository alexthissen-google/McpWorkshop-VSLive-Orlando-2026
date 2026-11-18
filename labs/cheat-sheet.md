# Cheat sheet — the commands you keep needing

Everything on this page is explained in the labs; this is the version for after a coffee,
when the Codespace has restarted and you just want things back the way they were. All
commands run in a Codespace terminal from the repo root unless the comment says otherwise.

## Terminals

The labs keep several things running side by side, each in its own terminal.

- **Open a terminal:** `Cmd+Shift+C` (`Ctrl+Shift+C` on Windows and Linux), or the Command
  Palette (`Cmd+Shift+P` / `Ctrl+Shift+P`) → type `new terminal` → **Terminal: Create New
  Terminal**.
- **Add another terminal:** the same again, or the **+** at the top right of the terminal
  panel. The terminals are listed at the right-hand side of the panel; click one to switch
  to it.
- **Name a terminal:** right-click it in that list → **Rename…** (or select it and press
  `F2`), type a name, `Enter`. With only one terminal open there is no list yet: use the
  Command Palette → **Terminal: Rename**.

Names that match the labs: `aspire` (where `aspire run` stays running), `shell` (for
`curl`, `deploy.sh` and `prepare-lab.sh`), `inspector` (MCP Inspector) and `devproxy`
(Lab 4). Named terminals make it obvious where to press `Ctrl+C` when a lab says to stop
something.

## After every break

A Codespace stops after 30 minutes idle. When it comes back, nothing you started is
running, and a public port may be private again.

```bash
# 1. Ports the outside world must reach: the agent (5197), IdentityServer (5001, Lab 5+),
#    the Aspire dashboard (15095). The Codespace does this on every start; run it when
#    the Foundry agent reports 401 or "cannot connect" after a break.
gh codespace ports visibility 5197:public 5001:public 15095:public -c "$CODESPACE_NAME"

# 2. Start Aspire (Postgres, pgAdmin, IdentityServer, the Host). Or: Aspire view -> Run AppHost.
aspire run

# 3. In a second terminal: wait until the MCP server is completely up. Returns as soon as
#    the Host is healthy, or gives up after two minutes.
aspire wait travelbooking-host

# 4. Prove the server is up, from inside and from outside
curl -s http://localhost:5197/health
curl -s "https://${CODESPACE_NAME}-5197.app.github.dev/health"
#    Inside OK but outside an empty 404, with the port listed as forwarded and public?
#    The forwarding tunnel went stale with the restart: in VS Code run
#    "Developer: Reload Window", then repeat step 1.

# 5. MCP Inspector, in its own terminal. Open the URL it prints (it carries a fresh token
#    for Inspector's own backend; an old tab keeps the old one and fails with
#    "Remote fetch failed (401)").
npx @modelcontextprotocol/inspector
```

Inspector settings: Transport **Streamable HTTP**, URL `http://localhost:5197/mcp`.

## The Foundry agent

```bash
# Lost the playground link? The script is idempotent and reprints it.
./infra/attendee/deploy.sh --mcp-url "https://${CODESPACE_NAME}-5197.app.github.dev/mcp"

# Lab 2 onward: no approval prompt per tool call (the policy sticks on later re-runs)
./infra/attendee/deploy.sh --mcp-url "https://${CODESPACE_NAME}-5197.app.github.dev/mcp" --approval never

# Lab 6: point it at Azure Container Apps instead of the Codespace, with a token from the
#   deployed identity provider's /Token page (the Codespace one is a different issuer)
./infra/attendee/deploy.sh --mcp-url https://travelbooking-host.<random>.<region>.azurecontainerapps.io/mcp --mcp-token "$TOKEN"

# Signed out of Azure? (the script tells you)
az login --use-device-code
```

Foundry caches your server's tool list with the agent version: a renamed tool, a changed
description or a new parameter is invisible to the agent until `deploy.sh` cuts a new
version. The Inspector always shows the live list.

Ask the agent with a **year** in the date, e.g. "a flight from Amsterdam to London on
November 21st, 2026"; without it the model guesses the year and finds no flights.

## Test data

```bash
# A flight id for curl or Inspector calls (Lab 0's search; adjust route/date to taste)
FLIGHT_ID=$(curl -s "http://localhost:5197/flights?from=AMS&to=LHR&date=2026-11-21" | jq -r '.[0].id')

# Wipe bookings, holds and audit entries; flights and airports stay
curl -s -X POST http://localhost:5197/admin/reset-data
```

From Lab 5 on (with `"UseIdentityServer": true`) both calls answer `401` without a token,
and `FLIGHT_ID` silently stays empty. Add `-H "Authorization: Bearer $TOKEN"`, with a token
from <http://localhost:5001/Token>; the reset needs one that carries `booking:write`.

## Lab prep and lab check

`N` is the lab number, 1 to 6. The committed code is the finished solution; the script
takes later labs' work out again as uncommitted changes under `src/`.

```bash
# Start of Lab N: exactly where the lab text begins. Stop Aspire first (it rebuilds).
./labs/prepare-lab.sh N

# Check your work: diff your tree against the END of Lab N (empty diff = you are done;
# ordering, comments and whitespace do not count -- add --strict to see those too)
./labs/prepare-lab.sh check N

# Catch up: skip to the start of the next lab with everything up to it applied
./labs/prepare-lab.sh N+1

# Back to the finished solution (the committed code)
./labs/prepare-lab.sh solution
```

Whatever is under `src/` when you switch is parked in a git stash first, never thrown
away: `git stash list` shows it, `git stash show -p stash@{0}` reads it back. Three flags:
`--force` skips that parking, `--no-build` skips the build at the end, and `--strict`
makes `check` show every difference, including ordering, comments and whitespace.

After any switch: restart Aspire, and reconnect the Inspector.

The visual version of `check`: the **Source Control** view (`Ctrl+Shift+G` /
`Cmd+Shift+G`) lists every file that differs from the finished solution; click one to see
the solution next to yours. It compares against the end of Lab 6, so later labs' files
stay listed, and it counts ordering and comments; `check N` compares against the end of
Lab N and ignores those.

## Lab-specific starts

```bash
# Lab 4: Dev Proxy, in its own terminal
cd labs/assets/lab-04 && devproxy --config-file latency.json      # or throttling.json

# Lab 5: the MCP endpoint protected by IdentityServer -- no special start, just set
#   "UseIdentityServer": true in src/aspire/TravelBooking.AppHost/appsettings.json and
aspire run

# Lab 6: Azure Container Apps
azd auth login --use-device-code
azd up                                                             # again after a code change: redeploys
azd down                                                           # when you are done

# azd up fails with "CONTAINER1013 ... being used by another process"? The container builds
#   ran in parallel and tripped over each other's cache. Usually a second azd up just works;
#   if it keeps failing, build one at a time:
AZD_CONCURRENCY_MAX=1 azd up
```

## Where things are

| What | Where |
|---|---|
| REST API + MCP endpoint | `http://localhost:5197` and `/mcp`; public: `https://${CODESPACE_NAME}-5197.app.github.dev` |
| Aspire dashboard | the `Login to the dashboard at http://localhost:15095/login?t=…` line in Aspire's output |
| MCP Inspector | the URL `npx @modelcontextprotocol/inspector` prints (port 6274) |
| IdentityServer (Lab 5) | `http://localhost:5001` |
| Playground | the link `deploy.sh` prints; it carries the agent version number |
