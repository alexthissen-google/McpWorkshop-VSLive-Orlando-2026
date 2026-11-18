# Storyline Overview — Crossing the Chasm: From Demo to Production

> The master narrative for the workshop. Read this before any individual chapter.
> Audience-facing chapters live alongside this file as `00`–`08`.

## The one-sentence story

> *Anyone can make an MCP server light up in MCP Inspector. Almost nobody's first one
> survives a real client, a real LLM, real latency, real users, and a real cloud.
> Today we cross that chasm — together, one gate at a time.*

## Why this story

We researched what actually bites developers building MCP servers in .NET. The same
chasm shows up everywhere: a server that works in a local inspector falls over the moment
it meets reality. The five recurring failure themes became our six gates.

| What developers hit in the wild | The gate that answers it |
|---|---|
| Works in MCP Inspector, breaks with a real client (transport, stdout, SDK mismatch) | **Gate 1 — Make It Work** |
| Non-deterministic agents are invisible; you can't see why a tool chain failed | **Gate 2 — Make It Debuggable** + **Gate 3 — Make It Observable** |
| No timeout budgets, no retries; transient 429s/latency cause cascading failures | **Gate 4 — Make It Resilient** |
| 53% still ship long-lived API keys; OAuth 2.1 is a genuine nightmare | **Gate 5 — Make It Secure** |
| "It runs locally" — now what? Unclear Azure hosting & deploy story | **Gate 6 — Make It Deployable** |

## The emotional arc

The through-line is **confidence**. Attendees arrive able to *make a demo*. They leave
knowing the difference between a demo and a system that's on call at 2am — and having
personally pushed one across that line. Every chapter ends with a visible "gate closed"
moment so progress is felt, not just told.

## The hero of the story: one demo app

A **Travel Booking MCP server** carries the entire day. It is deliberately ordinary in
domain but **stateful**, which makes its failures interesting:

- **Tools:** `SearchFlights`, `GetFlightDetails`, `HoldSeat`, `ConfirmBooking`,
  `GetBooking`, `CancelBooking`.
- **State that bites:** a seat **hold expires**. Hold → (latency/outage injected) →
  confirm-too-late → cascade. This single mechanic feeds the tracing, resilience, and
  idempotency lessons without any contrived examples.
- **Backed by** PostgreSQL via EF Core, in a container that .NET Aspire starts, migrated
  and seeded with fake flights at startup, so there's no database to set up in Codespaces.

The domain, the database and a REST API are pre-built and tested. In Chapter 2 attendees add
the MCP layer on top, and the *same* codebase is hardened gate by gate until it deploys to
Azure in Chapter 7. Two clients drive it all day: **MCP Inspector** as the deterministic
protocol test harness, and an **Azure AI Foundry agent** as the real LLM client, reaching
the Codespace over a public forwarded port.

## The 8-hour map

The communicated schedule for the day, 8:00–17:00. Each gate = concept framing → live demo →
hands-on lab → "gate closed" recap; every lab takes about 45 minutes.

| Time | Block | Chapters | Labs |
|------|-------|----------|------|
| 8:00 – 8:15 | Kick-off and introductions | 00, 01 | — |
| 8:15 – 10:00 | Setup, then Make It Work | 02 | Lab 0 + Lab 1 |
| 10:00 – 10:15 | *Morning coffee break* | — | — |
| 10:15 – 12:00 | Make It Debuggable, Make It Observable | 03, 04 | Lab 2 + Lab 3 |
| 12:00 – 1:00 | *Lunch* | — | — |
| 1:30 – 3:00 | Make It Resilient, Make It Secure | 05, 06 | Lab 4 + Lab 5 |
| 3:00 – 3:15 | *Afternoon break* | — | — |
| 3:15 – 4:45 | Make It Deployable (Azure) | 07 | Lab 6 |
| 4:45 – 5:00 | Wrap-up and eval | 08 | — |

The day is hands-on first: two labs fill most of each double block, so the theory for a gate
is a short framing and demo before its lab. The foundations chapter is folded into the
kick-off and the opening of the first block, and the last block has the most room for theory.

## Recurring narrative devices

- **The Gate Board.** A simple six-gate checklist shown at the start and revisited at every
  recap. Closing a gate is the visible win of each chapter.
- **"Reality bites" moments.** Each gate opens with a scripted failure the attendees *see*
  happen to the demo app (a broken client call, an invisible failure, a timeout storm, a
  leaked token), creating the need before we teach the fix.
- **The same trace, getting richer.** Once OpenTelemetry lands in Chapter 4, every later
  chapter reads the *same* end-to-end trace — now showing retries, auth, and cloud hops.
  The trace becomes the spine that ties the distributed story together.

## How the gates build on each other

Work → Debuggable → Observable → Resilient → Secure → Deployable is not arbitrary. You
can't debug what doesn't run; you can't make resilient what you can't observe; you don't
deploy to the cloud what isn't secure. Each gate assumes the previous ones are closed, so
the codebase only ever moves forward.

## What attendees leave with

A running Travel Booking MCP server in **Azure Container Apps**, secured with OAuth 2.1,
emitting OpenTelemetry traces and metrics, resilient to transient failure — plus the
repo, the labs, and a **production-readiness checklist** they can apply to their own servers.

---
*Next: [`01-foundations.md`](./01-foundations.md). Chapter files are instructor-facing
narrative; the matching step-by-step labs live in `/labs`.*
