# ADR 0007 — Hold expiration computed on read

**Status:** Accepted
**Date:** 2026-06-10

## Context

A `SeatHold` lives for a configurable duration (default 5 minutes). After
expiry, the seat must be available again and `ConfirmBooking` against the
expired hold must fail with a clear error.

Two implementations are conventional:

1. **Background sweeper** — a hosted service polls `seat_holds` periodically
   and marks expired rows as `Expired`.
2. **Computed on read** — store `ExpiresAtUtc`; compare to "now" wherever
   the hold's state matters.

## Decision

Store `ExpiresAtUtc` on the row. Treat a hold as expired if
`Status == Active && ExpiresAtUtc <= utcNow`. No background job ever writes
`Expired`. The `HoldStatus` enum does have an `Expired` value, and it is written
lazily: when a seat is held again, `Flight.HoldSeat` marks that seat's lapsed
holds `Expired`, so the unique index on active holds lets the seat be re-held.

## Consequences

- ✅ No background job to wire, schedule, monitor, or shut down cleanly.
- ✅ No race conditions between sweep and confirm — the source of truth is the
  clock.
- ✅ `TimeProvider` is injected, so tests can fast-forward time without sleeps.
- ⚠️ Stale rows accumulate in the DB. Acceptable for the workshop's runtime;
  a future production deployment could add a daily cleanup.
- ⚠️ Every query that asks "is this seat held?" must include the
  `ExpiresAtUtc > now` predicate. It lives in `SeatHold.IsActive`, which
  `Flight.HoldSeat` and `Flight.AvailableSeats` use; `FlightService.GetAvailableSeatCountsAsync`
  restates it once in its count query (accepted, so the search stays one SQL round trip).

## Alternatives considered

- **Background sweeper** — more code, more failure modes, no win for the
  workshop story.
- **Database-side check via a computed column or view** — Postgres-specific,
  obscures the rule, harder to teach.
