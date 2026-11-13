# Smart-X

Simulated South African IoT gateway for hydroponics, utilities, and site-meter telemetry. Heterogeneous ESP32 packets (float environmental, integer power, Boolean actuator) are ingested by an ASP.NET Core API and investigated in a Blazor WebAssembly operator console.

This repository covers **Part 1 Task 2** (telemetry ingestion + Progressive Disclosure), official **Part 2** (Real-Time Command Stream: queues, stacks, dictionaries, sets, predictive suggestions), and Final PoE **Part 3** (Network Topology and Mesh Routing: AVL history trees, graph traversal, Prim MST, topology dashboard).

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — for local `dotnet run` / Visual Studio
- Visual Studio 2026 (or `dotnet` CLI)
- **Optional:** [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Compose v2) — run API + client without installing the SDK on the host

## Restore, compile, run

Start the **API first**, then the **client**. The dashboard calls `http://localhost:5015/` (`ApiBaseUrl` in `SmartX.Client/wwwroot/appsettings.json`). If only the client is running, the status strip reports the gateway unreachable.

| Service | Default URL | Launch profile |
| --- | --- | --- |
| `SmartX.Api` | `http://localhost:5015` | `http` |
| `SmartX.Client` | `http://localhost:5270` | `http` |

CORS allows the Blazor origin `http://localhost:5270` → API `http://localhost:5015`. Prefer the **http** profiles so the browser origin matches that policy.

### Visual Studio

1. Open `SmartX.slnx`.
2. Restore NuGet packages if Visual Studio prompts (or **Build → Restore NuGet Packages**).
3. **Build → Build Solution** and confirm `SmartX.Api`, `SmartX.Client`, and `SmartX.Shared` succeed.
4. Set multiple startup projects: right-click the solution → **Configure Startup Projects…** → **Multiple startup projects** → **Start** for `SmartX.Api` and `SmartX.Client`.
5. Run (F5 or Ctrl+F5).
6. API listens on `http://localhost:5015`. Client opens `http://localhost:5270`.

### Command line

```bash
dotnet restore SmartX.slnx
dotnet build SmartX.slnx
```

Terminal 1 — API:

```bash
dotnet run --project SmartX.Api --launch-profile http
```

Terminal 2 — client:

```bash
dotnet run --project SmartX.Client --launch-profile http
```

Open `http://localhost:5270`. The operator console should show **Gateway Online**, a **16-device** seeded fleet, and all three pillars **Available**.

If the client is served from another origin, add it under `Cors:Origins` in `SmartX.Api/appsettings.Development.json` and set `ApiBaseUrl` in `SmartX.Client/wwwroot/appsettings.json`.

### Quick health check

```bash
curl http://localhost:5015/api/sensors/summary
curl http://localhost:5015/api/stream/snapshot
curl http://localhost:5015/api/mesh/topology
```

## Docker — run API + client in containers

Use this when you want the full Smart-X stack without starting two `dotnet run` terminals. Same host URLs as local development (`5015` / `5270`), so demos and curls stay unchanged.

### What was added

| File | Purpose |
| --- | --- |
| `docker-compose.yml` | Builds and runs `api` + `client` together |
| `SmartX.Api/Dockerfile` | Multi-stage .NET 10 publish → `aspnet` runtime on port **8080** |
| `SmartX.Api/appsettings.Docker.json` | CORS allow-list for `http://localhost:5270` |
| `SmartX.Client/Dockerfile` | Publish Blazor WASM → serve static files with **nginx** |
| `SmartX.Client/nginx.conf` | SPA fallback so `/mesh`, `/commands`, etc. work on refresh |
| `.dockerignore` | Keeps `bin`/`obj`/.vs out of the build context |

### Prerequisites

1. Install and **start Docker Desktop** (Linux engine).
2. Confirm the daemon is up:

```bash
docker version
docker compose version
```

3. Free host ports **5015** and **5270** (stop any local `dotnet run` of Smart-X first).

### Run (recommended)

From the **repository root** (`SmartX/`):

```bash
docker compose up --build
```

First build downloads .NET/nginx base images and can take several minutes. When both services are healthy:

1. Open the operator console: [http://localhost:5270](http://localhost:5270)
2. Confirm the status strip shows **Gateway Online** and **16 devices**
3. Smoke the three pillars: `/telemetry`, `/commands`, `/mesh`

| Compose service | Inside container | On your machine |
| --- | --- | --- |
| `api` (`smartx-api`) | `http://+:8080` | [http://localhost:5015](http://localhost:5015) |
| `client` (`smartx-client`) | nginx `:80` | [http://localhost:5270](http://localhost:5270) |

**How the browser talks to the API:** the WASM app reads `ApiBaseUrl` from `wwwroot/appsettings.json` → `http://localhost:5015/`. That hits the **host-mapped** API port (not the Docker service name). CORS is enabled for `http://localhost:5270` via `appsettings.Docker.json` and `Cors__Origins__0` in Compose.

### Useful Compose commands

```bash
# Build images and start in the foreground (logs in this terminal)
docker compose up --build

# Detached (background)
docker compose up --build -d

# Follow API logs
docker compose logs -f api

# Stop and remove containers
docker compose down

# Rebuild after code changes
docker compose up --build --force-recreate
```

### Build images individually

```bash
# API only
docker build -f SmartX.Api/Dockerfile -t smartx-api .
docker run --rm -p 5015:8080 -e ASPNETCORE_ENVIRONMENT=Docker -e Cors__Origins__0=http://localhost:5270 smartx-api

# Client only (needs API already on :5015 for a working UI)
docker build -f SmartX.Client/Dockerfile -t smartx-client .
docker run --rm -p 5270:80 smartx-client
```

### Health check (same as local)

```bash
curl http://localhost:5015/api/sensors/summary
curl http://localhost:5015/api/stream/snapshot
curl http://localhost:5015/api/mesh/topology
```

### Troubleshooting

| Symptom | Fix |
| --- | --- |
| `failed to connect to the docker API` / pipe error | Start **Docker Desktop** and wait until it is running |
| Port already in use | Stop local API/client (`Ctrl+C`) or change the left side of `ports:` in `docker-compose.yml` |
| Client loads but **Gateway unreachable** | Confirm `api` is up (`docker compose ps` / `curl localhost:5015/api/sensors/summary`) |
| `/mesh` refresh → nginx 404 | Ensure `SmartX.Client/nginx.conf` is copied in the client image (SPA `try_files`) |
| CORS errors in browser console | Check `ASPNETCORE_ENVIRONMENT=Docker` and origin `http://localhost:5270` |

Local `dotnet run` and Docker are alternatives — do not bind both to 5015/5270 at once.

## Architecture

| Project | Role |
| --- | --- |
| `SmartX.Api` | ASP.NET Core Web API. Registration, typed ingest, fleet queries, multipart attachments, command stream (FIFO / priority queues, undo stack, timeline, suggestions), AVL history range queries, mesh topology / BFS reachability / Prim MST. In-memory `GatewayStore` singleton. |
| `SmartX.Client` | Blazor WebAssembly standalone dashboard. Part 1 Progressive Disclosure; Part 2 Command Stream; Part 3 Topology (`/mesh`) — SVG mesh, route tracking, operator briefing, AVL history. Displays gateway state; does not classify anomalies itself. |
| `SmartX.Shared` | Models, generic `TelemetryPacket<T>`, DTOs, validators, classifiers, deployment tree, **AVL history trees**, **mesh graph / traversal / MST**. Referenced by both API and client. |

JSON uses camelCase property names and string enums (`"critical"`, not `2`).

Operator paths:

- `/` — three-pillar startup (telemetry, command stream, mesh)
- `/mesh` — Network Topology dashboard (mesh SVG, MST hop path, operator briefing, AVL history)
- `/telemetry` — Level 0 overview
- `/telemetry/fleet` — Level 1 filters (executed on the API)
- `/telemetry/devices/{id}` — Level 2 detail, attachments
- `/telemetry/devices/{id}/history` — Level 3 packets for that device only
- `/telemetry/register` and `/telemetry/ingest` — registration and typed ingest
- `/commands` — Part 2 Real-Time Command Stream and History

## Unified app — Part 1 + Part 2 still run (with Part 3)

Final PoE did **not** replace earlier work. One process hosts all three pillars against the same in-memory gateway.

| Pillar | What still works | Where |
| --- | --- | --- |
| **Part 1 — Telemetry** | Register / ingest, Progressive Disclosure L0–L3, health & freshness, attachments, seeded faults | `/telemetry…`, `SensorsController`, `TelemetryController` |
| **Part 2 — Command stream** | FIFO + priority queues, undo stack, timeline, HashSet unique alerts, suggestions, auto-refresh UI | `/commands`, `StreamController`, `CommandsController`, `TelemetryQueueConsumer` |
| **Part 3 — Mesh** | AVL range history, topology graph, BFS reachability, Prim MST, `/mesh` dashboard | `/mesh`, `MeshController`, Shared AVL + mesh types |

**Shared state.** `GatewayStore` is a singleton: the same 16 ESP32s, ingest history, queues, and alerts feed Part 1 views, Part 2 stream KPIs, and Part 3 mesh online/offline flags. Ingest still validates packets and writes AVL trees; disconnect freshness still marks mesh nodes down.

**Shared UI shell.** `/` is a three-pillar startup menu. The sidebar keeps **Telemetry**, **Command stream**, and **Mesh routing** groups (lecturer feedback: not a flat page list). Operator filter choices on `/commands` persist via `OperatorSessionState` when you navigate away and back.

**One boot = full demo.** After `dotnet run` on API + client you can, without rebuilding:

1. Open `/telemetry` — fleet overview and exception devices (Part 1).
2. Open `/commands` — FIFO/priority KPIs, suggestions, issue/undo override (Part 2).
3. Open `/mesh` — SVG mesh, MST path, AVL history for the same moist sensor (Part 3).

**Same seeds, richer story.** Part 1 silent sump and Part 2 inverter alerts also appear as mesh-down / HashSet keys; Part 3’s `MeshDownScenarioSeeder` ages the inverter so BFS healing is visible on the topology page without breaking the command-stream demo.

## Part 3 — Network Topology and Mesh Routing (how to demo)

Restart **SmartX.Api** so mesh seeds reload: full undirected mesh (`MeshTopologySeeder`), forced mid-mesh relay down (`MeshDownScenarioSeeder` → `sx-pwr-inverter`), plus the existing silent sump disconnect.

### UI walkthrough (`/mesh`)

1. Open `http://localhost:5270/` → **Network Topology and Mesh Routing**, or go to `http://localhost:5270/mesh`.
2. **Active mesh** — confirm ~17 nodes / ≥20 links; down nodes (inverter + sump) in red.
3. **Track route** — select `sx-env-rack1-moist` (or enter its MAC) → **Track route to gateway**. The SVG highlights the Prim MST hop path toward `sx-gateway`.
4. **Operator routing briefing** — readable reachability (reachable / down / cut-off), suggested detours, and full-mesh vs MST cost savings (~25 links → 16, ~47% radio cost saved).
5. **Historical log** — same selected sensor auto-loads an AVL range query (**Use full tree window**). Check **Matches**, **AVL tree size**, **Nodes visited**.

### Quick API checks (Part 3)

```bash
curl "http://localhost:5015/api/sensors/sx-env-rack1-moist/history/range?metric=Moisture"
curl http://localhost:5015/api/mesh/topology
curl http://localhost:5015/api/mesh/traversal
curl http://localhost:5015/api/mesh/mst
curl "http://localhost:5015/api/mesh/mst?onlineOnly=true"
```

Expect topology/traversal down lists to include `sx-pwr-inverter` (mesh-down seed) and usually `sx-env-sump-temp` (silent sensor). MST comparison should show fewer edges and lower total cost than the full mesh.

## Part 3 — Trees, Graphs, and MST in depth

### Trees — AVL historical telemetry

**Role in Smart-X.** Every accepted ingest writes into a **per-device** AVL tree (`TelemetryHistoryIndex` → `AvlTelemetryHistoryTree`). Keys are timestamp then sequence, so “moisture for Node A between 14:00–16:00” is a tree range walk, not a linear scan of that device’s packet `List`.

**Efficiency contribution.** A naïve history scan is **O(n)** per query (touch every stored sample). The AVL stays height-balanced on insert, so locating the window is **O(log n)** and collecting *k* matches is **O(k)**. The API returns `nodesVisited` vs `treeSize` so the `/mesh` historical log can prove the walk stayed narrower than a full scan when the window is tight.

**Relevant code** (`SmartX.Shared/Collections/AvlTelemetryHistoryTree.cs`):

```csharp
// Insert keeps the tree balanced (rotations in Insert / Balance).
public void Insert(TelemetryHistoryRecord record)
{
    _root = Insert(_root, record);
    Count++;
}

// Range walk prunes subtrees that cannot intersect [from, to].
private static void CollectRange(
    Node? node, DateTimeOffset from, DateTimeOffset to,
    string? metric, List<TelemetryHistoryRecord> results, ref int nodesVisited)
{
    if (node is null) return;
    nodesVisited++;
    var ts = node.Record.Timestamp;
    if (ts >= from) CollectRange(node.Left, from, to, metric, results, ref nodesVisited);
    if (ts >= from && ts <= to && MetricMatches(node.Record, metric))
        results.Add(node.Record);
    if (ts <= to) CollectRange(node.Right, from, to, metric, results, ref nodesVisited);
}
```

Operator surface: `GET /api/sensors/{id}/history/range` and the **Historical log** panel on `/mesh`.

### Graphs — mesh adjacency, BFS/DFS, healing hops

**Role in Smart-X.** The site radio network is an undirected **adjacency-list graph** (`MeshGraph`): vertices = `sx-gateway` + ESP32s; edges = wireless links with a non-negative **power / RSSI-style weight**. That model answers “who is connected to the gateway subnet?”, “which relays are down?”, and “what is the next live hop around a fault?”

**Efficiency contribution.** Listing neighbours is **O(degree)** from the adjacency list (not an edge-matrix scan). **BFS** from the gateway discovers reachable devices in hop order (**O(V + E)**) and yields shortest hop-count paths for healing. A second BFS with `onlineOnly: true` skips down relays so cut-off sensors (online but stranded) become visible. DFS is available for deep branch exploration; reachability reporting prefers BFS because hop distance is meaningful for routing.

**Relevant code** (`MeshGraphTraverser`, `MeshReachabilityAnalyzer`):

```csharp
// Live routing walk — do not expand through offline neighbours.
public static MeshTraversalResult BreadthFirst(
    MeshGraph graph, string? rootId = null, bool onlineOnly = false)
    => Traverse(graph, rootId, MeshTraversalMode.BreadthFirst, onlineOnly);

// Compare topology BFS vs live BFS → down / cut-off + hop suggestions.
var topology = MeshGraphTraverser.BreadthFirst(graph, root, onlineOnly: false);
var live = MeshGraphTraverser.BreadthFirst(graph, root, onlineOnly: true);
```

Operator surface: `GET /api/mesh/topology`, `GET /api/mesh/traversal`, SVG mesh + **Operator routing briefing** on `/mesh`. Seeded fault: `MeshDownScenarioSeeder` forces `sx-pwr-inverter` offline so alternate hops appear immediately.

### MST — Prim’s power-efficient backbone

**Role in Smart-X.** The full mesh keeps redundant links for resilience; day-to-day telemetry should prefer a **minimum spanning tree** of those weighted edges so every device still reaches the gateway at the **lowest total radio cost**. Parent hops from the MST are what `/mesh` highlights when you track a sensor.

**Why Prim (not Kruskal) here.** The tree is **rooted at the gateway**; Prim grows outward from that root so `Parents[node]` is naturally “next hop toward the gateway”. The graph is already an adjacency list, which Prim uses directly; Kruskal would need a global edge sort + Union-Find. With ~17 vertices, a clear **O(V·E)** Prim pass is enough for demos and marking evidence.

**Efficiency contribution.** Seeded full mesh ≈ **25** links (cost ≈ **42.7**); Prim MST ≈ **16** links (cost ≈ **22.7**) — about **9** redundant hops dropped and ~**47%** radio cost saved while remaining connected. `MeshVsMstComparison` on the MST API (and the operator briefing) exposes those numbers plus the omitted edges.

**Relevant code** (`SmartX.Shared/Mesh/MeshMstBuilder.cs`):

```csharp
/// Algorithm choice — Prim’s (not Kruskal’s):
/// - Routing tree is rooted at the gateway; parent hops fall out naturally.
/// - Adjacency list matches Prim’s “grow from in-tree vertices” scan.
public static MeshMstResult Build(
    MeshGraph graph, string? rootId = null, bool onlineOnly = false)
{
    // Prim key: cheapest edge from the grown tree to each outsider.
    // onlineOnly: true → live backbone used by route tracking on /mesh.
    ...
}
```

Operator surface: `GET /api/mesh/mst` (design tree + comparison), `?onlineOnly=true` (live tree for route tracking).

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
5. Part 3: undirected mesh (≥20 weighted links) + forced inverter relay down for BFS healing / MST demos

**Reset:** stop and start `SmartX.Api`. Registrations, ingest, and attachment *metadata* made after boot are discarded with the process.

Uploaded files are stored under `SmartX.Api/App_Data/attachments/` (gitignored). After an API restart the in-memory attachment list is empty; delete that folder if you also want the bytes gone.

## Demo devices

| Id | What to show |
| --- | --- |
| `sx-act-rack2-valve` | Critical stuck actuator (Part 1) + override/undo demo (Part 2) |
| `sx-act-sump-pump` | Suggested `StartPump` after moisture patterns |
| `sx-env-rack1-moist` | Critical moisture priority bypass; `/mesh` AVL range + MST hop-path demo |
| `sx-pwr-inverter` | Critical power-spike priority bypass + **Part 3 mesh relay down** (alternate hops) |
| `sx-env-sump-temp` | Disconnected + HashSet disconnect key (also offline on the mesh) |
| `sx-env-rack3-ph` | Invalid / rejected frames |
| `sx-pwr-feeder-a` (and neighbours on `node-feeder-1`) | Location-level stale outage |

Health bands and freshness windows are classified in the API (`TelemetryHealthClassifier`, `TelemetryFreshnessClassifier`). Live ≤ 60s, aging ≤ 5 min, stale ≤ 15 min, then disconnected.
