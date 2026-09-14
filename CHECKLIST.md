# Smart-X Part 2 Working Checklist

Use this file as the only sequence until the current implementation is finished. Tick an item `[x]` only after it has been built or tested. Work **one unchecked item at a time**. Do not generate the whole application in one pass.

**Current checkpoint:** Stage 1.3 shared models. Next: location/deployment identity types.

---

## How to use this file

- Do the next unchecked box only. Stop after it. Build or test it. Then tick it.
- After each meaningful item, the student makes a Git commit with a clear message and ticks the matching **Commit** box. Agents must not create commits. The assessment expects **20+ commits per project** (rubric: 25+ well-structured commits to avoid a GitHub deduction).
- Prefer Visual Studio GUI steps unless a terminal command is genuinely needed.
- Explain why each class or feature exists and which rubric item it satisfies.
- Keep the implementation IoT-specific. This is a telemetry gateway, not a generic CRUD app.
- Do not skip rubric items to “finish faster”.
- After ticking a box, the next chat should continue from the first remaining `[ ]`.

---

## Locked decisions (do not reopen)

- Architecture: ASP.NET Core Web API (`SmartX.Api`) + Blazor WebAssembly standalone (`SmartX.Client`) + shared class library (`SmartX.Shared`), all on **.NET 10**.
- Chosen dashboard strategy: **Progressive Disclosure** — overview, then zoom/filter, then details-on-demand.
- Exception status, health, connectivity and freshness are **information shown inside the overview**. They are not additional selected strategies. Do not rename the chosen strategy to “exception-first progressive disclosure”.
- The UI does **not** detect anomalies. Backend validation/state logic classifies packets. The dashboard makes those states visible and investigable.
- Real ESP32 hardware is optional. Use realistic simulated/mock telemetry, seeded heavily enough to prove the data structures under load.
- Startup menu must keep **Real-Time Command Stream and History** disabled until official Part 2, and **Network Topology and Mesh Routing** disabled until the final PoE.
- Part 1 research report is locked unless the lecturer specifically requests changes.

Official naming vs this checklist:

- This checklist covers official **Part 1 Task 2** (implementation, 80 marks). The handover calls this **Part 2**.
- Official **Part 2** (command stream) and the final PoE (mesh routing) are out of scope here except as disabled menu items.

---

## Stage 0 — Hygiene (before domain code)

- [x] Add a `.gitignore` that ignores `bin/`, `obj/`, `.vs/`, user files, other Visual Studio/build artefacts, everything in `Project Assets/`, and `CHECKLIST.md`.
- [x] Stop tracking already-generated build artefacts if they were committed or left untracked (`bin/`, `obj/`, `.vs/`).
- [x] Add a project reference from `SmartX.Api` to `SmartX.Shared`.
- [x] Add a project reference from `SmartX.Client` to `SmartX.Shared`.
- [x] Build the complete solution and confirm 3 projects succeed.
- [x] Commit: repository hygiene and shared project references.

---

## Stage 1.3 — Clean templates and first shared models

Do not dump every domain type in one step.

### Template cleanup

- [x] Remove or replace `SmartX.Shared/Class1.cs`.
- [x] Remove API weather sample (`WeatherForecast.cs`, `WeatherForecastController.cs`) or stop using it as the public API.
- [x] Remove Blazor template pages that are not Smart-X (`Counter.razor`, `Weather.razor`, related nav links, `wwwroot/sample-data/weather.json` if unused).
- [x] Leave a minimal compilable client shell (layout + home placeholder) so the solution still runs.
- [x] Build the solution.
- [x] Commit: remove default template sample code.

### First shared IoT concepts

- [x] Add sensor category enum (e.g. Environmental, Power Consumption, Actuator).
- [x] Add health/exception state enum (normal, warning, critical/abnormal, invalid).
- [x] Add freshness/connectivity enum (live, aging, stale, disconnected).
- [ ] Add location/deployment identity types needed for nested site → zone → node trees.
- [ ] Add sensor/device identity (MAC / unique ID, location, category, timestamps).
- [ ] Build the solution.
- [ ] Commit: shared IoT identity and state enums.

### Generic telemetry wrapper

- [ ] Add `TelemetryPacket<T>` in `SmartX.Shared` so float, int and bool payloads keep their type without boxing.
- [ ] Include packet metadata the gateway will need (device id, timestamp, sequence/quality or equivalent).
- [ ] Build the solution.
- [ ] Commit: add generic `TelemetryPacket<T>`.

