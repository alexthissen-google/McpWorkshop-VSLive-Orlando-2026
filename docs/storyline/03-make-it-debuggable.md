# Chapter 03 — Make It Debuggable (Gate 2)

**Gate:** 2 — Make It Debuggable · **Time:** 10:15–12:00, shared with Chapter 04 · **Lab:** Lab 2

## Story beat — *reality bites*

The server works — until it doesn't, and now the failure is happening *between* a cloud
client and code in your Codespace. The agent just says "something went wrong", the user sees
an apology, and you see nothing. Debugging agentic systems means first being able to *get a
request to your breakpoint*, *make failures say what went wrong*, and *see the raw traffic*.
That's this gate.

## Learning objectives

- Set breakpoints and step through MCP tool invocations in Codespaces / VS Code, with the
  debugger attached to the whole Aspire AppHost.
- Understand why you cannot debug *through* a real agent: a client with a timeout turns
  "slow" into "broken".
- Turn silent failures into loud ones: proper MCP errors the caller can act on, plus
  structured logs.
- Inspect the raw MCP/HTTP traffic to see exactly what the client sent.

## The pain point (grounded in research)

MCP servers "fail silently": stdio transport swallows logging, stray console output
corrupts the protocol, and errors vanish instead of surfacing. Add the network gap — a
client in the cloud, code somewhere else — and the default experience is *no signal at all*.
Developers can't reproduce, can't breakpoint, can't see the payload.

## Key concepts / talking points

- **Reaching the server.** The Codespace publishes port 5197 as a public forwarded port, so
  the agent in Azure calls the same process you are debugging. (Codespaces port forwarding is
  built on the Dev Tunnels service; outside a Codespace, a dev tunnel does the same job.)
- **Local debugging flow.** Start the AppHost with the debugger attached; break inside a
  handler; inspect the model-supplied arguments (a reminder: *you don't control these inputs*).
- **Debug with the Inspector, verify with the agent.** Foundry gives a tool call 100 seconds
  and then gives up, so sitting on a breakpoint fails the agent's call even though the server
  is correct. Breakpoints do not scale past your own machine.
- **Make failures loud.** Domain errors surface as `McpException` with an actionable message
  instead of a generic "an error occurred", and are logged with structure.
- **Traffic inspection.** A small middleware logs the JSON-RPC body and headers of every
  `/mcp` request, so you can distinguish "client sent the wrong thing" from "my tool did the
  wrong thing" — the question Chapter 02 left open. Headers worth knowing: `User-Agent`
  (which client is talking), `Mcp-Method` and `Mcp-Name`.
- **The human in the loop.** The agent asks for approval before every tool call. Switching
  that off is the trade every team makes on the way to production, and it raises the bar for
  the server.

## Demo arc (instructor, live)

1. Start the AppHost under the debugger; hit a breakpoint in `HoldSeatHandler` from the
   Inspector; inspect live arguments.
2. Do the same through the agent and wait: after 100 seconds the call fails, with a healthy
   server.
3. Show a swallowed domain error, then convert it into a visible MCP error + a log line.
4. Switch on wire logging; read one request's headers and body.
5. The "aha": *most MCP debugging pain is a visibility problem, not a logic problem.*

## Hands-on — Lab 2 (summary)

Attendees attach the debugger to their Chapter 2 server and break inside a tool call driven
from MCP Inspector, feel the agent's 100-second timeout, turn a silent `ConfirmBooking`
failure into a visible, logged MCP error, add wire logging to watch the raw JSON-RPC and its
headers, and switch off the agent's approval prompts for the rest of the day.

## Gate closed ✅

> ☑ Work ☑ **Debuggable** ☐ Observable ☐ Resilient ☐ Secure ☐ Deployable
> *"You can now get any request to a breakpoint and see the raw traffic. But breakpoints
> don't scale to production or to non-deterministic agents. For that, we need traces."*

## Instructor notes

- If an agent suddenly cannot reach a server, check that port 5197 is still public; the
  Codespace re-publishes it on every start, but that step can fail.
- Changing the approval policy publishes a new agent version, so attendees must use the new
  playground link that `deploy.sh` prints.
- This chapter sets up Chapter 04 directly: debugging-by-breakpoint → observability-by-trace.
- Lab 3 follows in the same block; lunch comes after Chapter 04.
