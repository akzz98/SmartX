# Smart-X

Simulated South African IoT gateway for hydroponics, utilities, and site-meter telemetry. Heterogeneous ESP32 packets (float environmental, integer power, Boolean actuator) are ingested by an ASP.NET Core API and investigated in a Blazor WebAssembly operator console.

This repository covers **Part 1 Task 2** (telemetry ingestion + Progressive Disclosure) and official **Part 2** (Real-Time Command Stream: queues, stacks, dictionaries, sets, predictive suggestions). Network Topology / Mesh Routing stays visible but **locked** until the final PoE.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2026 (or `dotnet` CLI)

## Restore, compile, run

Start the **API first**, then the **client**. The dashboard calls `http://localhost:5015/`; if only the client is running, the status strip reports the gateway unreachable.

### Visual Studio

1. Open `SmartX.slnx`.
2. Restore NuGet packages if Visual Studio prompts (or **Build → Restore NuGet Packages**).
3. **Build → Build Solution** and confirm `SmartX.Api`, `SmartX.Client`, and `SmartX.Shared` succeed.
4. Set multiple startup projects: right-click the solution → **Configure Startup Projects…** → **Multiple startup projects** → **Start** for `SmartX.Api` and `SmartX.Client`.
5. Run (F5 or Ctrl+F5).
6. API listens on `http://localhost:5015`. Client opens `http://localhost:5270`.

Use the **http** launch profiles so the browser origin matches CORS (`http://localhost:5270` → `http://localhost:5015`).

### Command line

```bash
dotnet restore SmartX.slnx
dotnet build SmartX.slnx
```

In one terminal:

```bash
dotnet run --project SmartX.Api --launch-profile http
```

In a second terminal:

```bash
dotnet run --project SmartX.Client --launch-profile http
```

Open `http://localhost:5270`. The operator console should show **Gateway Online** and a 16-device seeded fleet.

If the client is served from another origin, add it under `Cors:Origins` in `SmartX.Api/appsettings.Development.json` and set `ApiBaseUrl` in `SmartX.Client/wwwroot/appsettings.json`.

## Architecture

| Project | Role |
| --- | --- |
| `SmartX.Api` | ASP.NET Core Web API. Registration, typed ingest, fleet queries, multipart attachments, command stream (FIFO / priority queues, undo stack, timeline, suggestions). In-memory `GatewayStore` singleton. |
| `SmartX.Client` | Blazor WebAssembly standalone dashboard. Part 1 Progressive Disclosure: overview → filter → device → history. Part 2 Command Stream: overrides, undo, timeline, Suggested Actions. Does not classify anomalies; it displays gateway state. |
| `SmartX.Shared` | Models, generic `TelemetryPacket<T>`, DTOs, validators, health/freshness classifiers, deployment tree. Referenced by both API and client. |

JSON uses camelCase property names and string enums (`"critical"`, not `2`).

Operator paths:

- `/` — three-pillar startup (**ingestion** and **command stream** enabled; mesh locked)
- `/telemetry` — Level 0 overview
- `/telemetry/fleet` — Level 1 filters (executed on the API)
- `/telemetry/devices/{id}` — Level 2 detail, attachments
- `/telemetry/devices/{id}/history` — Level 3 packets for that device only
- `/telemetry/register` and `/telemetry/ingest` — registration and typed ingest
- `/commands` — Part 2 Real-Time Command Stream and History

## Part 2 — how to test queues, dictionaries, and high-priority alerts

Restart **SmartX.Api** to reload all Part 2 demo seeds (`Part2DemoSeeder`, moisture/power priority bypass, HashSet disconnect dedupe, recommendation bootstrap).

### Open the Command Stream

1. Run API + client as above.
2. Open `http://localhost:5270/` → **Real-Time Command Stream and History**, or go to `http://localhost:5270/commands`.
3. Confirm KPIs: FIFO depth / **peak**, priority depth, undo stack, unique active alerts.
4. Confirm **Suggested Actions / Automated Insights** appear without manual warmup.

### Simulate / prove a high-priority alert (priority queue bypass)

Seeds already raise Critical alerts that bypass the FIFO backlog:

| Seed | Device | What you see |
| --- | --- | --- |
| Moisture priority bypass | `sx-env-rack1-moist` | Critical moisture alert; HashSet unique key |
| Power priority bypass | `sx-pwr-inverter` | `lastPriorityBypassReason` mentions power spike |

**API checks:**

