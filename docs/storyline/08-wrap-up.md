# Chapter 08 — Wrap-Up & Production Checklist

**Gate:** — (the whole board is closed) · **Time:** 4:45–5:00 · **Lab:** none

## Story beat — *the chasm, crossed*

We return to the list the room made back in Chapter 01 ("what could go wrong?") and check it
off, item by item — almost every fear became a gate we closed. The point lands on its own:
the distance between a demo and production isn't talent, it's a finite set of gates, and
they just walked through all six on real code.

## Learning objectives

- Recall the six gates as a reusable mental model for *any* MCP server, not just this one.
- Apply the production-readiness checklist to their own work.
- Know where to go next: advanced topics, the spec, and the .NET MCP ecosystem.

## The journey, recapped

> ☑ Work ☑ Debuggable ☑ Observable ☑ Resilient ☑ Secure ☑ Deployable

One codebase. An empty `Tools/` folder → a Travel Booking MCP server running in Azure Container Apps,
secured with OAuth 2.1, emitting OpenTelemetry traces, resilient to transient failure, and
callable by a real agent over the internet.

## The production-readiness checklist (the takeaway artifact)

Hand this out as the thing they keep:

- **Works:** real-client tested (not just Inspector); typed I/O schemas; strong tool
  descriptions; dedicated `/health`; clean transport config.
- **Debuggable:** can attach a debugger to any call; failures are loud (logged + proper MCP
  errors with actionable messages), never swallowed; can inspect raw traffic.
- **Observable:** OpenTelemetry traces + metrics; context propagation across boundaries;
  GenAI semantic conventions; p95/p99 and error-rate dashboards.
- **Resilient:** idempotent mutating tools; retries on transient faults only, bounded, and
  close to the resource; timeout budgets; honored cancellation; error messages a model can
  act on.
- **Secure:** OAuth 2.1 + PKCE (no long-lived keys); audience validation; state bound to
  session; strict redirect-URI validation; least-privilege scopes mapped to tools; identity
  taken from the token, never from the model's arguments.
- **Deployable:** deployed from the same model that runs it locally; health probes; only
  the right endpoints public; cloud telemetry; secrets injected as configuration, not baked
  into images; a real identity provider.

## Where to go next

- The MCP specification and its evolving authorization model.
- Going deeper on resources & prompts (the server ships one of each); sampling; advanced
  server capabilities.
- Making the *agent* trustworthy, not just the server: what it will discuss, and what it
  believes when a tool result looks like an instruction.
- Enterprise authz for MCP — an honestly unsolved space; watch it evolve.
- Publishing servers (NuGet / the Official MCP Registry) and the broader .NET MCP ecosystem.

## Close

> "Anyone can make an MCP server light up in an inspector. You can make one that survives
> production. That's the difference this workshop was about — and you've got the repo, the
> labs, and the checklist to do it again on Monday."

## Instructor notes

- Keep it short and triumphant; resist new material this late.
- Circle back explicitly to the Chapter 01 fears list — that callback is the emotional close.
- Point attendees at the repo, the checklist, and a feedback channel.
