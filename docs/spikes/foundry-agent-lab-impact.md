# Lab impact — Foundry agent as the only client

> **Phase 2. Proposal only — no lab files have been edited.**
> Companion to [`foundry-agent-attendee-access.md`](./foundry-agent-attendee-access.md),
> which carries the research these changes rest on. Blocker IDs below (B1–B11)
> refer to that document.

Two decisions drive everything here:

1. **The Foundry agent replaces GitHub Copilot as the LLM client.** MCP Inspector
   stays as the protocol-level harness (D2).
2. **Attendees expose their server through Codespaces port forwarding, not Dev
   Tunnels** — Codespaces forwarding is built on the Dev Tunnels service anyway,
   so the CLI only added a login and an install step.

The second one reshapes the original "move Dev Tunnels to Lab 0/1" instruction:
what moves to Lab 0 is **making port 5197 public**, and Lab 2 loses its Dev
Tunnels section entirely — including, painfully, its traffic inspector.

---

## Summary of per-lab change

| Lab | Scale of change | The load-bearing bit |
|---|---|---|
| **0 — Setup** | **Large (additive)** | Azure provisioning moves here, so quota failures surface at 09:15 not 11:30 |
| **1 — Make It Work** | **Large** | Copilot section replaced by the Foundry playground; both failure drills survive and improve |
| **2 — Make It Debuggable** | **Large** | Dev Tunnels section deleted; needs a replacement for the lost traffic inspector |
| **3 — Make It Observable** | **Medium** | One mandatory env var, or the entire lab shows an empty dashboard |
| **4 — Make It Resilient** | **Large, highest risk** | Dev Proxy cannot sit in an inbound path; D3 needs revisiting |
| **5 — Make It Secure** | **Medium–large** | Foundry breaks when the server is secured; needs a deliberate answer |
| **6 — Make It Deployable** | **Small, improves** | Client switch becomes one command instead of editing JSON |

---

## Lab 0 — Preparing your environment

Currently ends at "the REST API works". **Proposal: it should end at "Azure is
ready too".**

The argument is operational, not pedagogical. Provisioning is the one step that
can fail for reasons no attendee can fix in the moment — a free-trial
subscription refusing to deploy a model (B1), a provider stuck registering, a
guest who never redeemed an invitation. Discovering that at 11:30 during Lab 1
costs the room its momentum. Discovering it at 09:15, while the proctors are
fresh and the fallback slots are warm, costs one person ten minutes.

**Add, after the existing REST smoke test:**

1. Make the port reachable from Azure:
   ```bash
   gh codespace ports visibility 5197:public -c "$CODESPACE_NAME"
   ```
   Worth one sentence on *why*: the agent runs in Azure and cannot see a port
   that is private to your Codespace.

2. Sign in and provision:
   ```bash
   az login --use-device-code
   ./infra/attendee/deploy.sh --mcp-url "https://${CODESPACE_NAME}-5197.app.github.dev/mcp"
   ```

3. Open the playground link the script prints. **Expect the agent to have no
   tools yet** — `src/TravelBooking.MCP/Tools/` is empty on purpose, and `/mcp`
   is not mapped until Lab 1. Say so explicitly, or the first thing attendees
   will do is report a bug.

**Extend the checkpoint** with: port 5197 shows **Public** in the Ports panel;
`deploy.sh` finished and printed a playground link; the link opens a Foundry
playground.