---

## Stage 2 — Sensor domain

- [ ] Add the sensor registration model (MAC/unique ID, deployment location, category).
- [ ] Add validation rules for registration (required fields, MAC/id format, valid location in the deployment tree).
- [ ] Add shared request/response DTOs for register / get sensor / list sensors.
- [ ] Build the solution.
- [ ] Commit: sensor registration domain and DTOs.

---

## Stage 3 — Generic telemetry contracts

- [ ] Define typed telemetry payloads for environmental floats (temperature, moisture, pH as appropriate).
- [ ] Define typed telemetry payloads for integer power metrics.
- [ ] Define typed telemetry payloads for Boolean actuator/valve states.
- [ ] Define ingestion contracts that accept `TelemetryPacket<T>` without converting everything to `object`.
- [ ] Build the solution.
- [ ] Commit: typed telemetry payloads and ingestion contracts.

---

## Stage 4 — Gateway API

- [ ] Configure API services (CORS for the Blazor client, JSON options, shared types).
- [ ] Implement sensor registration endpoint(s).
- [ ] Implement telemetry ingest endpoint(s) for heterogeneous packets.
- [ ] Implement retrieval endpoints for fleet summary, filtered sensors, device detail, and history.
- [ ] Validate incoming packets (malformed, missing identity, type/range issues).
- [ ] Calculate health/exception state from values and thresholds.
- [ ] Calculate freshness/connectivity from last-seen timestamps (live / aging / stale / disconnected).
- [ ] Confirm API runs and endpoints respond asynchronously.
- [ ] Commit: gateway registration, ingest, validation and state endpoints.

---

## Stage 5 — Required data structures (used for real work, not demos)

- [ ] Operator overloading on sensor/telemetry types for aggregation or delta comparison (e.g. combined meter load, value deltas). Must have a structural IoT purpose.
- [ ] Use jagged and/or multidimensional arrays for sequential historical batches of raw telemetry **before** promoting them into collections.
- [ ] Transfer those batches into generic `List<T>` (and related collections) for the ingestion/query pipeline.
- [ ] Recursive validation of nested deployment trees (e.g. Sub-Zone → Zone → Facility) with a clear base case.
- [ ] Prefer a custom collection where it genuinely tracks ingestion/fleet state (rubric: collections optimisation).
- [ ] Build the solution.
- [ ] Commit: arrays, collections, recursion and operator overloading.

---

## Stage 6 — Simulation and seeding

Seed enough devices and packets to look like a fleet, not a handful of rows.

- [ ] Seed a mixed fleet (environmental, power, actuator) across nested locations.
- [ ] Seed normal telemetry streams.
- [ ] Seed an anomalous value spike (e.g. temperature or pH excursion).
- [ ] Seed a Boolean actuator stuck in an unexpected state.
- [ ] Seed a sensor that stops transmitting and moves through aging → stale → disconnected.
- [ ] Seed invalid or malformed telemetry packets.
- [ ] Seed a group/location-level connectivity problem so multiple devices go stale together.
- [ ] Seed recovery after an abnormal or disconnected state.
- [ ] Confirm seeded data is visible through the API.
- [ ] Commit: realistic telemetry simulation and fault scenarios.

---

## Stage 7 — Dashboard foundation

- [ ] Point the Blazor `HttpClient` at the API (not only the client host).
- [ ] Replace the template home page with a Smart-X landing/startup menu.
- [ ] Menu pillar: **Sensor Data Ingestion and Telemetry** — enabled.
- [ ] Menu pillar: **Real-Time Command Stream and History** — visible but disabled.
- [ ] Menu pillar: **Network Topology and Mesh Routing** — visible but disabled.
- [ ] Add a technical layout (nav, status area, consistent labels) suitable for operators, not a consumer app.
- [ ] Confirm the client calls the API asynchronously and the solution runs (API + client).
- [ ] Commit: startup interface and API-connected client shell.

---

## Stage 8 — Progressive Disclosure dashboard

Keep one navigation model: overview → filter → details → history.

### Level 0 — Ecosystem overview

Answers: *Which devices need attention?*

- [ ] Fleet/device counts.
- [ ] Health summaries and exception totals.
- [ ] Stale/disconnected totals.
- [ ] Location and category roll-ups.
- [ ] Exception and freshness states remain visible (stale/disconnected devices do not disappear).
- [ ] Commit: overview dashboard.

