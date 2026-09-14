# Smart-X

Simulated South African IoT gateway for hydroponics, utilities, and site-meter telemetry. Heterogeneous ESP32 packets (float environmental, integer power, Boolean actuator) are ingested by an ASP.NET Core API and investigated in a Blazor WebAssembly operator console.

This repository is **Part 1 Task 2** (implementation). Real-Time Command Stream and Network Topology / Mesh Routing stay visible in the startup menu but disabled until later assessments.

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
| `SmartX.Api` | ASP.NET Core Web API. Registration, typed ingest, fleet queries, multipart attachments. In-memory `GatewayStore` singleton. |
| `SmartX.Client` | Blazor WebAssembly standalone dashboard. Progressive Disclosure: overview → filter → device → history. Does not classify anomalies; it displays gateway health, freshness, and rejections. |
| `SmartX.Shared` | Models, generic `TelemetryPacket<T>`, DTOs, validators, health/freshness classifiers, deployment tree. Referenced by both API and client so forms and endpoints share the same rules. |

JSON uses camelCase property names and string enums (`"critical"`, not `2`).

Operator paths:

- `/` — three-pillar startup (ingestion enabled; command stream and mesh locked)
- `/telemetry` — Level 0 overview
- `/telemetry/fleet` — Level 1 filters (executed on the API)
- `/telemetry/devices/{id}` — Level 2 detail, attachments
- `/telemetry/devices/{id}/history` — Level 3 packets for that device only
- `/telemetry/register` and `/telemetry/ingest` — MAC/location/category registration and typed ingest, validated in the browser and again on the API

## Simulated data (seed and reset)

There is no database. `GatewayStore` seeds on construction:

1. Nested site → zone → sub-zone → node forest (`DefaultDeploymentTree`)
2. 16 mixed devices (`FleetSeeder`)
3. Normal streams, then faults: temperature spike with recovery, stuck valve `sx-act-rack2-valve`, silent sump `sx-env-sump-temp`, malformed packets on `sx-env-rack3-ph`, feeder-1 location outage

**Reset:** stop and start `SmartX.Api`. Registrations, ingest, and attachment *metadata* made after boot are discarded with the process.

Uploaded files are stored under `SmartX.Api/App_Data/attachments/` (gitignored). After an API restart the in-memory attachment list is empty; delete that folder if you also want the bytes gone.

## Demo devices

| Id | What to show |
| --- | --- |
| `sx-act-rack2-valve` | Critical stuck actuator (commanded open, reports closed) |
| `sx-env-sump-temp` | Disconnected (last sample ~20 minutes ago) |
| `sx-env-rack3-ph` | Invalid / rejected frames |
| `sx-pwr-feeder-a` (and neighbours on `node-feeder-1`) | Location-level stale outage |

Health bands and freshness windows are classified in the API (`TelemetryHealthClassifier`, `TelemetryFreshnessClassifier`). Live ≤ 60s, aging ≤ 5 min, stale ≤ 15 min, then disconnected.
