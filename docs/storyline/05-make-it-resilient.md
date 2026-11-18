# Chapter 05 — Make It Resilient (Gate 4)

**Gate:** 4 — Make It Resilient · **Time:** 1:30–3:00, shared with Chapter 06 · **Lab:** Lab 4

## Story beat — *reality bites*

We put **Dev Proxy** in the path between client and server and turn reality up: responses
slow to a crawl, then half the calls come back `429` or `503`. A seat **hold expires
mid-flow** and `ConfirmBooking` arrives too late; a response gets lost, the client retries,
and the retry fails *because the first attempt succeeded*. Then the database has a bad
moment of its own. Nothing in the code is "wrong" — it just never met production. This is
the gate where a demo becomes a system.

## Learning objectives

- Simulate production failure modes locally with **Dev Proxy**: latency and transient
  errors (429/503).
- See why a retry policy alone does not fix a lost response (the double-booking problem).
- Make tools **idempotent** so retries are safe, and prove it with tests.
- Handle transient database failures with **EF Core's retry strategy**.
- Use the OpenTelemetry trace from Chapter 04 to *prove* the retry happened.

## The pain point (grounded in research)

The reliability data is stark: only ~82% of even *good* MCP servers retry transient errors,
87% handle timeouts/cancellation, 91% are idempotent. Without timeout budgets "one slow
operation holds resources indefinitely," leading to "connection pool exhaustion, health
check failures, and container restarts." For agents, latency *is* a reliability problem
because LLMs have hard timeouts. Top servers turn transient noise into a single clean
outcome — succeed or definitively fail — with bounded tail latency (p99 ~2,100ms vs
~6,200ms ecosystem-wide).

## Key concepts / talking points

- **Dev Proxy** as a production simulator: inject latency, rate limits and error rates
  without touching the server. It is a *forward* proxy, so it only sees clients that route
  through it; in the lab the attendee plays the agent with `curl -x`, then hands the result
  to the real agent.
- **The error message is a prompt fragment.** `Hold X expired at T` tells the model, or the
  human, what to do next. Hosts differ in whether they pass a tool error back to the model,
  and you do not control the host.
- **Retries done right.** Retry only *transient* faults, with bounded attempts, as close to
  the transient resource as possible: an EF Core retry repeats one query, a tool-level retry
  would repeat the whole use case, audit entries included.
- **Idempotency.** The seat-hold/confirm flow makes this concrete: a naive retry after a
  lost response leaves the customer with the wrong seat. The same caller asking again for
  the same seat gets the same hold back; confirming twice returns the same booking — the
  heart of the chapter.
- **Timeout budgets and backoff.** Every call needs a deadline, and a throttled client
  should honor `Retry-After` with backoff and jitter. These are talking points and stretch
  goals rather than lab steps.
- **Prove it in the trace.** The failed attempt and the retry appear as spans; resilience
  becomes visible, not theoretical.

## Demo arc (instructor, live)

1. Shorten the hold to one minute; put Dev Proxy's latency in the path; let a hold expire
   before the confirm and show what the agent makes of the error.
2. Switch to throttling; show the 429s and `Retry-After`.
3. Demonstrate the retry trap: a hold that succeeded, retried, now fails. Fix it with an
   idempotent `HoldSeat`.
4. Stop Postgres in the Aspire dashboard mid-request; switch on EF Core's retry strategy;
   show the failed span and the retry in the trace.
5. The "aha": *resilience is a design property you can see in a trace — not a hope.*

## Hands-on — Lab 4 (summary)

Attendees run their server behind Dev Proxy (latency, then 429/503 throttling), watch a hold
expire mid-flow, make `HoldSeat` and `ConfirmBooking` idempotent and prove it with tests,
and add EF Core retry for transient database failures, checking the result in the logs and
the trace. Timeout budgets and a backoff-aware client are stretch goals.

## Gate closed ✅

> ☑ Work ☑ Debuggable ☑ Observable ☑ **Resilient** ☐ Secure ☐ Deployable
> *"Your server now survives a bad day on the network. But right now *anyone* can call it.
> Before it goes near the cloud, it needs to know who's knocking."*

## Instructor notes

- This is the conceptual peak of the day — the seat-hold idempotency story is the keystone;
  rehearse it until it's crisp.
- The Dev Proxy configurations are pre-baked in `labs/assets/lab-04/`, so attendees toggle
  scenarios, not author them.
- Idempotency needs to know who is asking. Lab 4 uses a `customerId` the model supplies as a
  stopgap; Chapter 06 replaces it with a verified identity. Say so, it sets up the next gate.