```bash
curl http://localhost:5015/api/stream/snapshot
curl http://localhost:5015/api/stream/alerts
```

Look for `lastPriorityBypassReason`, `recentAlerts`, and `uniqueActiveAlertKeys`.

**Manual re-test (optional):**

```bash
curl -X POST http://localhost:5015/api/stream/searches -H "Content-Type: application/json" -d "{\"categoryFilter\":\"Environmental\",\"healthFilter\":\"Critical\",\"alertScope\":\"unique\",\"relatedDeviceId\":\"sx-env-rack1-moist\"}"
```

Then refresh `/commands` or `GET /api/stream/suggestions`.

### Verify FIFO vs priority processing

1. Snapshot field `standardQueuePeakDepth` should be **≥ 12** after boot (FIFO burst seed parked 12 packets, then drained).
2. `standardQueueDepth` is normally `0` after drain.
3. Priority bypass seeds assert FIFO depth does **not** change while a Critical alert is processed on the priority path.

```bash
curl http://localhost:5015/api/stream/snapshot
```

### Issue and undo a manual override (command stack)

Demo actuators: `sx-act-rack1-valve`, `sx-act-rack2-valve`, `sx-act-sump-pump`, `sx-act-feeder-switch`.

**UI:** on `/commands`, select an actuator → choose a `CommandKind` → **Issue override** → **Undo last override**.

**API:**

```bash
curl -X POST http://localhost:5015/api/commands -H "Content-Type: application/json" -d "{\"deviceId\":\"sx-act-rack2-valve\",\"kind\":\"closeValve\",\"operatorNote\":\"readme test\"}"
curl -X POST http://localhost:5015/api/commands/undo
```

Empty undo returns HTTP 400 with `"Nothing to undo — the command stack is empty."`

### Dictionaries and SortedDictionary timeline

- Live devices: O(1) `SensorFleet.ResolveLive` (id / MAC dictionaries).
- Timeline: `SortedDictionary` log via `GET /api/stream/timeline` (oldest first) — also shown on `/commands`.

```bash
curl http://localhost:5015/api/stream/timeline
```

### HashSet unique disconnect / error tracking

```bash
curl http://localhost:5015/api/stream/unique-alerts
```

Expect keys such as `disconnect:sx-env-sump-temp` (duplicate disconnect raises are suppressed). Count also appears as `uniqueActiveAlertCount` / `uniqueActiveAlertKeys` on the snapshot.

### Predictive recommendations

Bootstrapped on API start (moisture → pump search → `StartPump` pattern).

```bash
curl http://localhost:5015/api/stream/suggestions
```

Or open `/commands` and use a **Use …** button under Suggested Actions. Applying filters also `POST`s to `/api/stream/searches` so the sequence map keeps learning.

## Simulated data (seed and reset)

There is no database. `GatewayStore` seeds on construction:

1. Nested site → zone → sub-zone → node forest (`DefaultDeploymentTree`)
2. 16 mixed devices (`FleetSeeder`)
3. Normal streams, FIFO burst (PeakDepth), Part 1 faults (temperature spike, stuck valve, silent sump, malformed pH, feeder outage, recovery)
4. Part 2: moisture + power priority bypass, disconnect HashSet dedupe, actuator override/undo proof, recommendation patterns

**Reset:** stop and start `SmartX.Api`. Registrations, ingest, and attachment *metadata* made after boot are discarded with the process.

Uploaded files are stored under `SmartX.Api/App_Data/attachments/` (gitignored). After an API restart the in-memory attachment list is empty; delete that folder if you also want the bytes gone.

## Demo devices

| Id | What to show |
| --- | --- |
| `sx-act-rack2-valve` | Critical stuck actuator (Part 1) + override/undo demo (Part 2) |
| `sx-act-sump-pump` | Suggested `StartPump` after moisture patterns |
| `sx-env-rack1-moist` | Critical moisture priority bypass |
| `sx-pwr-inverter` | Critical power-spike priority bypass |
| `sx-env-sump-temp` | Disconnected + HashSet disconnect key |
| `sx-env-rack3-ph` | Invalid / rejected frames |
| `sx-pwr-feeder-a` (and neighbours on `node-feeder-1`) | Location-level stale outage |

Health bands and freshness windows are classified in the API (`TelemetryHealthClassifier`, `TelemetryFreshnessClassifier`). Live ≤ 60s, aging ≤ 5 min, stale ≤ 15 min, then disconnected.
