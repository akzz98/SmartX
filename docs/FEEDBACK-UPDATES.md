# Smart-X — Updates Based on Lecturer Feedback

## Part 2 feedback

Compound blocks are split into one ledger row per distinct requirement.

### Block A — Client UX

| # | Lecturer point (quote) | Change made | Where in code | How to verify | Status |
| --- | --- | --- | --- | --- | --- |
| P2-1 | “No state survives navigation.” | Added WASM singleton `OperatorSessionState`; Commands / Fleet / Device read and write filters and selections through it instead of page-local-only fields | `SmartX.Client/OperatorSessionState.cs`, `Program.cs` (`AddSingleton`), `Pages/Commands.razor`, `Pages/Fleet.razor`, `Pages/Device.razor` | On `/commands` set Health = Critical → open Overview (`/`) → return to `/commands`; Health filter still Critical | **Done** |
| P2-2 | “Menu is a flat list of pages.” | Sidebar and Home regrouped by pillars: Telemetry · Command stream · Mesh routing (mesh unlocked in Stage 2) | `Layout/NavMenu.razor`, `Pages/Home.razor`, related CSS | Sidebar shows three labelled pillar groups with short hints; Home shows Pillar 1–3 cards and shortcuts | **Done** |
| P2-3 | “No auto-refresh on the command stream.” | `PeriodicTimer` every 5 s on `/commands`; paused while override/undo runs; disposed with the page | `Pages/Commands.razor` (`AutoRefreshSeconds`, timer loop) | Stay on `/commands` without clicking Reload; status line “Auto-refresh every 5 s · last HH:mm:ss” advances | **Done** |

### Block B — Queues and priority

| # | Lecturer point (quote) | Change made | Where in code | How to verify | Status |
| --- | --- | --- | --- | --- | --- |
| P2-4 | “The queues are drained as they are filled. No background worker or separate consumer.” | Ingest/alert paths only enqueue and signal; `TelemetryQueueConsumer` (`BackgroundService`) dequeues priority then FIFO off the request thread | `GatewayStore.TryEnqueueOnly` / `RaisePriorityAlert`, `Storage/QueueWorkSignal.cs`, `Storage/TelemetryQueueConsumer.cs`, `Program.cs` hosted service | API console: consumer started log; after boot `GET /api/commands/snapshot` (or UI KPIs) shows `standardQueueDepth` ≈ 0 while `standardQueuePeakDepth` stayed elevated from the FIFO seed burst | **Done** |
| P2-5 | “No priority tie breaker.” | Priority key `(severityRank, RaisedAtUtcTicks)` — equal severity → earlier `RaisedAt` first | `Storage/CriticalAlertQueue.cs`; proven by `Part2DemoSeeder.SeedPriorityTieBreaker` | API must boot successfully: seed asserts earlier Warning dequeues before later Warning of the same severity | **Done** |

### Block C — Disconnects

| #    | Lecturer point (quote)     | Change made                                                                                                                                                           | Where in code                                                                                                            | How to verify                                                                                                                              | Status   |
| ---- | -------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------ | -------- |
| P2-6 | “No disconnect detection.” | Freshness-based detection: refresh last-seen, raise disconnect alerts for disconnected nodes, clear keys on recovery; consumer polls regularly; HashSet still dedupes | `GatewayStore.DetectAndRaiseDisconnects`, `TelemetryQueueConsumer`, `Part2DemoSeeder.SeedDisconnectDuplicateSuppression` | Unique keys include `disconnect:sx-env-sump-temp`; feeder Stale nodes no false disconnect keys; second raise of the same key is suppressed | **Done** |

### Block D — Suggestions

| #    | Lecturer point (quote)                                                                 | Change made                                                                                                                                        | Where in code                                                                                               | How to verify                                                                                                                                 | Status   |
| ---- | -------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- | -------- |
| P2-7 | “Uses counts, not confidence. Fixed rules dominate — the moisture rule is hard-coded.” | Suggestions expose normalised `Confidence` in 0, 1; moisture→pump hard-code removed; follow-ups come from learned sequence pairs / pattern scoring | `SuggestionItem.Confidence`, `Storage/RecommendationEngine.cs`, `Pages/Commands.razor` Suggested Actions UI | UI/API show confidence `0.00–1.00`; no hard-coded title “Moisture fault → consider sump pump”; `StartPump` can still appear via pattern seeds | **Done** |

---