> **Instructor note to add:** anyone whose provisioning fails here goes to a
> fallback slot *now*, not later. The [fallback procedure](#fallback-procedure)
> is a two-minute operation if started early.

---

## Lab 1 — Make It Work

The tool-writing and Inspector halves are unaffected. The client half changes
completely, and improves.

**Replace "Connect GitHub Copilot" with "Meet your agent".** Delete the
`.vscode/mcp.json` creation, the `MCP: List Servers` steps and the tools-icon
screenshot. In their place: re-run `deploy.sh` (or nothing at all, if the URL is
unchanged — it's idempotent), open the playground, ask the travel question.

Keep the Inspector section exactly as it is. It is now the *first* thing that
proves the server works, before any model is involved, which is a cleaner
teaching order than it was.

**Approval prompts stay ON here** (D4). The playground will ask before each tool
call. That is worth a sentence — it mirrors the approval UX attendees have seen
in Copilot and will meet in any agent platform, and it makes the tool chain
visible step by step the first time they see it.

**Both "break it on purpose" drills survive, and one gets better:**

- *Failure 1 — endpoint mismatch.* Currently edits `.vscode/mcp.json`. Now it
  becomes `./deploy.sh --mcp-url https://.../api/mcp`, wait, watch the agent
  fail, then put it back. That round-trip costs ~40 seconds of script time
  twice. **Recommendation: demonstrate this in the Inspector instead** (instant,
  same lesson) and mention in prose that the same mistake in `--mcp-url`
  produces the same silence. Keeps the lesson, saves four minutes across the
  room.
- *Failure 2 — the invisible tool.* **Strictly better with Foundry.** It was
  always the stronger drill, and now it runs against the actual client the
  workshop is built around rather than a side quest.

**Adjust the CORS rationale.** The current text justifies CORS with "real IDE
clients make cross-origin calls". Foundry is server-to-server and does not care
about CORS at all. The policy is still needed — for the browser-based Inspector
— so keep the code and fix the sentence, otherwise an attentive attendee will
catch the contradiction.

**Add a note on description length:** Foundry caps tool and function
descriptions at **1,024 characters**. Our current descriptions are well under,
but attendees who get enthusiastic in the "descriptions are prompt surface"
section can trip it.

---

## Lab 2 — Make It Debuggable

The most disrupted lab, because two of its four sections depended on Dev Tunnels.

**Delete the "Expose your server with Dev Tunnels" section.** That beat now
happens in Lab 0. Replace it with two or three sentences recalling that the
server is already public, and that the agent has been reaching it across the
internet since Lab 1.

**The "Watch the raw traffic" section loses its tool.** The Dev Tunnels inspect
UI has no Codespaces equivalent. This is a genuine loss and needs a replacement,
because "see the actual JSON-RPC on the wire" is the heart of this gate.

Three candidates:

| Option | Verdict |
|---|---|
| MCP Inspector's own request log | Free, but only shows *Inspector's* traffic — not the agent's. Half the lesson. |
| **Request-logging middleware on `/mcp`** | **Recommended.** ~15 lines, logs land in Aspire structured logs, survives the move to ACA in Lab 6. |
| Dev Proxy in record mode | Same inbound-path problem as Lab 4 (see below). |

The middleware option is not a consolation prize — it is **better than the
inspect UI was**, for one specific reason: it lets attendees see the headers
Foundry actually sends. Two of those matter later today:

```
traceparent: 00-<trace-id>-<span-id>-00
Mcp-Session-Id: <session>
```

That trailing **`-00`** is the sampled flag, and it is why Lab 3 needs
`OTEL_TRACES_SAMPLER=always_on` (B5). Seeing the header *before* the trace lab
turns an arbitrary-looking env var into an obvious fix for a problem they have
personally observed. `Mcp-Session-Id` becomes Lab 3's correlation key. I'd make
this an explicit, named moment.

**Rework the debugger exercise around the 100-second timeout (B7).** The lab
currently has attendees sit on a breakpoint while a client waits, and notes that
"real clients have timeouts". With Foundry that is now a hard **100s** and the
run fails server-side.

Proposal: *drive the breakpoint from the Inspector* — deterministic, no model,
no timeout, you can take as long as you like. Then, deliberately, repeat it from
the playground and let the agent time out. The current soft note becomes a
"reality bites" moment the attendee watches happen, which is exactly this
workshop's narrative device.

**Switch approvals off at the end of this lab** (D4):
```bash
./deploy.sh --mcp-url "https://${CODESPACE_NAME}-5197.app.github.dev/mcp" --approval never
```

---

## Lab 3 — Make It Observable

Mostly intact. One addition is mandatory and one is strongly recommended.

**Mandatory — `OTEL_TRACES_SAMPLER=always_on` (B5).** Foundry sends
`traceparent` with the sampled flag **off**. OpenTelemetry's default
`ParentBased` sampler drops a span whose remote parent was not sampled, so a
default-configured server **throws away every Foundry-initiated trace**. The
attendee opens the dashboard, sees nothing, and concludes their instrumentation
is broken.

This must land *before* the first "open the dashboard" instruction. Suggested
home is the AppHost, so it travels with the orchestration:

```csharp
builder.AddProject<Projects.TravelBooking_Host>("travelbooking-host")
    .WithEnvironment("OTEL_TRACES_SAMPLER", "always_on")
```

> This is a `src/` change and needs your sign-off before anyone writes it.

**Recommended — subscribe to the MCP SDK's own source.** One line in
ServiceDefaults next to the existing `AddSource` calls:

```csharp
.AddSource("Experimental.ModelContextProtocol")
```

It costs nothing and sets up the honest version of the propagation story below.

**Replace the Copilot-driven flow** with the same prompt in the playground.

**Teach the half-trace rather than hiding it (B6).** Foundry's own traces go to
Application Insights and *only* there — there is no documented way to point
server-side Foundry tracing at an OTLP endpoint. Locally, the Aspire dashboard
can therefore never show Foundry's half. Attendees will notice.

The honest framing is also the most interesting one, and it is a genuinely
advanced lesson most MCP material never reaches:

- MCP standardised trace propagation in **SEP-414, status Final** —
  `traceparent` goes in `params._meta`.
- The **C# SDK already implements both halves** (it extracts parent context from
  `_meta` on the server and injects it on the client).
- **Foundry's hosted client doesn't send it.** It sends an HTTP header instead,
  which ASP.NET Core picks up, so the trace ID *does* cross — but unsampled.

A short exercise — dump the incoming headers and `params._meta` from the Lab 2
middleware — makes all three visible in about four minutes. It also degrades
gracefully: if Microsoft changes the behaviour before November, the exercise
still works and simply shows something different.

**Add `mcp.session.id` as a span tag.** It is a Recommended attribute in the
GenAI semantic conventions, Foundry reliably sends the header, and it survives
sampling mismatches — making it the robust join key when trace parenting fails.

---

## Lab 4 — Make It Resilient

**The highest-risk lab, and I want to revise the D3 ruling.**

D3 chose "tunnel → Dev Proxy → 5197" so injected faults would land on the
Foundry→server path. Having thought harder about Dev Proxy's architecture, my
confidence in that has dropped and I'd rather say so now than have you discover
it mid-run.

Dev Proxy is a **forward** proxy: it works because the *client* is configured to
route through it (`curl -x http://127.0.0.1:8000`). Foundry will not do that —
it makes a direct HTTPS call to a public URL. For Dev Proxy to sit in the
inbound path it would have to act as a **reverse** proxy, which I could not
verify it supports. The whole of Lab 4's current mechanism — every `curl -x` in
the lab — depends on the forward-proxy model.

Three ways out, and I no longer recommend the one we picked:

**Option 1 — Keep fault injection client-side** *(now my recommendation)*.
Lab 4 keeps Dev Proxy and `curl -x` exactly as written, driving the server from
inside the Codespace. The Foundry agent is used only to show the *consequence*:
hold a seat through the latency proxy, let the hold expire, then ask the agent
to confirm and watch it fail with a clean domain error. Changes almost nothing,
keeps Dev Proxy (which AGENTS.md locks into the stack), and the agent still
earns its place in the lab.
*Cost:* the injected faults are not on the agent's path, so this is slightly
less "real" than the current lab was.

**Option 2 — In-process fault injection middleware.** A config-flagged
middleware that adds latency or returns 429/503 on `/mcp`. Fully reliable, puts
faults on the agent's real path, and works unchanged in Lab 6's cloud deploy —
where Dev Proxy could never have run anyway.
*Cost:* displaces Dev Proxy, which is a **locked stack decision in AGENTS.md** and
therefore your call, not mine. Could be positioned as "Dev Proxy for outbound,
middleware for inbound" so both survive.

**Option 3 — `socat` in front of 5197.** Your `post-create.sh` already installs
`socat`. Publish 8000, chain `socat` → Dev Proxy → 5197.
*Cost:* fragile, three moving parts, and a long debugging detour when it breaks
in front of 30 people. I'd avoid it.

**Recommendation: Option 1 now, Option 2 as a stretch goal**, and verify the Dev
Proxy reverse-proxy question when you run the lab. If it turns out Dev Proxy
*can* reverse-proxy, Option 1 upgrades to the original D3 plan with no rewrite —
only the topology sentence changes.

**Independently — the nullable-parameter risk (B8).** Lab 4 adds:

```csharp
string? customerId = null
```

Foundry rejects tool schemas containing `anyOf`/`allOf` or multi-type
parameters, and a nullable C# parameter may serialise as
`"type": ["string","null"]`. If it does, **every tool call in Labs 4 and 5 fails
with "Invalid tool schema"** — a spectacular failure right in the middle of the
day.

Mitigation is cheap: use a non-nullable `string customerId = ""` and treat empty
as absent. Lab 5 deletes the parameter anyway, so the sentinel lives for one
lab. **Worth checking the generated schema before November regardless** — it is
a five-minute check against a high-cost failure.

---

## Lab 5 — Make It Secure

Per D1, **MCP Inspector carries the OAuth discovery lesson.** The substantive
change is what happens to the *agent* once the server is locked down.

**The moment the endpoint requires a token, the Foundry agent breaks.** Foundry
is not a spec-compliant MCP OAuth client — it ignores the 401, the
`WWW-Authenticate` header and the protected-resource metadata document. That is
not a bug in the lab; it is a real gap in a shipping Microsoft product.

### The sequence I'd propose

1. **Secure the server** — unchanged from the current lab.
2. **Watch the agent break.** Ask it to book a flight; it fails. One deliberate
   paragraph: *this is what "the client must support OAuth" actually means.*
3. **Prove the spec works — with Inspector.** The 401, the metadata document,
   the automatic PKCE flow, the login page, the scope-gated `HoldSeat`. The
   current lab's steps 1–3 survive almost verbatim; only "VS Code" becomes
   "Inspector".
4. **Get the agent working again** — see the options below.
5. **Identity in the audit trail** — unchanged.

Step 2 is the best teaching moment in the revised day. The workshop's thesis is
the gap between demo and production; here is a gap *in the tooling itself*,
discovered first-hand.

### Options for re-connecting the agent

| | Mechanism | Verdict |
|---|---|---|
| **A** | Static bearer token in a `CustomKeys` project connection | **Recommended** |
| **B** | Foundry custom OAuth against IdentityServer | Not viable — see below |
| **C** | Leave the agent unauthenticated; secure only for Inspector | Dishonest; Lab 6 inherits a lie |

**Option B is a hard no, for three independent reasons.** IdentityServer runs on
`localhost:5001` inside the Codespace, so Foundry could not reach its token or
authorization endpoints without publishing port 5001 too. Foundry's OAuth mode
requires per-user interactive consent — thirty browser dances. And it needs the
user's Entra tenant to match the project's, which is fine for self-service
attendees but adds a constraint for guests in fallback slots.

**Option A is what I'd write.** Mint a token inside the Codespace with the
existing resource-owner-password grant the lab already uses, and store it as a
connection credential:

```bash
TOKEN=$(curl -s http://localhost:5001/connect/token \
  -d grant_type=password -d client_id=mcp-inspector \
  -d username=alice -d password=alice123 \
  -d scope="openid mcp:tools booking:write" | jq -r .access_token)
```

Two things this needs, and **both are gaps in what I built in Phase 1**:

- **`deploy.sh` currently hardcodes `authType: "None"`.** It needs a
  `--mcp-token <jwt>` flag that switches the connection to `CustomKeys` with an
  `Authorization: Bearer <jwt>` credential. The ARM payload shape is already
  verified and documented in the Phase 0 spike; this is maybe 15 lines. **I have
  not written it** — say the word and I will.
- **Token lifetime must be raised for the workshop.** A default access-token
  lifetime will expire partway through Lab 5 or during Lab 6, and the failure
  looks exactly like a broken config. Set the `mcp-inspector` client's
  `AccessTokenLifetime` in `Config.cs` to cover the rest of the day (8 hours is
  ample). **This is a `src/` change needing your approval.**

And it needs honest framing, because **Option A is literally the anti-pattern
the lab opens by condemning** — "about half of deployed MCP servers still ship
with long-lived static API keys". Writing a long-lived static token into a
shared project connection and saying nothing would undercut the whole gate.
Said out loud, it is the sharpest point in the lab:

> *We just did the thing we spent forty minutes arguing against — because the
> client left us no choice. That is what a compliance gap costs you downstream:
> not a worse design on paper, a worse design in production. Note also that
> anyone with access to this Foundry project can read that token.*

That last sentence is documented, not editorial: *"people who have access to the
project can access an API key stored in a project connection."*

---

## Lab 6 — Make It Deployable

Least affected, and the client switch gets cleaner.

**Replace the `.vscode/mcp.json` second-server edit with one command:**

```bash
./deploy.sh --mcp-url https://travelbooking-host.<random>.<region>.azurecontainerapps.io/mcp
```

Same agent, now talking to Azure. The payoff lands harder than editing JSON did,
and it quietly demonstrates the connection indirection from Lab 0.

> **Amended after live testing (2026-10-05).** The URL change also rewrites the
> agent definition, so it publishes a new agent version and the playground link
> changes. Foundry quotes the definition's `server_url` in MCP error messages
> regardless of where the call actually went, so a stale definition makes every
> later failure name the wrong host. `deploy.sh` now updates both, prunes the
> superseded versions, and prints the new link. The lab says so explicitly.

**Mention that port 5197 can go private again.** Small hygiene beat that
reinforces what the public port was ever for.

**The trace story can actually close here (B6).** Lab 6 is the one place where
Foundry's traces and ours can join, because both can land in the same
Application Insights resource — App Insights maps the W3C trace-id to
`operation_Id` and the transaction view joins them natively. ACA's managed
OpenTelemetry agent can fan out to App Insights *and* an OTLP endpoint
simultaneously, so the Aspire dashboard keeps working too.

Given the lab is already ~45 minutes with ~10 of waiting, I'd make this a
**stretch goal** rather than a required step — but it is the only point in the
day where the full end-to-end trace is achievable, so it deserves to be written
down even if most of the room doesn't reach it.

**Cleanup needs a second command:**
```bash
azd down
./infra/attendee/deploy.sh --delete
```

---

## Instructor runbook

### Pre-event checklist

**Do this first — it is currently blocking**

- [ ] 🔴 **Clear the `715-123420` anti-abuse gate on the instructor subscription.**
      Confirmed on 2026-10-05: the subscription is **Microsoft Azure Sponsorship**
      with an empty `quotaId`, the Foundry account and project create fine, and
      **model deployment is refused**. Because the fallback slots live in this
      same subscription, this blocks *both* the self-service and the fallback
      paths — the whole Azure side of the day.
      No CLI change fixes it. Raise a support request quoting `715-123420`, say
      it is an Azure AI Foundry chat model deployment for a training workshop,
      and mention that other Cognitive Services resources already deploy fine.
      Typically cleared in a day or two, but it is on the critical path, so
      raise it now rather than at T‑2 weeks. See
      [B1b](./foundry-agent-attendee-access.md#b1b).

**T‑4 weeks**

- [ ] Confirm the instructor subscription's quota tier:
      `az rest --method GET --url ".../providers/Microsoft.CognitiveServices/quotaTiers?api-version=2025-10-01-preview"`
- [ ] Register the nine resource providers on the instructor subscription.
- [ ] Decide slot count. **Rule of thumb: 20% of expected attendance**, minimum 3.
- [ ] Tell attendees what they need: an Azure subscription that is
      **pay-as-you-go, not a free trial** (B1). Give them the upgrade link and
      a deadline.

**T‑2 weeks**

- [ ] **Run one slot end to end with a real external email address.** This is
      the single highest-risk unknown in the plan: no Microsoft documentation
      says whether a subscription where the guest holds *only* an RG-scoped
      assignment appears in the Foundry portal's subscription picker. If it
      does not, slots need a subscription-scoped Reader and the isolation gets
      weaker — better to learn that now.
- [ ] Verify the playground deep link still resolves. It is undocumented and
      derived from `azd`'s source; Microsoft can change it without notice.
- [ ] Run `deploy.sh` on a *fresh* subscription to exercise the
      provider-registration path.
- [ ] Check the generated tool schemas for `anyOf`/multi-type params (B8).
- [ ] Verify whether Dev Proxy can reverse-proxy (settles Lab 4's shape).

**T‑1 week**

- [ ] Create the slots:
      `for n in 1 2 3; do ./deploy.sh --slot "$n" --mcp-url https://placeholder.invalid/mcp; done`
- [ ] Send guest invitations. **Chase redemptions** — an unredeemed guest cannot
      sign in and the error does not say why.
- [ ] Enable Codespaces **prebuilds** so first-start isn't 10 minutes of Lab 0.

**Morning of**

- [ ] `az group list -o table` — all slots present.
- [ ] Smoke-test one slot's playground end to end.
- [ ] Have the tenant-pinned link ready to paste: `https://ai.azure.com/?tid=<tenant>`
- [ ] Confirm `gh` is authenticated in a fresh Codespace (needed for
      `gh codespace ports visibility`).

### Fallback procedure

**Triggers** — any of: model deployment refused for quota; Foundry resource
creation refused; provider registration not completing; no usable Azure
subscription.

**Do this at Lab 0, not later.** It takes about two minutes.

```bash
./infra/attendee/invite.sh --slot <n> --email <attendee-email>
# then, once they have their Codespace URL:
./infra/attendee/deploy.sh --slot <n> --mcp-url https://<their-codespace>-5197.app.github.dev/mcp
```

The attendee opens `https://ai.azure.com/?tid=<your-tenant>`, selects your
directory, and works from there.

#### Everything downstream is NOT identical — the instructor drives the CLI

This is the part that is easy to get wrong on the day. A slot attendee **cannot run
`deploy.sh` themselves**:

- the labs' commands omit `--slot`, so the script would target a *personal* resource
  group derived from their own identity;
- their `Foundry User` role is scoped to the slot's resource group, so they cannot
  create one anyway; and
- they may have no Azure subscription selected in `az` at all.

So for every point in the day where a lab says "re-run `deploy.sh`", **the instructor
runs it instead**, with `--slot`. Three moments matter:

| Lab | Attendee instruction | Instructor runs |
|---|---|---|
| 1 | nothing (agent already points at their Codespace) | `./deploy.sh --slot N --mcp-url https://<their-cs>-5197.app.github.dev/mcp` |
| 2 | turn approvals off | `./deploy.sh --slot N --mcp-url <same> --approval never` |
| 5 | attach the bearer token | `./deploy.sh --slot N --mcp-url <same> --mcp-token "<their token>"` |

The attendee reads their token out of their own Codespace and passes it to you; it is
their IdentityServer, not yours.

Give each slot attendee the **playground link for their slot** (printed by `deploy.sh`)
rather than letting them navigate, since their directory will contain several projects
they cannot open.

#### Lab 6 for slot attendees

`azd up` needs a subscription the attendee can deploy into, which by definition they do
not have — a `Foundry User` role on one resource group does not permit creating a
Container Apps environment. Pick one before the event:

1. **Pair them up** with someone who has a working subscription. Cheapest, and Lab 6 is
   a good pairing exercise anyway.
2. **Grant `Contributor` on the slot resource group** and have them `azd up` into it.
   Costs you the ACA/ACR/Log Analytics spend (~$1–3/day each) and widens their access
   well beyond read-and-chat.
3. **Demo Lab 6 from the front** and have slot attendees follow along without deploying.
   Honest, and the lab is the shortest of the day.

Recommendation: **(1), falling back to (3)**. Option 2 undoes the least-privilege
property that makes slots safe to hand out.

**Pre-redeemed guests are much faster.** If someone flagged a shaky subscription
in advance, invite them a week early; on the day it is one `deploy.sh` call.

### Known mid-session failure modes

| Symptom | Cause | Fix |
|---|---|---|
| Agent worked, now fails after a break | Codespace restarted; **public port silently reverted to private** | `gh codespace ports visibility 5197:public -c "$CODESPACE_NAME"` |
| Agent times out on a long tool call | Foundry's **100s** non-streaming limit (B7) | Expected during the Lab 2 breakpoint drill; use Inspector for debugging |
| Sporadic 429s mid-morning | Tier 0 is **200 RPM**; ~150 requests when the room submits together | Stagger exercises; re-running `deploy.sh` is always safe |
| Empty Aspire trace view in Lab 3 | `OTEL_TRACES_SAMPLER` not set; Foundry's `-00` flag (B5) | The env var; check it before blaming instrumentation |
| Agent breaks right after Lab 5's auth step | Expected — Foundry can't do OAuth discovery | It's a teaching beat; re-connect per Lab 5 Option A |
| Agent breaks *later* in Lab 5/6 | Static bearer token **expired** | Raise `AccessTokenLifetime`; re-mint and re-run `deploy.sh --mcp-token` |
| "Invalid tool schema" | Nullable/multi-type param (B8) | Non-nullable with sentinel default |
| Guest can't sign in | Invitation never redeemed | Resend; no same-day fix if mail is slow |

### Cost estimate

**Per attendee (their own subscription) — well under $1 for the day.**

`gpt-4.1-mini` on Global Standard is **$0.40 / 1M input** and **$1.60 / 1M
output** (verified via the Azure Retail Prices API). A generous day is ~40 agent
turns, each chaining ~5 model calls at ~4K input / 400 output:

- ~800K input → **$0.32**
- ~80K output → **$0.13**
- **≈ $0.45 per attendee**

The Foundry resource and project have **no idle cost** — AIServices S0 is
pay-per-token, and the `--capacity 10` cap bounds the blast radius of a runaway
loop. Across 30 attendees the model spend is **~$13 total**, which is why
reliability rather than cost should drive any model decision.

**Lab 6 adds the only non-trivial cost**, on the attendee's own subscription:
a Container Apps environment (scales to zero), a container registry, and Log
Analytics. For a single day this is **roughly $1–3**, dominated by the registry
and log ingestion. **I have not verified these figures** against current pricing
— unlike the model numbers, treat them as an order-of-magnitude estimate, and
make sure `azd down` is actually run.

**Per fallback slot — the same ~$0.45**, plus the same Lab 6 costs **if** the
slot's occupant does Lab 6 on your subscription. Three idle slots created a week
early cost essentially nothing while unused. Budget **~$5 per slot** as a safe
upper bound including a full Lab 6, and tear them down the same day:

```bash
./infra/attendee/deploy.sh --delete --all-slots
```

---

## Decisions — resolved 2026-10-05 (Loek)

All four approved. Status of each:

1. **Lab 4 keeps fault injection client-side** (Option 1). D3 in the Phase 0
   spike is marked superseded. Option 2 (in-process middleware) stays available
   as a stretch goal, but it touches a locked AGENTS.md stack decision and is
   not assumed here.
2. **Enabling changes made** — see the commit that accompanies this revision:
   - `OTEL_TRACES_SAMPLER=always_on` on the Host, set in the AppHost. **Done.**
   - `AccessTokenLifetime = 28800` on the `mcp-inspector` client in
     IdentityServer's `Config.cs`. **Done.**
   - `string? customerId = null` → `string customerId = ""` in Lab 4, with the
     reason written into the lab. **Done.** (This one turned out to be a lab
     change, not a `src` change — `src/` is already at Lab-5 state and uses
     `ClaimsPrincipal`.)
3. **`deploy.sh --mcp-token`** — **Done.** Switches the project connection to
   `CustomKeys` with an `Authorization: Bearer …` credential. A bare re-run
   without the flag preserves an existing credential rather than silently
   dropping it; `--mcp-token ""` clears it deliberately.
4. **Lab 2's traffic-inspector replacement** — request-logging middleware,
   approved. **Not yet written**: it belongs in the lab rewrite rather than in
   pre-wired `src/`, since attendees should add it themselves the way they add
   their own spans in Lab 3.

### Still to do — the lab rewrite itself

Nothing under `labs/` has been rewritten yet beyond the `customerId` fix. The
per-lab sections above are the specification for that work.
