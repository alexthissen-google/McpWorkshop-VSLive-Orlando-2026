# ADR 0004 — Plain services in Application (no MediatR)

**Status:** Accepted
**Date:** 2026-06-10

## Context

A typical .NET Clean Architecture project uses MediatR for the Application layer
(`IRequest<T>` + `IRequestHandler<T,R>` + pipeline behaviors for cross-cutting
concerns). Some MediatR releases are commercially licensed. Workshop attendees take patterns
back to their orgs without procurement friction — see
[ADR 0009](./0009-oss-only-nuget-packages.md).

## Decision

Use plain handler classes in `TravelBooking.Application/Handlers/`. Each handler
exposes a single `HandleAsync(...)` method, takes a request DTO, returns a
response DTO. Registered as scoped services in DI.

```csharp
public class HoldSeatHandler(IBookingService bookings, IAuditService audit)
{
    public Task<HoldResult> HandleAsync(Guid flightId, string seatNumber,
        string? userId, CancellationToken ct) { ... }
}
```

REST endpoints inject the handler directly; MCP tools (Lab 1) will do the same.

## Consequences

- ✅ Zero commercial dependencies. Attendees can adopt the pattern at home.
- ✅ Stack trace from a failing call goes API → Handler → Domain Service →
  EF Core — no MediatR dispatch frame in the middle. Easier to teach.
- ✅ DI is explicit; no auto-discovery magic.
- ⚠️ No free pipeline behaviors. Audit logging is written directly in the handler
  (see [ADR 0008](./0008-audit-logging-from-handlers.md)). Cross-cutting concerns
  for logging/tracing (Labs 2-3) land in middleware + Infrastructure instead.

## Alternatives considered

- **MediatR (commercial)** — banned by [ADR 0009](./0009-oss-only-nuget-packages.md).
- **MassTransit Mediator** (OSS) — adds a heavyweight dependency for one pattern.
- **A bespoke `IMediator` clone** — solves a problem we don't have.
