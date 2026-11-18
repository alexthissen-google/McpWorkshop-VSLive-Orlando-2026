# Architecture Decision Records

Short, dated records of the architecture/design choices that shape this project.
Format: lightweight [MADR](https://adr.github.io/madr/) — Context, Decision, Consequences.

| # | Title | Status |
|---|-------|--------|
| [0001](./0001-clean-architecture.md) | Clean Architecture with six layer projects | Accepted |
| [0002](./0002-postgresql-and-ef-core.md) | PostgreSQL + EF Core (no in-memory) | Accepted |
| [0003](./0003-aspire-for-local-orchestration.md) | .NET Aspire for local orchestration | Accepted |
| [0004](./0004-plain-services-over-mediatr.md) | Plain services in Application (no MediatR) | Accepted |
| [0005](./0005-records-for-dtos.md) | Records for DTOs, classes for EF entities | Accepted |
| [0006](./0006-http-streamable-mcp-transport.md) | HTTP streamable MCP transport (not stdio) | Accepted |
| [0007](./0007-computed-hold-expiration.md) | Hold expiration computed on read | Accepted |
| [0008](./0008-audit-logging-from-handlers.md) | Audit logging directly from handlers | Accepted |
| [0009](./0009-oss-only-nuget-packages.md) | OSS-only NuGet packages | Accepted |
| [0010](./0010-centralized-package-management.md) | Centralized Package Management | Accepted |

## When to write a new ADR

Add an ADR when you make a decision that:
- Locks future labs into a specific shape (hard to reverse without rewriting attendee guidance)
- Picks between two reasonable alternatives where the "why" isn't obvious from the code
- Constrains library/package choices

Skip ADRs for mechanical fixes, refactors that don't change shape, and decisions that the code itself documents clearly.
