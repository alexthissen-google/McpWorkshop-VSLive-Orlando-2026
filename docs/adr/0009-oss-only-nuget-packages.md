# ADR 0009 — OSS-only NuGet packages

**Status:** Accepted
**Date:** 2026-06-10

## Context

The workshop's audience: polyglot developers who self-selected to become MCP
experts back at their own orgs. They'll re-teach these patterns. Picking up a
NuGet package with commercial licensing at their job means a license review or
a procurement ticket — that friction stalls adoption.

## Decision

All NuGet packages in this solution must be free for commercial use. Specifically:

- **No MediatR.** Use plain service handlers — see [ADR 0004](./0004-plain-services-over-mediatr.md).
- **No Fluent Assertions.** Use AwesomeAssertions (its open-source fork) with xunit.v3.
- **No similar commercial successors** without explicit re-review.

When adding a new package, verify licensing on the NuGet page before bringing
it in.

## Consequences

- ✅ Attendees can adopt these patterns without procurement friction.
- ✅ The workshop content stays consistent with the broader "use what you can
  actually ship at home" promise.
- ⚠️ Loses MediatR's pipeline behaviors, which are replaceable with slightly more
  code that's arguably clearer to read. AwesomeAssertions keeps the chained
  assertion syntax.

## Alternatives considered

- **Allow commercial packages** — defers the friction to attendees' orgs.
- **Pick OSS-only at random and call it out as a workshop quirk** — opens the
  door to inconsistency over time.
