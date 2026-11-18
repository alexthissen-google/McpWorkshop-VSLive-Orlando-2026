# Lab 0 — Preparing your environment

In this lab you will start your GitHub Codespace, take a quick tour of the Travel Booking
solution, verify that everything builds and runs, and provision the Azure AI agent you
will talk to for the rest of the day. At the end you will have a working REST API backed
by PostgreSQL, and an agent in Azure waiting for the MCP server you are about to build.

> ⏱ Expected time: ~45 minutes.

## Start your Codespace

<!-- Authors: this link names the event's repository. Update it for every new event. -->
1. Open **[Create your workshop Codespace](https://codespaces.new/loekd-consultancy/McpWorkshop-VSLive-Orlando-2026?quickstart=1&devcontainer_path=.devcontainer%2Fcodespaces%2Fdevcontainer.json&machine=standardLinux32gb&location=EastUs)**
   and sign in to GitHub if asked. The link selects the workshop's dev container, a 4-core
   machine and the US East region for you.
2. Click the green button to create the Codespace. If you already created one with this
   link, GitHub offers to resume that one instead; do that.
3. Wait for the Codespace to finish building. The first start can take a few minutes; you
   will know it is done when Visual Studio Code opens in your browser and the terminal
   prompt appears.

> Note: if the link does not work for you, open the workshop repository on GitHub, click the
> green **Code** button, select the **Codespaces** tab, open the **…** menu and choose
> **New with options…**. Pick the dev container configuration **MCP Workshop (Codespaces)**,
> region **US East** and the **4-core** machine type.

> Note: if you prefer working in your local VS Code, you can also open the Codespace from
> the desktop app (`Codespaces: Connect to Codespace` in the command palette). Everything in
> these labs works in both.

## Tour the solution

Take two minutes to look around. The important places:

```
src/
├── TravelBooking.Domain/          entities + service interfaces (no dependencies)
├── TravelBooking.Application/     use-case handlers (SearchFlights, HoldSeat, ...)
├── TravelBooking.Infrastructure/  EF Core, PostgreSQL, seeding
├── TravelBooking.API/             REST endpoints (for testing the same logic)
├── TravelBooking.MCP/             ⬅ you will (re)build this in Lab 1
├── TravelBooking.Host/            composition root (Program.cs)
├── TravelBooking.IdentityServer/  the identity provider you meet in Lab 5
├── aspire/                        .NET Aspire orchestrator + service defaults
└── tests/                         unit and integration tests
infra/attendee/                    scripts that provision your Azure AI agent
docs/storyline/                    the day's narrative, one chapter per gate
labs/                              you are here
```

The MCP project (`src/TravelBooking.MCP`) is where the day's work lands — turning this
application into an MCP server is *your* job, starting in Lab 1. If you peek now you will
find it already full: the repository is committed in its **finished** state, so it always
builds and the proctors always have a working reference. The next step takes it apart.

If you want the full background, [`ARCHITECTURE.md`](../ARCHITECTURE.md) explains the design
in 60 seconds, and [`docs/adr/`](../docs/adr/) holds the decision records.

## Put the code in its starting position

Open a terminal (`` Ctrl+` `` or `` Cmd+` ``) and run:

```bash
./labs/prepare-lab.sh 1
```

The script reverse-applies the changes of Labs 1 to 6 as *uncommitted* edits to your
working tree, then builds to prove the starting point compiles. The output should end
with:

```txt
Build succeeded.

Check your work:   ./labs/prepare-lab.sh check 1
Undo at any time:  ./labs/prepare-lab.sh solution
```

If you see errors, please ask a proctor for help before continuing.

Look at `src/TravelBooking.MCP/Tools/` now: empty, apart from a `.gitkeep`. Run
`git status` and you are looking at the day's work in reverse — everything the labs will
put back.

### See the reference next to your code

The same thing, visually: open the **Source Control** view (`Ctrl+Shift+G` /
`Cmd+Shift+G`). Every file the labs will touch is listed as modified or deleted, because
the committed code *is* the finished solution and the script only took the finishing
touches out. Click `src/TravelBooking.Host/Program.cs`: the solution is on the left, your
copy on the right, and the MCP wiring Lab 1 asks you to add is already there to read.

Keep that view open all day. As you work through a lab the list shrinks, and whenever a
step feels unclear, clicking the file shows what the reference did. Two things to know
when reading it: it compares against the end of Lab 6, so files that a *later* lab
changes stay listed until you get there; and a using directive in a different spot or a
comment of your own counts as a change there. `./labs/prepare-lab.sh check N` is the
precise version: it compares against the end of Lab N only and ignores ordering,
comments and whitespace.

> **Three commands to remember all day.** `./labs/prepare-lab.sh N` jumps to the start of
> Lab N, which is how you catch up if you fall behind. `./labs/prepare-lab.sh check N`
> diffs your tree against the finished state of Lab N, so you can compare your work with
> the reference at any point. And `./labs/prepare-lab.sh solution` restores the finished
> code, so a broken tree is never more than one command from green. Switching never
> discards anything: your previous edits are parked in a `git stash` entry first.

## Run the tests

```bash
dotnet test TravelBooking.slnx
```

All tests should pass. These tests cover the Domain, Application, and Infrastructure layers
— the plumbing you will *not* have to write today.

## Start the application

The solution is orchestrated by .NET Aspire: one command starts PostgreSQL (in a
container), pgAdmin, an identity provider (idle until Lab 5), and the Host application,
wired together. The Aspire CLI finds the
AppHost project for you:

```bash
aspire run
```

Watch the output. After a few seconds you should see a line similar to:

```txt
Dashboard:  http://localhost:15095/login?t=<token>
```

`Ctrl+Click` (or `Cmd+Click`) that link. Codespaces forwards the port automatically and the
**Aspire dashboard** opens in a new browser tab.

> The dashboard is always on port **15095**: the `http` launch profile pins it, and it is the
> first profile in `launchSettings.json`, so `aspire run` and the VS Code Aspire extension both
> land on it. The Codespace makes 15095 public on every start, so the login link opens
> directly; the dashboard still asks for the token in the link.

On the **Resources** page, wait until every resource shows state **Running**:

- `postgres` — the PostgreSQL container (plus `pgadmin`)
- `travelbookingdb` — the database
- `travelbooking-host` — the ASP.NET Core application
- `identity-server` — the identity provider; it runs from the start but nothing uses it until Lab 5

On startup the Host applies EF Core migrations and seeds the database with ~30 airports and
some 500 flights (the routes the labs name fly every day), so there is real data to play
with immediately.

> Note: keep this terminal running. From now on, open *new* terminal tabs for other
> commands (click the `+` in the terminal panel).

## Smoke-test the REST API

The Host listens on port **5197** (you can confirm this in the dashboard: the
`travelbooking-host` resource shows its endpoint). In a new terminal, check the health
endpoint:

```bash
curl -s http://localhost:5197/health
```

Expected output:

```txt
Healthy
```

Now find a real flight to search for. Open the **pgAdmin** resource from the Aspire
dashboard (click its endpoint URL), navigate to the `travelbookingdb` database, open the
Query Tool, and run:

```sql
SELECT d."Code" AS from_airport,
       a."Code" AS to_airport,
       f."DepartureTimeUtc"::date AS travel_date,
       f."FlightNumber"
FROM flights f
JOIN airports d ON d."Id" = f."DepartureAirportId"
JOIN airports a ON a."Id" = f."ArrivalAirportId"
WHERE f."DepartureTimeUtc" > NOW()
ORDER BY f."DepartureTimeUtc"
LIMIT 10;
```

Pick any row and plug its values into a search (replace the example values with yours):

```bash
curl -s "http://localhost:5197/flights?from=AMS&to=LHR&date=2026-11-21"
```

You should get back a JSON array with one or more flights, including `id`, `flightCode`,
`priceUsd`, and `availableSeats`. Keep the first flight's `id` in a shell variable — a seat
on that flight is about to be yours (same query as above, piped through `jq`):

```bash
FLIGHT_ID=$(curl -s "http://localhost:5197/flights?from=AMS&to=LHR&date=2026-11-21" | jq -r '.[0].id')
echo "$FLIGHT_ID"
```

## Hold and book a seat (REST)

Hold a seat:

```bash
curl -s -X POST http://localhost:5197/bookings/hold \
  -H "Content-Type: application/json" \
  -d "{\"flightId\": \"$FLIGHT_ID\", \"seatNumber\": \"12A\"}" | jq
```

> The JSON body is in **double** quotes so the shell expands `$FLIGHT_ID`. Inside single
> quotes it would stay the literal text `$FLIGHT_ID`, which is not a GUID, and the server
> would answer `400 invalid_request` — "Failed to read parameter … from the request body
> as JSON".

The response contains a `holdId` and an `expiresAtUtc` — note that the hold expires **5
minutes** from now. This little timer is going to cause a lot of trouble later today, on
purpose.

Confirm the booking before the hold expires (paste the `holdId` from the response):

```bash
HOLD_ID=<paste the holdId here>
curl -s -X POST http://localhost:5197/bookings/confirm \
  -H "Content-Type: application/json" \
  -d "{\"holdId\": \"$HOLD_ID\"}" | jq
```

You should get back a booking with a `bookingReference` like `BKG-X7K2QM` and status
`Confirmed`. Congratulations — the *application* works. Making it work **as an MCP server,
for an AI agent** is the rest of the day.

## Meet your client: an Azure AI Foundry agent

Every MCP server needs a client. Today yours is an **AI agent running in Azure AI
Foundry**, and you will chat with it in the Foundry portal playground. That choice matters
for a reason worth naming now: the agent runs *in Azure*, not in your Codespace. From the
very first tool call, your server is being called across the public internet by software
you do not control — which is exactly the gap this workshop is about.

We set this up **now**, before you have written a single tool. Provisioning touches
quotas, subscriptions and resource providers, and if something there is going to go wrong
it should go wrong at 9:15 with a proctor free to help, not at 11:30 in the middle of
Lab 2.

### 1. Let Azure reach your Codespace

Your app listens on port 5197, and a forwarded Codespace port is private by default:
only you, signed in to GitHub, can reach it. The Foundry agent calls from Azure, so the
port has to be **public**. This Codespace takes care of that for you — every time it
starts, it publishes port 5197 (and 5001 and 15095, which you will need later).

Your server is therefore already reachable at
`https://$CODESPACE_NAME-5197.app.github.dev`. Confirm it from outside — your phone's
browser works:

```bash
curl -s "https://${CODESPACE_NAME}-5197.app.github.dev/health"
```

```txt
Healthy
```

Not `Healthy`? Then the port is private after all. Publish it by hand:

```bash
gh codespace ports visibility 5197:public -c "$CODESPACE_NAME"
```

> ⚠️ **Remember this command.** Visibility is re-applied when the Codespace starts, but
> it can still end up private — the start-up step can fail, and you can flip it yourself
> in the **Ports** panel. If your agent mysteriously stops working after a break, run it
> before debugging anything else. It is the first entry on the
> [cheat sheet](cheat-sheet.md), together with everything else you restart after a coffee.
>
> The symptom is misleading. A private port doesn't refuse the connection — GitHub's
> front door answers with a redirect to its sign-in page, and Foundry reports that as
> **"Authentication failed when connecting to the MCP server … 401 (Unauthorized)"**.
> That reads like a problem with your agent, your token, or your auth code. It isn't.
> Check the port first.

### 2. Sign in to Azure

```bash
az login --use-device-code
```

Open the printed URL on any device, enter the code, and sign in with the account that owns
your subscription.

### 3. Provision the agent

```bash
./infra/attendee/deploy.sh --mcp-url "https://${CODESPACE_NAME}-5197.app.github.dev/mcp"
```

This creates a resource group, an Azure AI Foundry resource and project, a
`gpt-4.1-mini` deployment capped at 50,000 tokens per minute, and an agent configured to
call your MCP server. It takes a few minutes. It is also **safe to re-run at any time** —
every step checks before it acts, so if you lose your terminal or change the URL later,
just run it again.

> **About that cap.** One playground turn that books a seat is four or five model
> requests, each resending the whole conversation plus the tool schemas, and Azure meters
> the cap in windows of a few seconds rather than per calm minute. If the playground ever
> answers *"exceeded token rate limit"*, start a new thread and wait a minute before
> retrying; if it keeps happening, re-run the script with `--capacity 100` — it updates
> the deployment in place.

When it finishes it prints a **playground link**. Open it.

### 4. Say hello — and expect an error

Ask the agent to find you a flight. The playground answers with an error instead of a
flight:

```txt
The remote MCP server at https://<codespace>-5197.app.github.dev:443/mcp returned
HTTP 404 (Not Found) while enumerating tools.
```

**That is the correct result.** Look at `src/TravelBooking.MCP/Tools/` again: empty since
you ran `prepare-lab.sh`, and nothing maps a `/mcp` endpoint yet — `curl` from your
Codespace gets the same 404. Foundry asks your server for its tool list before it answers
anything, and a server that does not exist at that path cannot answer. Giving it some
tools is Lab 1.

> Some Foundry builds swallow the error and let the agent apologise or answer from
> general knowledge instead. Either way: no tool was called, and your server saw nothing.

> **Provisioning failed?** Read the error — the script tries hard to tell you which of the
> three usual causes you hit (subscription type, quota, or region). The most common is a
> **free trial subscription**, which Microsoft documents as having zero model quota and
> which cannot be granted more. If that is you, **tell a proctor now** rather than retrying:
> there are pre-built fallback environments and switching to one takes about two minutes.

## If things go wrong

- **Database in a weird state?** Reset all bookings, holds and audit entries (flights and
  airports stay):

  ```bash
  curl -s -X POST http://localhost:5197/admin/reset-data
  ```

- **Aspire won't start / ports stuck?** Stop it with `Ctrl+C` and run it again. If that
  doesn't help, ask a proctor.

## Checkpoint ✅

Before moving on to Lab 1, confirm:

- [ ] `./labs/prepare-lab.sh 1` ran and `src/TravelBooking.MCP/Tools/` is empty
- [ ] The Aspire dashboard shows all resources **Running**
- [ ] `curl http://localhost:5197/health` returns `Healthy`
- [ ] You searched flights and confirmed a booking via the REST API
- [ ] Port **5197** shows as **Public** in the VS Code **Ports** panel
- [ ] `deploy.sh` finished and printed a playground link
- [ ] The playground opens and the agent reports a 404 from `/mcp` — no tools, for now

---

*Next up: [Lab 1 — Make It Work](lab-01-make-it-work.md), where you expose these
capabilities to an AI agent.*
