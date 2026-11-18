# Chapter 04 — Make It Observable (Gate 3)

**Gate:** 3 — Make It Observable · **Time:** 10:15–12:00, shared with Chapter 03 · **Lab:** Lab 3

## Story beat — *reality bites*

An agent makes three tool calls to fulfill one request, one of them fails intermittently,
and the user just sees a vague apology. A breakpoint can't help — you can't pause
production, and you can't predict which tool chain the model will choose. You need to see
the *whole journey* of a request after the fact: prompt → tool call → downstream API →
result. That's distributed tracing, and it's the spine of the rest of the day.

## Learning objectives

- Add **OpenTelemetry** tracing and metrics to the ASP.NET Core MCP server.
- Understand **context propagation** (W3C Trace Context) across agent → server → downstream.
- View end-to-end traces and metrics locally in the **.NET Aspire dashboard**.
- Apply **GenAI semantic conventions** so tool calls are legible in any OTel backend.
- Read a trace to diagnose a non-deterministic, multi-tool failure.

## The pain point (grounded in research)

Agentic systems are non-deterministic and *invisible*: the same prompt triggers different
tool chains, and failures hide across agent boundaries. The research is blunt — distributed
tracing "materially reduces debugging time by exposing end-to-end tool invocations,
reasoning steps, and downstream failures in a single trace." Without it, production MCP is
undebuggable.

## Key concepts / talking points

- **Why OTel fits MCP.** Same open-standards philosophy; vendor-neutral; one instrumentation,
  any backend.
- **Traces vs metrics vs logs.** Traces answer "what happened to *this* request"; metrics
  answer "how's the system trending" (p95/p99 latency, error rate, tool-call volume).
- **Context propagation.** When the agent calls the tool, trace headers flow in; the server
  continues the trace; downstream calls extend it — one connected story across processes.
- **GenAI semantic conventions.** Standard attribute names for tool name, arguments, tokens,
  model — so traces are comparable and tooling understands them.
- **The Aspire dashboard.** Zero-backend local trace/metric viewing inside Codespaces — see
  the seat-hold flow as a waterfall.

## Demo arc (instructor, live)

1. Add OpenTelemetry to the server; light up the Aspire dashboard.
2. Drive a multi-tool request (`SearchFlights` → `HoldSeat` → `ConfirmBooking`); show the
   single connected trace as a waterfall.
3. Introduce an intermittent downstream slowdown; find the culprit span *in the trace*,
   not by guessing.
4. The "aha": *the trace is the new breakpoint — and it works in production.*

## The recurring trace (narrative device)

From here on, **every later chapter reads this same trace**, now richer: Chapter 05 adds
retry spans, Chapter 06 adds auth, Chapter 07 adds the cloud hop. The trace is how we keep
the distributed story coherent.

## Hands-on — Lab 3 (summary)

Attendees read their first end-to-end trace in the Aspire dashboard (the solution has had
OpenTelemetry wired in since Lab 0), add their own activity source, spans and counters to the
booking tools, find the agent's half of the request in Application Insights, and explain a
failing `HoldSeat` call from the trace alone.

## Gate closed ✅

> ☑ Work ☑ Debuggable ☑ **Observable** ☐ Resilient ☐ Secure ☐ Deployable
> *"You can now see what your server does in production, across process boundaries. So let's
> make it *behave* under production conditions — latency, transient errors, and outages."*

## Instructor notes

- Aspire dashboard keeps this backend-free; avoid sending people to set up a full APM.
- Keep one canonical trace on screen the whole afternoon; refer back to it constantly.
