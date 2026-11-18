# Attendee Azure setup

Scripts that give each attendee an **Azure AI Foundry agent** wired to their own
Travel Booking MCP server. The agent is the only client attendees chat with; they
talk to it in the Foundry portal playground, and it reaches their server through
their Codespace's public port 5197 (Labs 0–5) or the Container Apps URL (Lab 6).

Everything here is built on the findings in
[`docs/spikes/foundry-agent-attendee-access.md`](../../docs/spikes/foundry-agent-attendee-access.md).
Read that before changing any API version, role GUID or payload shape — several
of them are the way they are for a non-obvious reason, and a few rest on
behaviour Microsoft does not document.

## Files

| File | Purpose |
|---|---|
| `deploy.sh` | Provision / converge / delete one attendee setup (or one instructor slot) |
| `invite.sh` | Invite a guest by email and grant them one slot |
| `common.sh` | Shared constants and helpers — sourced, not run |

## Attendee flow

```bash
# 1. Start the app (Lab 0), then make its port reachable from Azure
gh codespace ports visibility 5197:public -c "$CODESPACE_NAME"

# 2. Provision Azure, in a new terminal
az login --use-device-code
./infra/attendee/deploy.sh --mcp-url "https://${CODESPACE_NAME}-5197.app.github.dev/mcp"
```

The script prints a playground link. Open it and ask the agent to book a flight.

### Re-running

`deploy.sh` is idempotent — run it as often as you like. Each step checks before
it acts and reports `already correct` when there is nothing to do.

The commands from here on are run from `infra/attendee/`.

```bash
# MCP URL changed (a new Codespace, not just a restart)
./deploy.sh --mcp-url https://<new>/mcp

# Lab 2 onward: stop approving every single tool call
./deploy.sh --mcp-url https://<new>/mcp --approval never

# Lab 6: same agent, now pointing at Azure Container Apps
./deploy.sh --mcp-url https://travelbooking-host.<x>.<region>.azurecontainerapps.io/mcp
```

Changing `--mcp-url` updates the project connection (which is what actually
routes traffic) **and** the agent definition. Because agent versions are
immutable, the definition change publishes a new version; the script then
deletes the superseded ones and prints a fresh playground link. Use that link —
the old one carries a `?version=` that no longer exists.

### Why the URL lives in a connection *and* in the definition

The MCP URL is stored in a Foundry **project connection**, and the agent
references it by name. Learn is explicit that *"if you provide different
endpoints in the connection versus `server_url`, the endpoint in the connection
is used"* — so the connection is authoritative **for routing**. Agent
**versions are immutable** and `PATCH /agents/{name}` cannot touch
`definition.tools`, so routing through the connection is what lets a URL change
take effect at all.

The inline `server_url` is not therefore redundant, which we learned the hard
way. Foundry quotes it verbatim in MCP tool error messages, and it quotes it
even when the call went somewhere else entirely. An agent whose definition still
names a dead host reports *every* MCP failure against that host:

```txt
Error Authentication failed when connecting to the MCP server:
https://old-host.example:443/mcp: Response status code does not indicate
success: 401 (Unauthorized)
```

— while the request in fact went to the connection's target and failed there
for an unrelated reason. During development this sent us chasing a host that
did not even resolve. So the script keeps the two in step: the definition always
carries the current URL, at the cost of one new version per change.

> **Debugging note.** Because of the above, the host named in a Foundry MCP
> error is only trustworthy if `deploy.sh` ran cleanly. If the connection update
> succeeded but the agent update failed, the message will name the old host.

> `deploy.sh` deliberately does **not** use
> `az cognitiveservices account project connection create`. That command has no
> `CustomKeys` case, and it silently rewrites `authType: "None"` into `"AAD"`
> for any category outside Git/PythonFeed — producing a connection that fails
> against an anonymous endpoint with nothing in the output explaining why.
> We `az rest` the ARM resource directly instead.

## Instructor fallback slots

