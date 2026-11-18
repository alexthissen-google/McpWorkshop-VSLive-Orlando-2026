# Chapter 06 — Make It Secure (Gate 5)

**Gate:** 5 — Make It Secure · **Time:** 1:30–3:00, shared with Chapter 05 · **Lab:** Lab 5

## Story beat — *reality bites*

Our resilient server has a quiet, fatal flaw: it's **wide open**. It has been on the public
internet since Lab 0, and anyone with the URL could have booked and cancelled every seat. We
show the uncomfortable truth from the research — no auth, long-lived API keys leaking, CVEs
and one-click account takeovers via broken OAuth. Then we add authentication the right way,
to the letter of the spec — and watch the agent break anyway, because the client does not
implement the discovery the spec describes. That gap between the standard and the tooling is
the chasm in one request, and we work around it with open eyes.

## Learning objectives

- Explain MCP's authorization model and why it expects **OAuth 2.1 + PKCE** for HTTP transports.
- Add authentication/authorization to the ASP.NET Core MCP server.
- Walk a correct authorization-code-with-PKCE flow against an identity provider: the
  workshop's **Duende IdentityServer**, running as a project inside Aspire.
- Apply scope- and tool-level authorization so a token can't do everything.
- Derive the caller's identity from the token, not from arguments the model supplies.
- Recognize and avoid the common pitfalls: static API keys, redirect-URI manipulation,
  unbound state (CSRF), missing audience validation, and over-broad scopes.

## The pain point (grounded in research)

This is the single most-cited struggle. ~53% of deployed MCP servers still rely on insecure
long-lived API keys despite the spec mandating OAuth 2.1 + PKCE. The mismatch between what
MCP expects and what real OAuth servers support is "a common point of friction holding back
enterprise adoption." Real CVEs and one-click account-takeover patterns exist: failing to
bind OAuth state to a session enables CSRF; manipulated redirect URIs leak auth codes.
"Individual MCP tools struggle with authentication and often just rely on secret tokens."

## Key concepts / talking points

- **Config-driven auth.** JWT bearer validation switches on when an authority is configured;
  without one a pass-through scheme keeps the same `[Authorize]` attributes working. The same
  binary runs open on a laptop and protected in the cloud.
- **Protected resource metadata.** `.AddMcp(...)` makes an unauthenticated call answer `401`
  with a pointer to the authorization server and its scopes, so a compliant client starts the
  OAuth flow by itself.
- **OAuth 2.1 + PKCE, plainly.** Authorization-code flow with PKCE, no client secret in the
  client, audience validation on the server. Walk the flow end to end so it stops being magic.
- **Authorization, not just authentication.** The booking tools require the `booking:write`
  scope (the `CanBook` policy); searching does not. A leaked read token cannot spend money.
- **Identity from the token.** `ClaimsPrincipal` replaces Lab 4's model-supplied customer
  id, flows into the audit log and the trace (Chapter 04), and backs the ownership rule in
  the domain: bob cannot confirm alice's hold.
- **The non-compliant client.** Azure AI Foundry does not read the `401` or fetch the
  metadata. It needs a static token handed to it, which costs the one property that depended
  on the client: short-lived credentials. *Build the server as if every client is
  non-compliant, because one of them will be.*
- **Enterprise reality.** No homogeneous cross-enterprise MCP authz story yet; we teach the
  defensible baseline and name the open problems honestly.

## Demo arc (instructor, live)

1. Show the unauthenticated server being called by *anyone* — the wide-open problem, live.
2. Switch on IdentityServer and JWT validation; show the `401`, the resource metadata and the
   discovery chain.
3. Ask the agent for a flight: it fails. Explain why, then get a token from the token page
   (sign in as alice, PKCE, consent) and hand it to the agent.
4. Get a token without `booking:write`; show searching works and booking is refused.
5. Show the identity in the trace and the audit trail; let bob try to take alice's hold.
6. The "aha": *MCP auth is hard because it's real OAuth — done correctly, not because it's exotic.*

## Hands-on — Lab 5 (summary)

Attendees protect their `/mcp` endpoint with OAuth 2.1 + PKCE against the Duende
IdentityServer that Aspire has been running all day (test users `alice` and `bob`), gate the
booking tools behind the `booking:write` scope, replace the model-supplied customer id with
the verified identity, and tighten the ownership rule. They then prove it: a rejected call
without a token, the discovery chain by hand, a real token, scopes that bite, the agent
working again with a static token, identity in the audit trail, and bob failing to confirm
alice's hold.

## Gate closed ✅

> ☑ Work ☑ Debuggable ☑ Observable ☑ Resilient ☑ **Secure** ☐ Deployable
> *"It knows who's calling and what they're allowed to do — without the OAuth footguns. Now,
> and only now, is it fit to leave your laptop. Let's ship it to Azure."*

## Instructor notes

- The identity provider ships in the repo and starts with the AppHost; there is nothing to
  pre-register. It is a lab prop: published test passwords and a developer signing key.
- Holds live for one minute since Lab 4, so in the alice-and-bob step attendees should get
  bob signed in *before* alice places the hold.
- Keep the flow diagram on screen throughout — this chapter fails when it gets abstract.
- Be honest about the unsolved enterprise parts; advanced attendees will respect it.
