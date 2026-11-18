# Spike — Azure AI Foundry agent as the workshop's MCP client

> **Phase 0 findings. Decision document — not yet implemented.**
> Researched 2026-10-05 against current Microsoft Learn. Every claim below is marked
> **[V]** verified against a first-party source, **[C]** community evidence only, or
> **[?]** could not be verified. Where Learn is silent, this document says so rather
> than guessing.

## Context

The decision under test: attendees talk to their Travel Booking MCP server **exclusively
through an Azure AI Foundry agent**, chatting in the Foundry portal playground. The agent
reaches the server through a Dev Tunnel in Labs 0–5 and through the Azure Container Apps
URL in Lab 6.

## Verdict in one paragraph

**The core path works and Microsoft runs a workshop on it themselves** — Foundry Agent
Service's MCP tool speaks Streamable HTTP to an arbitrary public HTTPS endpoint, and MS's
own AI Tour lab WRK540 drives it over an anonymous Dev Tunnel [V]. Three things, however,
are not negotiable detours but changes to what the workshop *teaches*: **Foundry is not a
spec-compliant MCP OAuth client** and will ignore the 401 + protected-resource-metadata
discovery that is the centrepiece of Lab 5; **Dev Proxy can no longer sit in the request
path** the way Lab 4 uses it; and **free-trial subscriptions probably cannot deploy a model
at all**. Each has a workaround, but two of them need your decision rather than my
judgement — they trade away teaching content.

---

## Decisions — resolved 2026-10-05 (Loek)

All four decided as recommended.

**D1 — Lab 5's OAuth discovery lesson: keep MCP Inspector for that demonstration.**
Foundry will not perform MCP's discovery dance [V], so the lab cannot show "compliant
client sees 401, reads metadata, starts OAuth automatically" through Foundry. Inspector
keeps the lesson intact and gains a better framing: *the spec defines discovery, our SDK
implements it, and a major hosted client hasn't shipped it yet.* See [B2](#b2).

**D2 — "Exclusively Foundry" bans LLM clients, not the protocol harness.** GitHub Copilot
goes; MCP Inspector stays. Inspector remains load-bearing in Labs 1–4 for deterministic,
model-free tool calls and for showing raw JSON-RPC.

**D3 — Lab 4 fault injection.** ~~Tunnel → Dev Proxy → 5197.~~
**Superseded 2026-10-05: fault injection stays client-side.**

The original ruling put Dev Proxy in the inbound path so injected faults would hit the
Foundry→server leg. On closer reading of Dev Proxy's architecture that does not work:
Dev Proxy is a **forward** proxy, effective only because the *client* is configured to
route through it (`curl -x`), and a cloud agent calling a public URL never will. It would
have to act as a reverse proxy, which is unverified.