For attendees whose subscription cannot deploy a model (see
[Free-trial quota](#free-trial-quota)), pre-create numbered slots in your own
subscription and invite them in.

```bash
# Create three slots (do this before the event)
for n in 1 2 3; do
  ./deploy.sh --slot "$n" --location swedencentral --mcp-url https://placeholder.invalid/mcp
done

# Give slot 2 to an attendee
./invite.sh --slot 2 --email alice@example.com --name "Alice"

# On the day, point their slot at their actual Codespace
./deploy.sh --slot 2 --mcp-url https://<their-codespace>-5197.app.github.dev/mcp

# Afterwards
./invite.sh --slot 2 --email alice@example.com --revoke
./deploy.sh --delete --all-slots
```

Each slot is its own resource group (`rg-mcpws-slotNN`) with its own Foundry
account, project, model deployment and agent. Attendees get exactly one role —
**Foundry User**, scoped to that resource group — which is enough to browse the
project and chat with the agent, and nothing else in the subscription.

No `Reader` and no subscription-scoped role is needed: Foundry User already
carries `Microsoft.Resources/subscriptions/resourceGroups/read` and
`Microsoft.CognitiveServices/*/read`.

> **Guests must redeem their invitation before the workshop.** An unredeemed
> guest cannot sign in, and the error (*"account does not exist in tenant"*)
> does not say why. `invite.sh` sends the mail and also prints a direct
> redemption link.

> B2B guests **can** use the playground. The warning in Foundry's MCP docs that
> *"the user's Entra tenant must match the tenant of your Foundry project"*
> applies only to **OAuth identity passthrough**, which we do not use — our
> connections are unauthenticated or `CustomKeys`, and for both the docs mark
> "user context persists: No".

## Parameters

| Flag | Default | Notes |
|---|---|---|
| `--mcp-url` | *(required)* | Full HTTPS URL including `/mcp`; a URL that does not end in `/mcp` gets it appended. Rejected if it's http, localhost, or missing. |
| `--location` | `eastus2` | Must be a Foundry **Agent Service** region — a shorter list than model availability. `swedencentral` is the other recommended choice. |
| `--model` | `gpt-4.1-mini` | See [Model choice](#model-choice). |
| `--model-version` | `2025-04-14` | |
| `--capacity` | `50` | TPM cap in thousands, so 50 = 50,000 TPM. A booking turn in the playground is ~5 model requests that each resend the whole thread and the tool schemas, metered in windows of a few seconds; 10K throttled mid-turn once a thread had any history. |
| `--approval` | `always` | `always` for Lab 1, `never` from Lab 2. |
| `--mcp-token` | — | Bearer token for a secured `/mcp` (Lab 5). See below. |
| `--no-app-insights` | — | Skip provisioning or updating Application Insights; Aspire/OTLP tracing remains available. Existing Application Insights state is unchanged. See [Tracing](#tracing). |
| `--no-appsettings` | — | Provision tracing but don't touch `appsettings.json`. |
| `--slot <n>` | — | Instructor fallback mode. |
| `--delete` | — | Teardown. Add `--all-slots` to remove every slot. |
| `-y`, `--yes` | — | Skip confirmation prompts. |

## Tracing

`deploy.sh` also provisions an **Application Insights** instance (plus the Log
Analytics workspace it has to ingest into — classic App Insights is retired) and
connects it to the Foundry project. Two things then happen, and they are
independent of each other:

1. **Foundry starts recording agent traces.** Connecting the resource is the
   whole switch — no code, no SDK. Every prompt-agent run shows up under
   **Traces** in the portal. This matters because Application Insights is the
   *only* place those traces exist; Foundry cannot be pointed at an OTLP
   endpoint, so they will never reach the Aspire dashboard.
2. **The attendee's own server exports there too.** The script writes the
   connection string into `src/TravelBooking.Host/appsettings.json` as
   `AzureMonitor:ConnectionString`, which `ServiceDefaults` picks up and enables
   the Azure Monitor exporter *alongside* the OTLP export to the Aspire
   dashboard — not instead of it. The dashboard keeps working exactly as before.

Because both halves land in one store and Application Insights maps the W3C
trace id onto its own `operation_Id`, they join on their own: one trace runs from
the sentence typed in the playground, through the model's tool call, into the
attendee's server, down to the SQL that reserved the seat.

```bash
# Print the connection string but leave the repo alone
./deploy.sh --mcp-url https://.../mcp --no-appsettings

# Skip Application Insights provisioning (existing state untouched)
./deploy.sh --mcp-url https://.../mcp --no-app-insights
```

Notes:

- The connection string is printed at the end of every run, and it carries an
  **ingestion key** — anyone holding it can write telemetry into the resource.
  That is acceptable for a workshop, but `appsettings.json` is tracked by git, so
  don't commit it.
- `--delete` clears the value out of `appsettings.json` again, but only when it
  still matches the instance being deleted. A string the attendee pointed
  somewhere else of their own is left alone.
- Re-running is free: the workspace and component are created once, and the
  project connection is re-written every time on purpose. A stored credential is
  never returned by a GET, so matching on the target alone would not prove the
  key is current — and a component that was deleted and recreated keeps the same
  resource id with a *different* ingestion key.
- Guests on a fallback slot need `Log Analytics Reader` on top of `Foundry User`
  to open the Traces tab; `invite.sh` grants both.
- Cost: Log Analytics bills by ingested volume with a monthly free allowance, and
  a workshop's worth of traces sits well inside it. It is deleted along with
  everything else by `./deploy.sh --delete`.

## Lab 5 — authenticating the agent

Once Lab 5 secures `/mcp`, the agent stops working. Foundry is **not** a
spec-compliant MCP OAuth client: it ignores the 401, the `WWW-Authenticate`
header and the protected-resource metadata document, so it cannot discover the
authorization server the way MCP Inspector and VS Code do.

The workaround is a static bearer token on the connection. Get one from the identity
provider's token page — `http://localhost:5001/Token` (the forwarded URL for port 5001 in
a Codespace) — signing in as `alice` / `alice123` and granting both `mcp:tools` and
`booking:write`. Then:

```bash
export TOKEN='<paste the token here>'

./deploy.sh --mcp-url "https://${CODESPACE_NAME}-5197.app.github.dev/mcp" \
            --mcp-token "$TOKEN" --approval never
```

Behaviour worth knowing:

- A later run **without** `--mcp-token` keeps the existing credential — it will
  not silently drop it and break the agent.
- `--mcp-token ""` clears it and returns the connection to anonymous.
- A stored credential is never returned by a GET, so the script cannot tell
  whether the token changed. Passing a token always re-writes the connection,
  which is exactly what you want after getting a fresh one from `/Token`.

> ⚠️ This is the anti-pattern Lab 5 opens by condemning — a long-lived static
> credential — and it is worth saying so out loud in the room. Microsoft
> documents that *"people who have access to the project can access an API key
> stored in a project connection."* The token also does not refresh;
> `AccessTokenLifetime` on both clients (`workshop-token-helper`, which the `/Token`
> page uses, and `mcp-inspector`) is set to 8 hours so it
> survives the teaching day, which is itself a workshop-only concession.

## Model choice

`gpt-4.1-mini` is not merely the cheap option — on a low-quota subscription it is
the **only** workable one:

- Full `gpt-4.1` is **absent from quota Tier 0** entirely. A Tier 0 subscription
  cannot deploy it at any price; moving up is a usage-based tier change, not a
  same-day lever.
- `gpt-5-mini` has 2.5× the Tier 0 quota but Agent Service marks it
  **Functions: No, OpenAPI: No** — a bad trade in a tools workshop. The gpt-5
  family also needs separate access registration.
- `o4-mini` has a punishing 1 RPM per 1,000 TPM ratio.

`gpt-4.1-mini` is the only Tier 0 model with a full tool-support row (MCP,
Functions, OpenAPI, Code Interpreter, Azure AI Search).

> **Burst headroom, not tokens, is the Tier 0 risk.** 200 RPM, and one turn that
> chains four tool calls is ~5 model requests. Thirty attendees pressing send
> together is ~150 requests in a burst. Stagger exercises or expect the odd 429.

## Free-trial quota

Microsoft documents free trials at **0 TPM for all models**, and free trials are
**not eligible for quota increases**. The newer quota-tier system may supersede
this — Tier 0 does include `gpt-4.1-mini` at 200K TPM — but Learn never states
which tier a fresh trial lands in.

`deploy.sh` reads the subscription's `quotaId` and quota tier during preflight
and warns before it gets to the failing step. If the model deployment is refused:

1. Upgrade to pay-as-you-go (self-service in the portal) — **do this at least a
   day ahead**, propagation is not instant.
2. Or lower the cap: `--capacity 1`.
3. Or use an instructor slot.

## Exposing the MCP server — Codespaces, not Dev Tunnels

Attendees run in a Codespace, and Codespaces port forwarding is itself built on
the Dev Tunnels service — so the `devtunnel` CLI adds nothing to the attendee
path. Make port 5197 public instead:

```bash
gh codespace ports visibility 5197:public -c "$CODESPACE_NAME"
./deploy.sh --mcp-url "https://${CODESPACE_NAME}-5197.app.github.dev/mcp"
```

> ⚠️ **Public ports revert to private when the Codespace restarts**, and
> Codespaces idle out after 30 minutes by default. The dev container's
> `postStartCommand` makes 5197 public again on every start, but that step can
> fail, and then an attendee who takes a break comes back to an agent that fails
> with no obvious cause. Re-running the `visibility` command fixes it; the
> provisioning does not need to change, because the URL is derived from the
> Codespace name and stays stable.

Dev Tunnels remains useful to the **workshop authors** running the stack outside
a Codespace. It is deliberately not installed in the image:

```bash
curl -sL https://aka.ms/DevTunnelCliInstall | bash   # authors only
```

### Devcontainer — applied

`.devcontainer/codespaces/devcontainer.json` forwards **5197** (the port every lab
uses), **5001**, **6274** and **15095** (and 5000), with labels so the Codespaces
ports panel is readable.

`jq` is used by both scripts; `.devcontainer/codespaces/post-create.sh` installs it.

## Things that rest on undocumented behaviour

Called out so a failure points at the right suspect rather than sending you
hunting. Each is flagged in the scripts too.

| What | Why it matters |
|---|---|
| **Playground deep link** format | Undocumented; derived from Microsoft's own `azd` extension source and checked against a real emitted URL. If the link 404s, the provisioning still worked — open `https://ai.azure.com/?tid=<tenant>` and navigate manually. |
| **`GET /agents/{name}` returns 404** when absent | Confirmed via azd's source, not docs. `deploy.sh` branches on it to decide create-vs-update. |
| **Connection target overrides `server_url`** | Documented, but we verify after writing and warn if the two diverge. |
| **Whether an RG-scoped assignment is enough** for the Foundry portal's subscription picker | No doc either way. **The highest-risk unknown in the slot plan** — dry-run one slot with a real external address before the event. |
| **Re-POST to `/invitations`** for an existing guest | Undocumented. Avoided entirely: `invite.sh` looks the guest up first. |
| **Whether RG deletion cascades role assignments** | Only inferred. Both scripts delete assignments explicitly, first. |
