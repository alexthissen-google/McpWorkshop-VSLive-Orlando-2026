# Chapter 01 — Foundations: Agentic AI, LLMs/SLMs & MCP

**Gate:** — (sets up the whole journey) · **Time:** 8:00–8:15, with the kick-off · **Lab:** none (concept chapter)

## Story beat

Before we build anything, we agree on *what* we're building and *why it's hard*. We zoom
out from "an MCP server is a web API with a funny shape" to the real picture: a
non-deterministic agent, reasoning in a loop, reaching for our tool at a moment we don't
control. That single realization — **we don't own the caller** — reframes every gate that
follows.

## Learning objectives

By the end of this chapter attendees can:

- Explain agentic AI: the model-reasoning loop, tools, and why agents are non-deterministic.
- Distinguish Large vs Small Language Models and where each fits (cost, latency, on-device).
- Describe what MCP is, the problem it solves (the *M×N integration* problem), and its core
  primitives: **tools**, **resources**, **prompts**.
- Name the transports (stdio vs streamable HTTP) and when each is used.
- Place "our MCP server" correctly in the distributed picture: host → client → server →
  downstream systems.

## Why this matters (the framing)

Most developers meet MCP as "expose a function to Copilot." That mental model quietly
causes every downstream failure in the workshop: they assume one caller, one happy path,
deterministic behavior, and a trusted network. The whole day is about replacing that model
with reality. We plant that flag here.

## Key concepts / talking points

- **Agentic AI.** The model plans, calls a tool, reads the result, and decides the next
  step — a loop, not a request/response. The same prompt can produce different tool chains.
- **LLMs vs SLMs.** Trade-offs in capability, latency, cost, privacy, and on-device hosting.
  Why an agent platform might route between them — and why your tool must serve both.
- **The M×N problem.** Without a protocol, every model integrates with every tool by hand.
  MCP is the *USB-C of AI tooling*: one contract, many clients, many servers.
- **MCP primitives.** Tools (callable functions), resources (readable context), prompts
  (reusable templates). The workshop centers on **tools**; the demo server also ships one
  resource (the baggage policy) and one prompt (the booking workflow) so all three can be
  shown side by side — see `src/README.md`.
- **Transports.** `stdio` for local single-process tools; **streamable HTTP** for networked,
  multi-client servers — which is what we build, because production is networked.
- **The trust boundary.** The client and the LLM are *outside* our control. Inputs are
  model-generated. This is the seed of the security and resilience gates.

## Demo arc (instructor, live)

Show a real agent (GitHub Copilot in VS Code) calling an *existing* MCP server and narrate
the loop out loud: "it decided to call this tool, with these arguments it invented, and now
it's reasoning about the result." Then ask: *what could go wrong here?* — and let the room
list the very failures we'll engineer solutions for. Capture their list; revisit it in
Chapter 08.

## The Gate Board (introduce it here)

Reveal the six-gate board that structures the day. Make it clear: we will close these in
order, on one codebase, and each chapter ends by checking one off.

> ☐ Work ☐ Debuggable ☐ Observable ☐ Resilient ☐ Secure ☐ Deployable

## Instructor notes

- Keep this tight and energetic — attendees are itching to code. 45 minutes max.
- Resist deep model theory; this is a *systems* workshop, not an ML course.
- The "what could go wrong" exercise is the hook for the whole day. Don't skip it.

## Transition

> "Good. You know what an agent is and why it's an unreliable narrator. Let's give it a tool
> — and watch our first one work in the inspector and then *not* work with a real client.
> That gap is Gate 1."
