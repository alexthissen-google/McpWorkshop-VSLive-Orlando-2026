# labs — Hands-on labs

Step-by-step, attendee-facing lab guides. Each lab corresponds to a storyline chapter in
[`../docs/storyline/`](../docs/storyline/) and ends with a closed gate.

The workshop day has **four blocks** (two before lunch, two after), each about 45 minutes
of lab time per lab plus the theory that frames it. The labs map onto the blocks like this:

| Time | Block | Labs | Gate(s) |
|------|-------|------|---------|
| 8:00 – 8:15 | Kick-off and introductions | — | — |
| 8:15 – 10:00 | Morning 1 | [Lab 0 — Setup](lab-00-setup.md) + [Lab 1 — Make It Work](lab-01-make-it-work.md) | 1 |
| 10:00 – 10:15 | Morning coffee break | — | — |
| 10:15 – 12:00 | Morning 2 (theory + labs) | [Lab 2 — Make It Debuggable](lab-02-make-it-debuggable.md) + [Lab 3 — Make It Observable](lab-03-make-it-observable.md) | 2 + 3 |
| 12:00 – 1:00 | Lunch | — | — |
| 1:30 – 3:00 | Afternoon 1 (theory + labs) | [Lab 4 — Make It Resilient](lab-04-make-it-resilient.md) + [Lab 5 — Make It Secure](lab-05-make-it-secure.md) | 4 + 5 |
| 3:00 – 3:15 | Afternoon break | — | — |
| 3:15 – 4:45 | Afternoon 2 (theory + lab) | [Lab 6 — Make It Deployable](lab-06-make-it-deployable.md) | 6 |
| 4:45 – 5:00 | Wrap-up and eval | — | — |

Every lab is self-contained: all code is included as copy-paste blocks. Labs 1–6 each
assume the previous lab is finished, and `./labs/prepare-lab.sh N` puts the source tree
exactly where Lab N starts, so you can catch up at any point; `./labs/prepare-lab.sh check N`
diffs your tree against the finished state of Lab N. The committed code is the
finished solution; `./labs/prepare-lab.sh solution` restores it (see
[`assets/prepare/`](assets/prepare/README.md) for how this works).

| Lab | Chapter | Gate | Theme |
|-----|---------|------|-------|
| [Lab 0](lab-00-setup.md) | 00 | — | Setup — Codespace tour, and provision your Azure AI agent |
| [Lab 1](lab-01-make-it-work.md) | 02 | 1 | Make It Work — first tools, Inspector, then a real LLM client |
| [Lab 2](lab-02-make-it-debuggable.md) | 03 | 2 | Make It Debuggable — breakpoints, loud failures, wire logging |
| [Lab 3](lab-03-make-it-observable.md) | 04 | 3 | Make It Observable — OpenTelemetry + Aspire |
| [Lab 4](lab-04-make-it-resilient.md) | 05 | 4 | Make It Resilient — Dev Proxy, retries, idempotency |
| [Lab 5](lab-05-make-it-secure.md) | 06 | 5 | Make It Secure — OAuth 2.1 + PKCE |
| [Lab 6](lab-06-make-it-deployable.md) | 07 | 6 | Make It Deployable — Azure Container Apps |

Assets used by the labs (Dev Proxy configuration, etc.) live in [`assets/`](assets/).

Coming back from a break? The **[cheat sheet](cheat-sheet.md)** has the commands that get
a restarted Codespace back to where you were: ports, Aspire, the Inspector, the agent.

## The two clients

From Lab 0 onward you work with two MCP clients, and keeping them straight will save you
a lot of confusion:

- **An Azure AI Foundry agent**, which you chat with in the Foundry portal playground. A
  real LLM client running in Azure: it picks its own tools and makes up its own arguments.
  Provisioned by [`infra/attendee/deploy.sh`](../infra/attendee/README.md), and it reaches
  your Codespace over a published port.
- **MCP Inspector**, a protocol test harness you run locally. Deterministic, patient, and
  it shows you the raw JSON-RPC. Use it to debug; use the agent to verify.

A rule of thumb for the whole day: **if a problem is mechanical, reproduce it in the
Inspector. If it only happens with the agent, the problem is the prompt surface** — your
tool names, descriptions and error messages.
