# ADR 0001 — Clean Architecture with six layer projects

**Status:** Accepted
**Date:** 2026-06-10

## Context

The demo app is hardened across six gates (Work → Deployable). Each gate touches a
different concern — logging, tracing, resilience, auth, deployment — and the labs must
be teachable independently. If every concern threads through Domain code, attendees
spend the day refactoring instead of learning the pattern of each gate.

We also need both a REST API (for testing the domain before MCP exists) and an MCP
server (Lab 1 onwards) sitting on the same business logic.

## Decision

Use Clean Architecture with six layer projects under `/src`:

```
TravelBooking.Domain          ← rich entities (rules live here), service interfaces, zero dependencies
TravelBooking.Application     ← use-case handlers, depends on Domain only
TravelBooking.Infrastructure  ← EF Core, DbContext, service implementations
TravelBooking.API             ← REST endpoints (Application client)
TravelBooking.MCP             ← MCP tools (Application client, Lab 1)
TravelBooking.Host            ← composition root, runs API + MCP
```

Around them sit four more projects, ten in all: `TravelBooking.IdentityServer` under
`/src` (the workshop identity provider for Lab 5; it references none of the layers),
`TravelBooking.AppHost` (Aspire orchestrator) and `TravelBooking.ServiceDefaults`
(shared OTEL / health / resilience, the Aspire convention) under `/src/aspire`, and
one unified test project under `/src/tests`.

## Consequences

- ✅ Each gate touches a specific layer. Logging (Lab 2) lives in MCP + Host;
  auth (Lab 5) lives in Host, MCP and API, with the ownership rule in Domain;
  deployment (Lab 6) lives in AppHost.
- ✅ API and MCP are peers — both call Application. No duplication.
- ✅ Domain is portable; an attendee who later wants to swap EF Core for Dapper
  only touches Infrastructure.
- ⚠️ Ten projects is heavier than a single-project demo. Mitigated because the
  scaffold is pre-built; attendees don't create projects.
- ⚠️ Project references can confuse beginners — `Host` references `API`, `MCP`,
  `Infrastructure` and `ServiceDefaults`: as composition root it is the only
  project that sees Infrastructure. `API` and `MCP` reference `Application` only.
  Documented in [ARCHITECTURE.md](../../ARCHITECTURE.md).

## Alternatives considered

- **Single web project** — too much coupling; impossible to teach layered concerns.
- **Vertical slices** — better for a maintainable production app, worse for a
  teaching app where the *layers themselves* are the lesson.
