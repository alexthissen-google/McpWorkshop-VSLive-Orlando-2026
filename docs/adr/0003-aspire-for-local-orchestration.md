# ADR 0003 — .NET Aspire for local orchestration

**Status:** Accepted
**Date:** 2026-06-10

## Context

The demo app composes multiple processes: the ASP.NET Core host, Postgres,
pgAdmin (debugging), IdentityServer (used from Lab 5) and the Aspire dashboard
for traces and logs (Lab 3). Attendees need *one command* that brings the right
stack up. Dev Proxy (Lab 4) is the one tool that stays outside: it is started by
hand, in its own terminal.

## Decision

Use .NET Aspire's `DistributedApplication.CreateBuilder` (the AppHost) as the
local orchestrator. Every resource (Postgres, pgAdmin, IdentityServer, the Host)
is declared in `AppHost.cs` from the start; labs switch things on rather than add
resources. Lab 5 flips `UseIdentityServer` in the AppHost's `appsettings.json`,
and Lab 6 marks which endpoints are public.

We deliberately did **not** use `docker-compose`:

- Aspire is the workshop subject material on .NET 11 — picking the .NET-native
  story matches the stack story attendees take home.
- The Aspire dashboard's OTEL ingestion replaces a separate Jaeger/Prometheus
  setup for Lab 3 (Observability).
- Aspire's service-discovery wires DB connection strings into the Host without
  attendees touching `appsettings.json`.

## Consequences

- ✅ One command (`aspire run`, or `dotnet run --project src/aspire/TravelBooking.AppHost`)
  brings up the whole stack.
- ✅ Lab 3 gets traces/logs in the Aspire dashboard with effectively zero config.
- ✅ Resource references propagate connection details automatically — Lab 5's
  Identity Server URL flows into the Host without hand-wiring.
- ⚠️ Aspire's dashboard wants HTTPS by default. The AppHost's `http` launch
  profile is listed first, so it is the default, and it sets
  `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`; nobody has to trust a dev cert.
- ⚠️ Aspire templates evolve between scaffolds. Dependencies are managed centrally —
  see [ADR 0010](./0010-centralized-package-management.md).

## Alternatives considered

- **`docker-compose`** — works fine but adds a second orchestration mental model
  on top of .NET that attendees don't need to learn.
- **Manual `dotnet run` + side-running Postgres** — fastest to start, terrible
  to teach (six commands per lab).
