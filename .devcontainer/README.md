# Dev Container

Defines the **GitHub Codespaces** environment for attendees — they need only a browser.

Lives at the repo root (`.devcontainer/`) because that is where GitHub Codespaces
auto-discovers it; the attendee configuration is in `.devcontainer/codespaces/`. It builds
the **.NET 11** image and adds Azure CLI, Node (for tooling
such as `npx`), and the GitHub CLI, plus the C#/Copilot/Azure Container Apps VS Code
extensions. The post-create scripts also pre-install the Aspire CLI (pinned to the `aspire.cli`
version in `.config/dotnet-tools.json`, so `aspire` and `dotnet aspire` agree), the Azure
Developer CLI (pinned to the version Lab 6 names) and
MCP Inspector (a global npm package, so `npx @modelcontextprotocol/inspector` starts without a
download). Dev Proxy is installed at image build time by the local feature
`.devcontainer/codespaces/local-features/devproxy`, pinned to the version the
`labs/assets/lab-04/*.json` schemas target.
The workshop **source code lives in `/src`**.

To start: open the repo in a Codespace, then build/run from `src/`.

## Forwarded ports

| Port | What |
|---|---|
| 5197 | Travel Booking Host — the REST API **and** the `/mcp` endpoint. Every lab uses it. |
| 5001 | Duende IdentityServer (Lab 5) — login, consent, and the `/Token` page |
| 6274 | MCP Inspector — `remoteEnv` pre-sets `ALLOWED_ORIGINS` to the Codespace's forwarded origin, or every Connect in the Inspector answers 403 |
| 15095 | Aspire dashboard |

The dashboard port is fixed by the AppHost's `http` launch profile (`applicationUrl`), which
is listed **first** in `launchSettings.json` on purpose: `dotnet run`, `aspire run` and the
VS Code Aspire extension all fall back to the first profile when none is named, so the
dashboard lands on 15095 however it is started. The `https` profile would put it on 17055.

Three of these are made **public** on every Codespace start (see `postStartCommand`):
5197, because the Azure AI Foundry agent runs in Azure and cannot reach a private port;
5001, because MCP Inspector redeems its authorization code with a cross-origin
request that carries no GitHub session cookie, so a private port would reject it;
and 15095, so the dashboard login link from the AppHost output opens without a detour
through GitHub's sign-in page (the dashboard still requires its own browser token).
Lab 5's own token flow does not need 5001 to be public — only Inspector's does.

## Exposing the MCP server to the Azure AI Foundry agent

The Foundry agent runs in Azure and cannot reach a forwarded port that is
private to your Codespace. The Codespace makes **port 5197 public** on every start
(`postStartCommand`); to do it by hand:

```bash
gh codespace ports visibility 5197:public -c "$CODESPACE_NAME"
```

The public URL is `https://$CODESPACE_NAME-5197.app.github.dev`, and the MCP
endpoint is that plus `/mcp`.

> ⚠️ **A public port reverts to private when the Codespace restarts** — and
> Codespaces stop after 30 minutes idle by default. `postStartCommand` makes it public
> again, but that step can fail. If the agent suddenly starts failing after a break,
> re-run the command above before debugging anything else.

**Dev Tunnels is not part of the attendee path.** Codespaces port forwarding is
itself built on the Dev Tunnels service, so the CLI adds nothing here. It is
only useful to the workshop authors running the stack *outside* a Codespace, who
can install it on demand:

```bash
curl -sL https://aka.ms/DevTunnelCliInstall | bash   # authors only, not needed in a Codespace
```
