# ADR 0002 — PostgreSQL + EF Core (no in-memory)

**Status:** Accepted
**Date:** 2026-06-10
**Supersedes:** the original plan for an in-memory store.

## Context

Original plan was an in-memory store seeded with fake flights — no DB to fight
inside Codespaces. After review, we wanted attendees to be able to *query* state
between lab steps (peek at audit entries, verify a hold was created, etc.), which
is awkward against in-memory dictionaries. We also wanted realistic resilience
patterns in Lab 4 (transient connection failures, EF Core retry strategies) —
those don't exist with in-memory data.

## Decision

Use PostgreSQL (containerized via .NET Aspire) with EF Core as the ORM.
Schema bootstrap via EF Core migrations; data seeded by a `DataSeeder` class
after migrations apply. PostgreSQL because:

- First-class JSON column support (`jsonb`) for `Flight.SeatMap`.
- Real connection lifecycle for the resilience lab.
- Containerized and orchestrated by Aspire — no manual setup.

## Consequences

- ✅ Attendees can `SELECT * FROM audit_entries` to verify the audit lab works.
- ✅ Lab 4 (resilience) gets real `Npgsql` transient errors to retry against
  EF Core's built-in retry strategy (`EnableRetryOnFailure`).
- ✅ Lab 6 (deploy) ships the same Postgres as a container next to the Host; a move
  to Azure Database for PostgreSQL later needs no schema work.
- ⚠️ Codespaces must have Docker (or an alternative container runtime). Mitigated
  by GitHub Codespaces' default dev container support.
- ⚠️ First startup pulls the Postgres image. ~30–60 sec on a fresh Codespace.

## Alternatives considered

- **In-memory** — original plan; rejected because attendees can't query state and
  resilience lessons become contrived.
- **SQLite** — file-based, no container required, but no `jsonb`, weaker EF Core
  story for indexes on JSON, no real concurrency.
- **Cosmos DB / SQL Server** — overkill for a workshop; pulls in larger images.