### Level 1 — Filterable sensor fleet

Answers: *Which subset is affected?*

- [ ] Filter by location.
- [ ] Filter by category.
- [ ] Filter by MAC/unique ID.
- [ ] Filter by health.
- [ ] Filter by freshness.
- [ ] Optional time-window filter.
- [ ] Commit: zoom and filter fleet view.

### Level 2 — Device details

Answers: *What happened?*

- [ ] Latest value, status, thresholds, last-seen timestamp.
- [ ] Device metadata, configuration and packet/error information.
- [ ] Related sensors / location context.
- [ ] Commit: device details view.

### Level 3 — History / logs / configuration

Answers: *Why did it happen?*

- [ ] Telemetry history for the selected device.
- [ ] Supporting diagnostic context (thresholds, related series, error/packet notes).
- [ ] Confirm the path from overview → subset → device → history works without dumping every live stream on one screen.
- [ ] Commit: history and diagnostic drill-down.

---

## Stage 9 — Uploads and diagnostics

- [ ] Allow attaching configuration files, deployment photos, or hardware logs to a sensor profile.
- [ ] Implement API multipart upload and associate files with the sensor.
- [ ] Show attachments on the device/detail view.
- [ ] Handle upload errors without breaking the gateway.
- [ ] Commit: media/log attachment.

---

## Stage 10 — Polish, documentation and submission evidence

### UI and quality

- [ ] Consistent typography, labels and colour encoding for health/freshness.
- [ ] Layout works at standard desktop widths; check a narrower viewport.
- [ ] Crisp feedback for success, validation errors and API failures.
- [ ] Client-side validation on registration/ingestion forms, with matching backend parsing.
- [ ] Error handling for disconnected API / failed uploads / invalid packets.

### Documentation

- [ ] Write `README.md` in Markdown: restore dependencies, compile, boot the API, run the client.
- [ ] Document architecture (Api / Client / Shared) and how to seed or reset simulated data.
- [ ] Optional: Docker files if we choose to include them (suggested, not required).

### Git and GitHub

- [ ] Confirm commit history is frequent, meaningful, and well-structured (target 25+).
- [ ] Create/push the GitHub repository if it is not already remote.
- [ ] Confirm `README.md` is in the repo root and renders as Markdown.

### Rubric audit and demo

- [ ] Walk the rubric tracker below and tick evidence only when it actually exists in the running app.
- [ ] Consult the lecturer on YouTube video vs presentation; record/prepare if required.
- [ ] Final solution build: API and client both compile and run. **If they do not, no functionality marks are awarded.**

---

## Rubric tracker (Part 1 Task 2 — 80 marks + GitHub)

Tick **Evidence in app** only when you can demonstrate it in a running build.

| Criteria | Marks | Evidence in app | Notes |
| --- | --- | --- | --- |
| Gateway startup layout and API integration | 10 | [ ] | Async API calls; three-pillar menu; ingestion enabled |
| Sensor telemetry data ingestion and UI | 10 | [ ] | MAC, location, category; client + API validation |
| Generics and overloading | 10 | [ ] | `TelemetryPacket<T>`; meaningful `+` / delta operators |
| Arrays and recursion | 10 | [ ] | Jagged/multidimensional history batches; recursive tree validation |
| Media / log file upload | 10 | [ ] | Multipart upload on a sensor profile |
| Dynamic dashboard engagement | 10 | [ ] | Progressive Disclosure, live health/freshness/exceptions |
| User interface design | 10 | [ ] | Consistent, labelled, resizes, clear feedback |
| Collections optimisation and lists | 5 | [ ] | Arrays → `List<T>`; custom collection if justified |
| Documentation and README | 5 | [ ] | Restore, build, boot API, run client |
| GitHub history (penalty if weak/missing) | −5 to 0 | [ ] | 25+ commits, Markdown README, remote repo |

Research (Task 1, 20 marks) is already done and locked. Do not rewrite it here.

---

## Out of scope until later

- Official Part 2: Real-Time Command Stream and History (keep the menu item disabled).
- Final PoE: Network Topology and Mesh Routing (keep the menu item disabled).
- Rewriting the locked Part 1 research report.
- Recreating the Visual Studio solution.
- Connecting physical ESP32 hardware (optional extra, never a blocker).

---

## Next action

Start at the first remaining `[ ]` in **Stage 0**.
