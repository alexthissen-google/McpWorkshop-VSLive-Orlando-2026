# Chapter 07 — Make It Deployable to Azure (Gate 6)

**Gate:** 6 — Make It Deployable · **Time:** 3:15–4:45 · **Lab:** Lab 6

## Story beat — *the payoff*

Everything so far lives in a Codespace. This is the moment it becomes **real**: the same
hardened server — observable, resilient, secured — deploys to **Azure** and answers a real
client over the public internet, with its traces visible in the cloud. Attendees watch their
own MCP server cross the finish line, then make their Codespace port private again and see
the agent keep working.

## Learning objectives

- Compare Azure hosting options for an MCP server and choose deliberately.
- Deploy the whole Aspire application to **Azure Container Apps** with the Azure Developer
  CLI (`azd`), straight from the AppHost.
- Wire production essentials: health endpoints, which endpoints face the internet,
  configuration that differs between local and cloud, and telemetry in the cloud.
- Validate the deployed server end-to-end from a real client and read its cloud trace.

## The pain point (grounded in research)

After "it runs locally," developers stall on *where* and *how* to host. The hosting story is
unclear, health checks are subtly wrong (MCP's JSON-RPC responses aren't valid health probes
— you need a dedicated `/health`), and identity/telemetry that "worked locally" must be
re-wired for the cloud. We make the choice explicit and the path concrete.

## Key concepts / talking points

- **Hosting options, compared.** Azure Container Apps (our pick: serverless containers,
  scale-to-zero, easy ingress) vs App Service vs Azure Functions (remote MCP) vs AKS. Pick by
  control vs operational burden; we deploy to **Container Apps** and explain the trade-off.
- **The orchestration file is the deployment model.** `azd` reads the Aspire AppHost, infers
  the container resources, builds the images and provisions everything. There is no
  Dockerfile and no hand-written infrastructure code.
- **Health probes.** The `/health` and `/alive` endpoints were only mapped in Development;
  the first step is to map them everywhere — a direct callback to Chapter 02.
- **What faces the internet.** `WithExternalHttpEndpoints()` on the Host and the identity
  provider; Postgres stays internal.
- **Local versus published.** The AppHost knows whether it is describing `aspire run` or
  `azd up` and decides the scheme (`http` locally, `https` behind the ingress), the data
  volume and the identity provider's settings accordingly.
- **Identity in the cloud.** The workshop identity provider is deployed alongside the server
  so Chapter 06's work survives the move. It is a lab prop; a real system points
  `Authentication:Authority` at a real identity provider, and the server code does not change.
- **Telemetry in the cloud.** The deployed Aspire dashboard collects OTLP exactly as the
  local one did, and the spans also reach Application Insights: the *same trace shape* from
  Chapter 04, now with a cloud hop.
- **Secrets are configuration.** A connection string baked into an image is the habit
  Chapter 06 argued against; inject it through the environment instead (stretch goal).

## Demo arc (instructor, live)

1. Fix the health endpoints; mark the public endpoints in `AppHost.cs`.
2. `azd init`, `azd up`; show the endpoints come live and probe `/health` over the internet.
3. Get a token from the *deployed* identity provider; re-point the agent at the public
   endpoint; complete a booking.
4. Open the cloud trace — the *same* waterfall from Chapter 04, now spanning the internet.
5. The "aha": *because we closed gates 1–5 first, deploying is the easy part.*

## Hands-on — Lab 6 (summary)

Attendees map the health endpoints in every environment, decide which endpoints face the
internet, and deploy the whole Aspire application (Host, identity provider and PostgreSQL)
to Azure Container Apps with `azd init` and `azd up`, into their own Azure subscription. They
confirm the `/health` probe, get a token from the deployed identity provider, re-point their
agent, book a flight against the cloud, read the trace in the deployed Aspire dashboard, and
clean up with `azd down`.

## Gate closed ✅ — all gates closed

> ☑ Work ☑ Debuggable ☑ Observable ☑ Resilient ☑ Secure ☑ **Deployable**
> *"This morning you had an empty `Tools/` folder. You now have a production-grade MCP server
> running in Azure. You've crossed the chasm."*

## Instructor notes

- Attendees deploy into their own Azure subscription, arranged before the workshop.
  Provisioning takes 5–10 minutes; start it early and talk through the trace while it runs.
- If `azd up` fails with a container build error about a file in use, a second `azd up`
  usually works; `AZD_CONCURRENCY_MAX=1 azd up` builds one container at a time.
- Re-pointing the agent publishes a new agent version, so attendees must use the new
  playground link.
- Have a pre-deployed reference instance as a fallback so the narrative survives a bad network.
- If time is tight, this is the chapter to trim to a guided walkthrough + reference deploy.
