# ADR 0008 — Audit logging directly from handlers

**Status:** Accepted
**Date:** 2026-06-10

## Context

Lab 5 (Security) wants a persisted audit trail of who did what when. Two common
patterns:

1. **Interceptor / middleware** — wrap every call automatically (MediatR pipeline
   behavior, EF Core `SaveChangesInterceptor`, or ASP.NET middleware).
2. **Explicit calls from handlers** — each state-changing handler calls
   `IAuditService.LogAsync` directly.

We're not using MediatR (see [ADR 0004](./0004-plain-services-over-mediatr.md)),
so pipeline behaviors aren't free. An EF Core interceptor would couple audit
semantics to entity tracking — fragile.

## Decision

Each state-changing Application handler (`HoldSeatHandler`,
`ConfirmBookingHandler`, `CancelBookingHandler`) wraps its core call in
try/catch and writes to `IAuditService` on both success and failure paths.
Read handlers do not audit.

```csharp
try
{
    var hold = await bookings.HoldSeatAsync(...);
    await audit.LogAsync("HoldSeat", "SeatHold", hold.Id, ..., AuditOutcome.Success, ...);
    return new HoldResult(...);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    await audit.LogAsync("HoldSeat", "SeatHold", Guid.Empty, ..., AuditOutcome.Failure, ..., ex.Message, ...);
    throw;
}
```

The catch is intentionally broad: any failure (domain rejection, DB concurrency, downstream service error) gets audited. Cancellation is excluded because it's not a failure of the operation — the caller dropped.


## Consequences

- ✅ Audit policy is visible in the code path. Easy to grep "who logs ConfirmBooking?"
- ✅ Failure paths get audited too — auditors can see "user X attempted to hold a
  booked seat and got rejected."
- ✅ Lab 5 can swap `userId: null` for a `ClaimsIdentity` lookup at the same
  call sites — no architectural change.
- ⚠️ More boilerplate in handlers than a pipeline behavior would have.
  Tradeoff: explicitness > less code in a teaching repo.
- ⚠️ Risk of forgetting to wrap a new handler. Mitigated by the small fixed
  number of state-changing operations (3) and tests asserting audit entries
  on both paths.

## Alternatives considered

- **EF Core `SaveChangesInterceptor`** — couples audit to entity changes
  rather than business operations. Logs would say "rows inserted" instead of
  "HoldSeat called."
- **ASP.NET middleware** — captures HTTP, not the operation semantics. Same
  problem at the MCP layer (no HTTP framing).