Lab 4 therefore keeps Dev Proxy and `curl -x` exactly as written, driving the server from
inside the Codespace, and uses the agent to show the *consequence* — hold a seat through
the latency proxy, let it expire, then ask the agent to confirm and watch it fail cleanly.
If Dev Proxy turns out to reverse-proxy after all, the original plan returns with no
rewrite. See [B4](#b4) and the Phase 2 proposal.

**D4 — `require_approval`: `"always"` in Lab 1, `"never"` from Lab 2 onward.** Lab 1 keeps
the approval prompt as a teaching beat (it mirrors the agent-approval UX attendees will
meet in the wild); later labs drop it so exercises don't cost four extra clicks each.

**Empirical spike: not run up front.** Loek will execute the labs end to end and surface
the remaining unknowns that way. Phase 1 is therefore written defensively — every step
resting on an unverified assumption emits a diagnostic naming that assumption, so a
failure identifies *which* one broke.

---

## Q1 — Can a free trial deploy a chat model?

### The blocking finding

Two independent gates hit a free trial, and neither can be lifted on one:

- The offer-type quota table lists **Free Trials → all models: 0 TPM** [V].
- **Free trial subscriptions are not eligible for quota increases**; the only documented
  remedy is upgrading to pay-as-you-go [V].

### The complication that stops this being a clean "no"

That table lives in the **legacy** `/azure/ai-foundry/` doc tree. The **current**
`/azure/foundry/` tree deleted it and replaced the whole model with **quota tiers**,
effective after 2026-05-07 [V]. Tier 0 ("Free Tier") does contain a usable model:

| Tier 0 model | Deployment | RPM | TPM |
|---|---|---|---|
| `gpt-4.1-mini` | GlobalStandard | 200 | 200,000 |
| `gpt-5-mini` | GlobalStandard | 500 | 500,000 |
| `o4-mini` | GlobalStandard | 100 | 100,000 |
| `text-embedding-3-small` | GlobalStandard | 1000 / 10s | 1,000,000 |

**Learn never states which tier a fresh free trial lands in** [?]. It says only that "a
customer's initial assigned tier is based on their current usage and their current
relationship with Microsoft." Community evidence shows an Azure-for-Students user sitting
in Free Tier [C]. So the honest position is: *best case* `gpt-4.1-mini` at 200K TPM,
*worst case* 0 TPM and a hard stop, and **no document resolves which**. This is the single
question worth 30 minutes of empirical testing before Phase 1 is written — see
[the spike plan](#recommended-empirical-spike).

Azure for Students is worse, not better: 1K TPM on the legacy table [V], which a single
turn carrying system prompt plus six tool schemas can exceed.

### Model recommendation — `gpt-4.1-mini`, and the choice is forced

I went looking for a more capable model on your "it's only one day, we can afford more
than the cheapest" instinct. The instinct is right but the lever doesn't exist:

- **Full `gpt-4.1` is absent from Tier 0 entirely** [V]. It first appears in Tier 1. A
  Tier 0 subscription *cannot deploy it* — this is a tier move (automatic, usage-based),
  not a same-day deployment choice.
- **`gpt-5-mini` has 2.5× the Tier 0 quota but is disqualified on capability**: the Agent
  Service tool matrix marks it **Functions: No, OpenAPI: No** [V]. It supports the MCP
  tool, but losing the rest of the tool surface in a tools workshop is a bad trade.
- **`o4-mini`** has a punishing 1 RPM per 1K TPM ratio and no OpenAPI support [V].

`gpt-4.1-mini` is the only Tier 0 model with a **full** tool-support row (MCP, Functions,
OpenAPI, Code Interpreter, Azure AI Search all Yes) [V], and neither it nor full `gpt-4.1`
needs gated-access registration — unlike `gpt-5`/`gpt-5-pro`/`gpt-5-codex`, which do [V].

On whether mini is *good enough*: Learn offers no minimum model class for tool calling, and
its two relevant pages point opposite ways. The model-choice guide pushes GPT-5 for
"agentic workflows… that must plan, call multiple tools, and act" [V]; the model-router
page classes "tool orchestration — calling a function, passing arguments, formatting
results" as **medium complexity** suited to "mid-tier models", adding that "mechanistic
tool calls don't require expensive models" [V]. A deterministic four-step chain against a
known server is the second case. No documented tool-calling difference exists between
`gpt-4.1` and `gpt-4.1-mini`; both are listed for parallel function calling [V].

Real pricing, pulled from the first-party Azure Retail Prices API because the pricing page
renders client-side and serves `$-` placeholders [V]:

| Model | Input / 1M | Output / 1M |
|---|---|---|
| `gpt-4.1-mini` | $0.40 | $1.60 |
| `gpt-4.1` | $2.00 | $8.00 |

Full gpt-4.1 is exactly 5× mini. Across 30 attendees at ~40 turns each that's roughly
**$13 versus $67** — trivial either way, which means **reliability, not cost, is the only
argument**, and Learn doesn't support the reliability worry for this workload.

> ⚠️ **Burst headroom is the real Tier 0 risk, not tokens.** 200 RPM, and a turn chaining
> four tool calls is ~5 model requests. Thirty attendees hitting "send" together is ~150
> requests in a burst. It fits, barely. Stagger the exercises or expect occasional 429s.

### Regions

`--location` defaults to **`eastus2`**; your **`swedencentral`** is equally first-class.
Both are on the 30-region Agent Service list, both carry the higher **2,000** concurrent
hosted-session limit (vs 1,000 elsewhere), both have `gpt-4.1-mini` *and* `gpt-4.1` on
GlobalStandard, and both have full tool support including Computer Use [V]. Avoid Italy
North and Brazil South (no file search) and West Europe (1,000 sessions).

> Since May 2026, GlobalStandard quota is a **single subscription-wide pool across all
> regions** [V] — picking a different region no longer multiplies your quota.

---

## Q2 — The Foundry Agent Service MCP tool

### Two generations; build against the new one

| | Foundry (classic) | Foundry Agent Service (current) |
|---|---|---|
| Shape | threads / runs / `tool_resources` | agents + versions, Responses API |
| MCP tool | Preview | **GA** |
| api-version | classic | **`v1`** |
| Lifecycle | **retires 2027-03-31** [V] | current |

Most blog and Stack Overflow material describes classic and is now wrong. Phase 1 targets
`api-version=v1`.

### Transport — our server is already the right shape

stdio is rejected outright: "the Agent Service runtime only accepts a remote MCP server
endpoint" [V]. Streamable HTTP is the transport used everywhere in MS material [V].
`server_url` takes a full URI including path and even query string [V], so
`https://<host>/mcp` is correct. TLS 1.2+ required; **mTLS not supported**; Azure service
tags not supported for MCP client traffic [V]. Public endpoints are *not* required —
private VNet works, but only with Standard Agent Setup + BYO VNet, which is out of scope
here [V]. Whether SSE is still accepted is **[?]** — Learn takes no position.

### Changing the URL later — this solves your `--mcp-url` requirement cleanly

Agent *versions* are immutable, and `PATCH /agents/{name}` touches only `agent_endpoint` /
`agent_card`, **not** `definition.tools` [V]. So a naive design would recreate the agent on
every tunnel rotation. It doesn't have to, because of this, verbatim from Learn [V]:

> `project_connection_id`: The connection name that stores the MCP server endpoint,
> authentication selection, and relevant information. **If you provide different endpoints
> in the connection versus `server_url`, the endpoint in the connection is used.**

**Phase 1 will put the URL in a project connection and point the agent at it by
`project_connection_id`.** Re-running `deploy.sh --mcp-url <new>` then updates the
connection's `--target` and never touches the agent. That covers both the Dev Tunnel
rotation and the Lab 6 cutover to the ACA URL, with no agent churn and no change to the
playground link attendees have bookmarked.

> **Amended 2026-10-05 after test items 3 and 4 ran live.** The routing claim above is
> confirmed: the connection target *is* what Foundry dials, and the quoted Learn sentence
> holds [V]. The *conclusion* drawn from it — "never touches the agent" — was wrong, for a
> reason the docs do not mention. **Foundry renders the definition's `server_url` into its
> MCP error messages, irrespective of where the request actually went** [V, observed]. A
> connection pointing at a live Codespace plus a definition still naming a placeholder
> host produced:
>
> ```txt
> Error Authentication failed when connecting to the MCP server:
> https://bla.com:443/mcp: ... 401 (Unauthorized)
> ```
>
> `bla.com` does not resolve (`curl` exit `000`), so it cannot have returned a 401. The
> real failure was at the connection's target — a Codespace port that had reverted to
> private, where GitHub's front door answers with a redirect to its sign-in page. Two
> misleading signals stacked: the wrong host *and* an auth error for a non-auth problem.
>
> `deploy.sh` therefore writes the current URL into **both** the connection and the
> definition, accepting one new immutable version per URL change and pruning the
> superseded ones. Diagnosability beat version stability. The cost is that the playground
> link moves, which the labs now state outright — Lab 2 already had this behaviour for
> `--approval`, so the pattern is not new to attendees.

### Authentication — the important negative result

| Mode | Supported? |
|---|---|
| Anonymous (`--auth-type none`) | ✅ [V] |
| Static header via connection (`--auth-type custom-keys --custom-key "Authorization=Bearer …"`, repeatable) | ✅ [V] |
| Inline per-tool `headers` field | ⚠️ exists in schema, **service rejects it** — see B3 |
| Full OAuth 2.1 / DCR / MCP discovery | ❌ **No** [V] |

Foundry is a **statically-configured OAuth client**. Custom OAuth requires you to hand it
Client ID, Auth URL, Refresh URL, Token URL and scopes by hand, and register a redirect URL
it gives you [V]. The terms "dynamic client registration", "WWW-Authenticate",
`.well-known`, "oauth-protected-resource" and RFCs 7591/8414/9728 appear **nowhere** on the
Foundry MCP pages (verified by exhaustive grep) [V]. Docs cite OAuth **2.0 / RFC 6749**;
"OAuth 2.1" and PKCE are never mentioned [?].

It is *not* Entra-only — "available for Microsoft and **non-Microsoft** MCP servers…
compliant with OAuth" [V] — so IdentityServer is a legitimate target in principle. But
OAuth mode is **per-user and interactive**: first use emits an `oauth_consent_request` with
a `consent_link` a human must open [V]. Thirty attendees means thirty browser consent
dances. And it requires the user's Entra tenant to match the project's — "cross-tenant
token exchange isn't supported" [V], which matters for guest users in your fallback slots.

### Other constraints worth designing around

- **100-second timeout** on non-streaming calls [V] (classic was 50s). This collides with
  Lab 2's debugger exercise — see [B7](#b7).
- **Tool schemas using `anyOf`/`allOf` or multi-type params are rejected** with "Invalid
  tool schema" [V]. Our current tools are clean, but Lab 4 introduces
  `string? customerId = null` — see [B8](#b8).
- `allowed_tools` is case-sensitive and must match `tools/list` exactly [V].
- The **.NET SDK is prerelease** [V]. Irrelevant to `deploy.sh` (bash + az), but relevant
  if we ever script this in C#.
- Agents can reach "any publicly accessible remote MCP server" [V].

---

## Q3 — Dev Tunnels

### Commands for a persistent, anonymous tunnel on 5197

```bash
curl -sL https://aka.ms/DevTunnelCliInstall | bash

devtunnel user login -g -d                              # GitHub + device code (Codespaces)
devtunnel create mcpworkshop --allow-anonymous          # -a is the short form
devtunnel port create mcpworkshop -p 5197 --protocol https
devtunnel host mcpworkshop
```

All [V]. Anonymous *hosting* is impossible — "Dev Tunnels doesn't support hosting tunnels
anonymously" [V] — so attendees must log in; `-d` (device code) is the Codespaces-safe
flow. Anonymous *access* can also be granted after the fact with
`devtunnel access create <id> --anonymous`.

**Persistence:** running `devtunnel create` first is what makes the tunnel persistent — a
bare `devtunnel host` creates a temporary tunnel deleted when the connection closes [V].
Re-hosting the same tunnel ID keeps the same URL: "the tunnel URL stays the same, so you
don't need to update your… endpoint configuration" [V]. Expiry is a **sliding 30 days of
inactivity**, `--expiration` accepts 1h–30d [V].

> ⚠️ **A custom tunnel ID does not give you a custom URL.** Learn has an FAQ heading that
> says so outright: "Why would I want to choose a tunnel ID if it's not used in the tunnel
> URL?" [V]. `devtunnel create mcpworkshop` is a memorable CLI handle, nothing more — so
> the script cannot pre-compute the URL, and you can't pre-print it on a slide.

**Reading the URL programmatically:** `devtunnel show <id> --json` works and Microsoft's own
code uses it (`microsoft/TypeAgent` reads `tunnel.hostConnections`; others read
`tunnel.ports[].portUri`) — but **`--json` is undocumented on Learn** [C]. Phase 1 will use
it with a stdout-regex fallback, and treat the schema as unstable.

**Limits (per user, monthly reset)** [V]: 5 GB bandwidth · 10 tunnels · 10 ports per tunnel
· 1000 connections per port · 1500 requests/min per port · 16 MB max HTTP body. Dev Tunnels
is still **public preview, no SLA** [V].

### The anti-phishing interstitial is a non-issue

Learn lists the skip conditions verbatim [V]: non-`GET` method; `Accept` not containing
`text/html`; presence of `X-Tunnel-Skip-AntiPhishing-Page`; presence of
`X-Tunnel-Authorization`; or the user already clicked through.

MCP Streamable HTTP satisfies **two of these unconditionally**: `initialize`, `tools/list`
and `tools/call` are all **POST** (the spec says the client *MUST* use POST) [V], and the
SSE stream is a GET with `Accept: text/event-stream`, never `text/html` [V]. A community
capture of Foundry's actual outbound request confirms `Accept: application/json,
text/event-stream` [C]. Learn corroborates generally: "this notification page isn't shown
for API requests using dev tunnels" [V].

So **even if Foundry could not set custom headers, it would not hit the interstitial.**
Keep the header as belt-and-braces; don't build lab narrative on it. Note the header's
*value* is undocumented — Learn says only that the request "contains" it; the `: true` form
you'll find online is community lore [C].

### Codespaces

`devtunnel` inside a Codespace is **[?] — Learn never addresses it**, but the grounding is
strong: it's a plain Linux x64 binary needing only outbound HTTPS to a documented domain
list [V], and Codespaces port forwarding is itself built on the Dev Tunnels service [V].
Use `-d` device-code login; the localhost-redirect OAuth flow is the known Codespaces
failure mode.

**Codespaces' own port forwarding is the wrong choice here**, for one decisive reason [V]:

> A public forwarded port will automatically revert back to private when you remove and
> re-add the port, **or if you restart the codespace**.

Combined with the 30-minute default idle timeout [V], an attendee who goes for coffee
returns to a codespace that stopped, restarted, and **silently made the port private**.
Foundry then fails with no visible cause. Dev Tunnels survives this.

---

## Q4 — Does Foundry propagate trace context?

### Headline

**Foundry does send a W3C `traceparent` header to external MCP servers — but with the
sampled flag OFF (`-00`), and it does not populate MCP's `params._meta`.** Microsoft
documents none of this.

The MCP tool page contains **zero** occurrences of `trace`, `telemetry`, `observability`,
`traceparent` or `correlat` [V, by exhaustive grep]. The only evidence is a community
capture of a real Foundry → self-hosted MCP request [C]:

```
Accept: application/json, text/event-stream
User-Agent: openai-mcp/1.0.0
traceparent: 00-79f93fda2abc02f31cafad4187d239d2-433bd352f4e4c8eb-00
Mcp-Protocol-Version: 2025-06-18
Mcp-Session-Id: k4dmO4FG9MmQBAtdGgjxww
```

Three things to read out of that: the trace ID **does** cross the boundary; the trailing
**`-00`** means *not sampled*; and the `User-Agent` reveals Foundry's MCP client is the
**OpenAI MCP stack**, not an Azure SDK. The same thread shows `tools/call` carrying only
`"_meta": {"progressToken": 4}` — **no `traceparent` in `_meta`** [C].

### The one line that decides whether attendees see anything

OpenTelemetry's default sampler is `ParentBased(root=AlwaysOn)`, whose
`remoteParentNotSampled` branch is **`AlwaysOff`** [V]. A default-configured MCP server
therefore receives Foundry's trace ID and **throws the span away**. The attendee sees an
empty dashboard and concludes nothing works.

**`OTEL_TRACES_SAMPLER=always_on` is the single most important line in the revised labs.**

### Can the two halves be joined?

Foundry's server-side traces go to **Application Insights only** — "in Foundry, traces are
stored in Azure Monitor Application Insights" [V]. There is **no documented way to point
server-side Foundry tracing at an arbitrary OTLP endpoint** [V, by documented silence].
OTLP freedom exists only for *client-side* tracing of your own app code [V].

Consequence for the labs: **locally (Labs 3–5), the Aspire dashboard can never show
Foundry's half of the trace.** Two half-traces, same trace ID, two stores, no UI that joins
them. In Lab 6, pointing the MCP server at the *same* App Insights resource Foundry uses
makes the transaction view join them natively (App Insights maps W3C trace-id →
`operation_Id`) [V]. Azure Container Apps' managed OTel agent can fan out to App Insights
**and** an OTLP endpoint simultaneously [V], so Lab 6 can have both.

### What our server already does right

The MCP C# SDK implements **both halves of `_meta` propagation already** [V, from source]:
`ActivitySource` is `Experimental.ModelContextProtocol`; the server extracts parent context
from `_meta` and the client injects it; gating is only `ActivitySource.HasListeners()`, so
`.AddSource("Experimental.ModelContextProtocol")` is all that's needed. MCP standardised
this in **SEP-414, status Final** — `traceparent`/`tracestate`/`baggage` go in
`params._meta` unprefixed [V].

So: the protocol defines it, our SDK implements it, and **Foundry's hosted client hasn't
caught up**. Today we fall back to the HTTP header, which ASP.NET Core instrumentation
picks up natively. That gap is genuinely good teaching material rather than an
embarrassment — see the Phase 2 proposal.

**Sampling-proof fallback:** Foundry *does* send `Mcp-Session-Id` [C], and semconv defines
`mcp.session.id` as a Recommended attribute [V]. Tagging every server span with it gives a
join key that needs nothing from Foundry.

### Three claims not to put on a slide

- *"Foundry propagates trace context per spec"* — it sends an HTTP header, not `_meta`, and
  Microsoft documents neither.
- *"`AZURE_TRACING_GEN_AI_ENABLE_TRACE_CONTEXT_PROPAGATION` makes Foundry→MCP tracing
  work"* — it only affects your app → Foundry via `get_openai_client()`, and it's
  Python-only [V].
- *"`enable_telemetry` enables agent tracing"* — it raises `ImportError`. It exists in the
  official README but **in no released `azure-ai-agents` package** [V, from source].

---

## Q5 — Resource providers on a fresh subscription

None of these are registered by default [V]:

```bash
az provider register --namespace Microsoft.CognitiveServices --wait   # Foundry + model deployment
az provider register --namespace Microsoft.KeyVault --wait
az provider register --namespace Microsoft.Storage --wait
az provider register --namespace Microsoft.MachineLearningServices --wait
az provider register --namespace Microsoft.Search --wait
az provider register --namespace Microsoft.App --wait                 # Agent Service + Container Apps
az provider register --namespace Microsoft.ContainerService --wait
az provider register --namespace Microsoft.OperationalInsights --wait # Log Analytics (Lab 6)
az provider register --namespace Microsoft.Insights --wait            # App Insights (Lab 6)
```

The seven-RP list is the documented prerequisite for Agent Service [V]; the last two come
from the Container Apps quickstarts [V]. For a *basic* Foundry setup,
`Microsoft.CognitiveServices` alone is likely sufficient — every hosted-agent quickstart
lists exactly one RP fix in troubleshooting, `SubscriptionNotRegistered` →
`az provider register --namespace Microsoft.CognitiveServices` [V] — but registering all
nine is cheap insurance [inference].

`Microsoft.Bing` is needed **only** for the Grounding with Bing tool [V]; `Microsoft.Network`
only for private networking [V]. Neither applies here.

Polling:

```bash
az provider show --namespace Microsoft.CognitiveServices --query registrationState -o tsv
az provider list --query "[?registrationState!='Registered'].namespace" -o tsv
```

> ⚠️ Registration is **per-region and incremental**. Learn warns: "your application code
> shouldn't block the creation of resources for a resource provider that's in the
> registering state" [V]. `deploy.sh` will warn-and-continue rather than hard-block on a
> provider stuck in `Registering`.

**No subscription-level feature flag is required** for this scenario [V, by absence across
all prerequisite pages]. The only gated thing is the gpt-5 family, which we're not using.

Useful preflight — identify the subscription offer and its quota tier:

```bash
az rest --method GET --uri "https://management.azure.com/subscriptions/$SUB?api-version=2020-01-01" \
  --query "subscriptionPolicies.quotaId" -o tsv        # FreeTrial_2014-09-01 == trial

az rest --method GET --uri "https://management.azure.com/subscriptions/$SUB/providers/Microsoft.CognitiveServices/quotaTiers?api-version=2025-10-01-preview"
```

Both [V]. These two calls are the heart of the Phase 1 preflight.

---

## Blockers and proposed workarounds

<a id="b1"></a>
### B1 — Free trial probably cannot deploy a model · **severity: high**

Documented 0 TPM for trials, no quota increases possible [V]; current tier system may or
may not supersede this [?]. **Workaround:** require pay-as-you-go. Run the trial → PAYG
upgrade as **pre-work at least a day before** the event (there are community reports of
propagation lag [C]). The `--slot` fallback already in your Phase 1 brief is the real
safety net. `deploy.sh` preflight reads `quotaId` and the quota tier and fails with a clear
message rather than a confusing deployment error.

<a id="b1b"></a>
### B1b — CONFIRMED IN PRACTICE: the instructor subscription is blocked · **severity: critical**

**Observed 2026-10-05 on Loek's own subscription**, running `deploy.sh` for real. This is
no longer a documentation risk; it happened.

- The subscription is **Microsoft Azure Sponsorship** and reports an **empty `quotaId`**,
  so it is neither the free-trial case nor pay-as-you-go. The documented free-trial
  remedy ("upgrade to pay-as-you-go") **does not apply — there is nothing to upgrade.**
- The Foundry **account and project were created successfully**. The gate is specifically
  on **model deployment**, which failed with Azure error code **`715-123420`**
  ("unusual activity"). That is an **anti-abuse gate on the subscription or billing
  account**, not a quota, region or model problem — the request never got as far as
  validating the model.
- `gpt-4.1-mini` `2025-04-14` on `GlobalStandard` in `swedencentral` was separately
  confirmed available, so the request itself was valid.

**Why this is worse than B1.** The fallback slots live in *this same subscription*. If the
instructor subscription cannot deploy a model, **neither self-service nor the fallback
path works** — the entire Azure side of the workshop is blocked by one gate.

**Workaround:** there is no CLI flag or payload change that clears this. It needs a
Microsoft support request quoting `715-123420`, stating that this is an Azure AI Foundry
chat model deployment for a training workshop, and noting that other Cognitive Services
resources already deploy fine in the subscription (that detail reportedly speeds up
review). These are typically cleared in a day or two. **This is now the single
highest-priority pre-event task** — see the runbook in the Phase 2 document.

`deploy.sh` detects both halves of this: the preflight warns when `quotaId` is empty, and
the deployment step has a dedicated `715-123420` branch that explains the gate rather than
sending you to check model availability.

<a id="b2"></a>
### B2 — Foundry is not a spec-compliant MCP OAuth client · **severity: high, pedagogical**

Lab 5's best moment currently reads: *"Compliant clients (VS Code, Inspector) read it and
start the OAuth flow automatically — this is how MCP makes auth discoverable instead of
documented-in-a-readme."* Foundry does none of this [V]. Options:

1. **Keep MCP Inspector for that one demonstration** *(my recommendation)*. The lesson
   survives intact, costs ~5 minutes, and gains a genuinely better framing: *the spec
   defines discovery, the SDK implements it, and a major hosted client hasn't shipped it
   yet.* That's a more honest "crossing the chasm" lesson than pretending everyone
   complies. Needs your D1/D2 ruling.
2. **Static bearer token in a `custom-keys` project connection.** Works today [V]. But it
   is a long-lived static credential in a shared connection — precisely the anti-pattern
   Lab 5 opens by condemning ("53% still ship long-lived API keys"). Usable if framed
   deliberately as the compromise it is; actively harmful if framed as the answer.
3. **Foundry custom OAuth against IdentityServer.** All the endpoints line up
   ([table below](#identityserver-oauth-mapping)) but it's per-user interactive consent
   [V] — thirty browser dances — and requires tenant match [V]. Not viable live.

> **Token expiry is a real mid-workshop hazard for option 2.** A static bearer has no
> refresh. IdentityServer's default access-token lifetime is short; the lab *will* break
> mid-session unless the realm's lifetime is raised for the day.

<a id="b3"></a>
### B3 — The inline `headers` field is rejected by the service · **severity: medium**

The field exists in the REST schema and all three SDKs, and Learn's own Responses API
example uses it — but two open Azure GitHub issues report the Agent Service refusing it
[C]: *"Headers that can include sensitive information are not allowed in the headers
property for MCP tools. Use project_connection_id instead."* No Microsoft response, nothing
on Learn. Whether it's a deny-list or blanket is **[?]**. **Workaround:** use project
connections exclusively — which Phase 1 does anyway for the URL-swap reason. Note the
documented consequence: "people who have access to the project can access an API key stored
in a project connection" [V].

<a id="b4"></a>
### B4 — Dev Proxy can't sit in the request path any more · **severity: high, needs D3**

Lab 4 drives fault injection with `curl -x http://127.0.0.1:8000`. A cloud agent cannot be
pointed at a forward proxy inside the Codespace. Candidate fixes, best first:

1. **Point the Dev Tunnel at Dev Proxy instead of at the app** — tunnel port 8000, have Dev
   Proxy forward to 5197, so injected faults land on the Foundry→server path. Needs
   verification that Dev Proxy will operate as a reverse proxy in that topology; I have
   **not** verified this and it's on the spike list.
2. **Keep Lab 4 local**, driving faults through Inspector/curl as today, and treat Foundry
   as the Lab 1/5/6 client only. Cheapest, but breaks "exclusively Foundry".
3. **Move fault injection in-process** (middleware behind a config flag). Most reliable,
   but loses Dev Proxy, which is a named part of the locked stack in AGENTS.md.

<a id="b5"></a>
### B5 — Foundry's `traceparent` is unsampled; default OTel drops the span · **severity: high, easy fix**

Set `OTEL_TRACES_SAMPLER=always_on` on the MCP server [V]. Without it, Labs 3/4/6 show an
empty trace view and the whole observability arc collapses. One line, but non-negotiable.

<a id="b6"></a>
### B6 — Foundry traces are App-Insights-only · **severity: medium**

No OTLP export for server-side Foundry tracing [V]. Locally the Aspire dashboard shows only
our half. **Workaround:** accept two half-traces in Labs 3–5 and teach the reason; in Lab 6
point the MCP server at the same App Insights resource so the transaction view joins them
[V]. Tag spans with `mcp.session.id` as a sampling-proof join key [V].

<a id="b7"></a>
### B7 — The 100-second MCP timeout breaks Lab 2's breakpoint exercise · **severity: medium**

Lab 2 has attendees pause on a breakpoint while a client waits, and even notes "if you sit
on a breakpoint for minutes, the agent gives up". With Foundry that's now a hard **100s**
[V] and the run fails server-side. **Workaround:** drive the breakpoint exercise from
Inspector (deterministic, no model, no timeout) and use Foundry to *demonstrate* the
timeout deliberately — it's a better "reality bites" moment than the current note.

<a id="b8"></a>
### B8 — Lab 4's nullable tool parameter may trip schema validation · **severity: low**

Foundry rejects tool schemas with `anyOf`/`allOf` or multi-type params [V]. `src/` is clean
today (no nullable tool params — tools already take `ClaimsPrincipal`), but Lab 4
introduces `string? customerId = null`, which may serialise as `"type": ["string","null"]`.
**Workaround:** verify the generated schema; if it breaks, use a non-nullable `string` with
a sentinel default. Lab 5 deletes the parameter anyway.

<a id="b9"></a>
### B9 — The Dev Tunnel URL can't be predicted · **severity: low**

Custom tunnel ID ≠ custom URL [V]. **Workaround:** attendee creates the tunnel first, then
passes `--mcp-url`; the script never guesses. This is exactly why `--mcp-url` is a
parameter and why the connection-based update path matters.

<a id="b10"></a>
### B10 — The devcontainer has neither `devtunnel` nor port 5197 · **severity: low**

`.devcontainer/devcontainer.json` installs azure-cli, node and github-cli, and forwards
5000/5001/18888 — **not 5197**, the port every lab actually uses. Lab 2 currently installs
the Dev Tunnels CLI by curl at runtime. Phase 1 will *propose* (not apply) adding a
devtunnel install and the missing ports.

<a id="b11"></a>
### B11 — `az` has no first-class Foundry agent commands · **severity: medium, affects Phase 1 shape**

Agent and connection creation is documented via REST and `azd ai connection create` [V]; I
found no `az` CLI equivalent for agent CRUD. **Consequence:** `deploy.sh` will use
`az rest` against the data plane with `api-version=v1` for the agent and connection, and
`az cognitiveservices` for the resource and deployment. That's fine but makes idempotency
hand-rolled (GET-then-PUT/PATCH) rather than ARM-declarative. Flagging because it changes
how much of the script I need to write defensively.

---

## What I could not verify

Listed so nothing here is mistaken for settled:

- Which quota tier a fresh free trial actually receives — **the decisive open question**.
- Whether SSE transport is accepted or rejected by Foundry's MCP client.
- Whether the inline `headers` rejection is a deny-list or blanket.
- Whether connection headers are sent on `initialize`/`tools/list` or only `tools/call`.
- Whether `devtunnel show --json` is a supported, stable interface.
- Whether `devtunnel` is officially supported inside Codespaces.
- Whether Foundry propagates `traceparent` consistently (one community capture, one request
  type, no Microsoft commitment — it could change without notice).
- Foundry tool-count and payload-size limits (the 80-tool cap you'll find online is **Azure
  SRE Agent**, a different product — don't cite it).
- The exact portal playground deep-link format that `deploy.sh` must print.
- Whether Dev Proxy can act as a reverse proxy for the B4 workaround.

Separately, these **live Microsoft doc bugs** should stay out of any lab: the
`azure-ai-agents` README's `enable_telemetry` import (raises `ImportError`), and the
`Azure.AI.Projects.Agents` 2.0.0 README's content-recording snippet (passes `false`).

---

## Empirical checks still outstanding

**Decided not to run these up front** (D-section above) — they will be settled by running
the labs end to end. Listed here so each one is a known open item rather than a surprise,
roughly in order of how much damage a negative result does.

1. **Throwaway free trial.** Register `Microsoft.CognitiveServices`, create an `AIServices`
   account (`--sku S0`) in `eastus2`, query the `quotaTiers` API, attempt a `gpt-4.1-mini`
   GlobalStandard deployment. **This single run resolves B1.**
2. **No-auth happy path.** ASP.NET Core server behind a Dev Tunnel, agent with
   `require_approval: "never"`, no auth. Confirms transport, path and Streamable HTTP in
   one shot — and lets you capture Foundry's real outbound headers to confirm or kill the
   `traceparent` finding.
3. ~~**Connection override.** Add a `custom-keys` connection; confirm its `--target` really
   does override `server_url`. **This validates the entire URL-swap design.**~~
   **Done 2026-10-05 — confirmed for routing, with a caveat on error messages. See the
   amendment under "Changing the URL later".**
4. ~~**Rotate the tunnel**, change only the connection target, re-run. If the agent needs no
   edit, the workshop's biggest operational risk is gone.~~
   **Done 2026-10-05 — rotation works, but the agent *does* need an edit to keep its error
   messages honest. `deploy.sh` now updates both and prunes old versions.**
5. **Dev Proxy reverse-proxy topology** (B4 option 1) — does it work at all?

---

*Phase 0 complete; D1–D4 resolved. Phase 1 (provisioning script) proceeds on these
findings. Phase 2 (lab impact) follows.*
