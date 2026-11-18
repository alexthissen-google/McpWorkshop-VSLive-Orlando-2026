# ADR 0005 — Records for DTOs, classes for EF entities

**Status:** Accepted
**Date:** 2026-06-10

## Context

C# offers both `record` and `class`. The two carry different intent: records
signal immutability and value equality; classes signal identity and mutable
state. Mixing them randomly muddies the codebase.

## Decision

Use `record` (typically positional) for:

- DTOs in Application handlers (`HoldResult`, `BookingResult`, `FlightSummary`)
- REST API request/response contracts (`HoldSeatRequest`, `ErrorResponse`)
- MCP tool request/response shapes (Lab 1)
- Value objects stored as JSON (`SeatMapEntry`)

Use `class` for:

- EF Core entities with mutable state and navigation properties
  (`Flight`, `Airport`, `SeatHold`, `Booking`, `AuditEntry`)

## Consequences

- ✅ Records get value equality and `with` expressions for free — useful for
  unit tests comparing expected/actual DTOs.
- ✅ Reading a `record` declaration is a clear signal: "this is immutable
  data on the wire, not a tracked entity."
- ✅ EF Core works naturally with mutable classes; no awkward
  `record class` with `init` properties pretending to be entities.
- ⚠️ One subtle rule for attendees: when they add an MCP tool DTO in Lab 1,
  it must be a `record`, not a `class`. Lab document calls this out.

## Alternatives considered

- **Everything as `class`** — works, hides intent.
- **Everything as `record`** — requires `init`-only properties on entities,
  fights EF Core's change tracking.
