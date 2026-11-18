# Chapter 02 — Make It Work (Gate 1)

**Gate:** 1 — Make It Work · **Time:** 8:15–10:00, with Lab 0 · **Lab:** Lab 1

## Story beat — *reality bites*

Attendees turn the pre-built Travel Booking application into an MCP server, and it lights up
beautifully in **MCP Inspector**. Victory! Then they hand it to a *real* client, the
**Azure AI Foundry agent** they provisioned in Lab 0, and discover that "works in the
Inspector" is not the same as "works": the model picks its own arguments, mangles an id now
and then, and a wrong URL fails in Azure where nothing in the Codespace looks wrong. This is
the single most common first experience in the wild, and we make it happen *on purpose* so we
can teach the cure.

## Learning objectives

- Add an MCP server to an ASP.NET Core application with `ModelContextProtocol.AspNetCore`.
- Define tools as `[McpServerToolType]` classes with `[McpServerTool]` methods and
  `[Description]` metadata the model actually reads.
- Wire `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()` and `MapMcp("/mcp")`.
- Test with **MCP Inspector**, then connect a **real client** and diagnose the gap.
- Recognize the classic "works in inspector, breaks with a client" causes.

## The pain point (grounded in research)

The number-one beginner failure: a server that passes MCP Inspector but breaks with real
clients. The documented causes, and where the day meets them:

- **Transport / endpoint-path mismatch** — the client points at the wrong route or transport.
  Reproduced hands-on in Lab 1.
- **A stale tool list** — the Foundry agent fetches `tools/list` once and keeps it with the
  agent version, so a renamed tool or a changed description stays invisible until a new
  version is published. The Inspector always shows the live list.
- **Stdout pollution** — anything not-MCP written to stdout corrupts the protocol stream
  (a stdio-transport classic; we use HTTP, where logs and protocol never share a stream).
- **SDK version mismatch** — pinning the wrong/preview package version (we pin it centrally
  in `Directory.Packages.props`).
- **Tool discovered but not invoked** — missing/weak `[Description]`; the model "answers
  without calling it."

## Key concepts / talking points

- Minimal `Program.cs`: `AddMcpServer()` → `WithHttpTransport()` → `WithToolsFromAssembly()`
  → `app.MapMcp("/mcp")`. A separate `/health` endpoint already exists (MCP's JSON-RPC
  responses are not valid health checks).
- Tools are a **thin layer on handlers that already exist** — exactly like the REST
  endpoints, but speaking MCP. The same application logic serves both.
- Why tool **names and descriptions are an API for the model** — they are prompt surface,
  not just docs. Good descriptions are the difference between "tool used" and "tool ignored."
- Typed input/output schemas: the single strongest reliability predictor in the research
  (100% of top servers ship them). Start as we mean to go on.
- **Two clients, two jobs.** MCP Inspector is a protocol test harness: deterministic, you
  choose the arguments. The Foundry agent is a real LLM client: non-deterministic, *it*
  chooses the arguments. You need both.
- CORS: the Inspector runs in a browser and makes cross-origin calls; enable it or the
  client silently fails.
- The server also ships one **resource** and one **prompt**, registered alongside the tools,
  so all three MCP primitives are on the same server.

## Demo arc (instructor, live)

1. Open the pre-built solution; write `SearchFlights` on top of the existing handler and
   register the MCP server.
2. Run MCP Inspector → call the tool → green. "We're done, right?"
3. Ask the Foundry agent a travel question; watch the approval prompt, the arguments the
   model invented, and a failure that the Inspector never showed.
4. Point a client at the wrong path; show that the server logs nothing useful.
5. The "aha": *MCP Inspector tests the protocol, not your integration.*

## Hands-on — Lab 1 (summary)

Attendees write all six tools in two classes (`FlightTools`, `BookingTools`) on the existing
handlers, register the MCP server in the Host, verify in Inspector, pin the tool list down
with a test, hand the tools to their Foundry agent to book a flight, and then break the
endpoint path on purpose. By the end the server is callable by a real agent.

## Gate closed ✅

> ☑ **Work** ☐ Debuggable ☐ Observable ☐ Resilient ☐ Secure ☐ Deployable
> *"It works — with a real client, not just the inspector. That's a higher bar than most
> people's first server clears. Now: when it breaks, can you see why?"*

## Instructor notes

- The seeded data guarantees the routes the labs name (Amsterdam–London among them) on every
  day of the next two months, so nobody burns lab time inventing flights.
- Tell attendees to give the agent a full date with a year; without one the model guesses.
- Keep the failure scripted and *fast* — the lesson is the diagnosis, not the suffering.
- This block also holds Lab 0; protect the build time by keeping the kick-off and Chapter 01 tight.
