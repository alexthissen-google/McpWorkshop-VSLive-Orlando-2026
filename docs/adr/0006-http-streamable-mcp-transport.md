# ADR 0006 — HTTP streamable MCP transport (not stdio)

**Status:** Accepted
**Date:** 2026-06-10

## Context

`ModelContextProtocol.AspNetCore` supports two transports:

- **stdio** — single local process per client, classic MCP "tool over stdin/stdout."
- **streamable HTTP** — networked, multi-client, multi-tenant. `MapMcp("/mcp")`.

The workshop runs in GitHub Codespaces, exposes the server on a public forwarded
port to an external client (an Azure AI Foundry agent), and ultimately deploys to
Azure Container Apps.

## Decision

Use **streamable HTTP** transport exclusively. Wire with
`AddMcpServer().WithHttpTransport().WithToolsFromAssembly()` and
`app.MapMcp("/mcp")` (attendees do this in Lab 1).

## Consequences

- ✅ Same transport locally, over the public Codespaces port, and in Azure. No re-plumbing per gate.
- ✅ Multiple clients can connect concurrently — required for Lab 4 (resilience)
  to demo retries from one client while another holds a seat.
- ✅ Health probes (`/health`) and MCP (`/mcp`) live in the same ASP.NET Core
  pipeline. Standard middleware (auth, OTEL, exception handlers) wraps both.
- ⚠️ Stdio is the more familiar MCP onboarding path elsewhere. Chapter 01 needs
  to explicitly call out the choice: "We're networked because production is."
- ⚠️ MCP Inspector must be told to use HTTP transport. Lab 1 documents this.

## Alternatives considered

- **stdio** — natural for local toy demos; falls apart when the next step is
  "now serve a real client over the network."
- **Run both transports** — clutter without payoff in a one-day workshop.
