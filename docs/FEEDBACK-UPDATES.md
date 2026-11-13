# Smart-X — Updates Based on Lecturer Feedback

**Purpose.** Final PoE submission ledger for the rubric item *Iterative feedback incorporation*. Every lecturer critique point is mapped to what changed, where in the codebase, and how a marker can verify it.

**Sources**

| Source | Location | Coverage |
| --- | --- | --- |
| Part 2 criterion feedback (screenshot) | `PROG7312/POE/Lecturer Feedback.png` (campus POE pack) | Four criterion blocks quoted below |
| Part 1 written criterion feedback | *Not present* in the PROG7312 POE pack (only `POE.pdf`, `Rubric.pdf`, `Lecturer Feedback.png`) | See Part 1 section |

**Status summary:** Part 1 — N/A (none received) · Part 2 — **7/7 points Done** · Mesh pillar unlocked after feedback fixes (Stage 2; not a lecturer critique item).

---

## Part 1 feedback

| Lecturer point | Change made | Where in code | How to verify | Status |
| --- | --- | --- | --- | --- |
| No discrete Part 1 criterion feedback was issued in the Final PoE pack | No Part 1–only remediation ticket. Part 1 telemetry (Progressive Disclosure, health/freshness, ingest) remains live. Shared Final PoE shell work that also improves Part 1 UX: pillar-grouped nav and `OperatorSessionState` on Fleet/Device | `SmartX.Client/Pages/Fleet.razor`, `Device.razor`, `Layout/NavMenu.razor`, `Pages/Home.razor` | Open `/telemetry` and a device detail page after navigating from `/commands`; Part 1 still functions with Part 2/3 in the same app | **N/A — none received** |

If oral Part 1 notes are supplied later, add rows above this line and mark them Done when verified.

---

## Part 2 feedback

Source quotes are from `Lecturer Feedback.png` (four Criterion Feedback blocks). Compound blocks are split into one ledger row per distinct requirement.

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

| # | Lecturer point (quote) | Change made | Where in code | How to verify | Status |
| --- | --- | --- | --- | --- | --- |
| P2-6 | “No disconnect detection.” | Freshness-based detection: refresh last-seen, raise disconnect alerts for disconnected nodes, clear keys on recovery; consumer polls regularly; HashSet still dedupes | `GatewayStore.DetectAndRaiseDisconnects`, `TelemetryQueueConsumer`, `Part2DemoSeeder.SeedDisconnectDuplicateSuppression` | Unique keys include `disconnect:sx-env-sump-temp`; feeder Stale nodes do not create false disconnect keys; second raise of the same key is suppressed | **Done** |

### Block D — Suggestions

| # | Lecturer point (quote) | Change made | Where in code | How to verify | Status |
| --- | --- | --- | --- | --- | --- |
| P2-7 | “Uses counts, not confidence. Fixed rules dominate — the moisture rule is hard-coded.” | Suggestions expose normalised `Confidence` in \[0, 1\]; moisture→pump hard-code removed; follow-ups come from learned sequence pairs / pattern scoring | `SuggestionItem.Confidence`, `Storage/RecommendationEngine.cs`, `Pages/Commands.razor` Suggested Actions UI | UI/API show confidence `0.00–1.00`; no hard-coded title “Moisture fault → consider sump pump”; `StartPump` can still appear via pattern seeds | **Done** |

---

## Quick verification (marker path)

With Api on `http://localhost:5015` and Client on `http://localhost:5270` (or Docker Compose equivalents):

1. **P2-1 / P2-2 / P2-3** — Browser: `/commands` filter persist + pillar nav + auto-refresh clock.  
2. **P2-4 / P2-5** — Api startup logs + command-stream KPIs (peak vs depth); tie-breaker seed must not throw.  
3. **P2-6** — Unique alert keys on `/commands` include the silent-sump disconnect.  
4. **P2-7** — Suggested Actions show confidence; no moisture hard-code dominating the list.  
5. **Regression** — `/telemetry` Progressive Disclosure and `/mesh` topology still load after the above.

---

## Change log (newest first)

| Date | Checklist / commits | Summary |
| --- | --- | --- |
| 2026-11-13 | Stage **8.9** | Ledger finalised for submission: Part 1 N/A documented; Part 2 split to P2-1…P2-7 with quote → code → verify |
| 2026-11-12 | `f4263c7` | Disconnect detection + confidence-based suggestions |
| 2026-11-12 | `2e7cc93` | Background `TelemetryQueueConsumer` + work signal (enqueue-only ingest) |
| 2026-11-12 | `63c2027`, `be33c1a`, `1a3db6f` | Operator session state, pillar nav, command-stream auto-refresh |
| 2026-11-12 | Stage 0.3 | Ledger stub created |

---

## Notes for submission

- Pair this file with the live repo: all **Done** rows are implemented on the unified Part 1+2+3 app (not a separate patch branch).  
- Student commit for the docs pack (Stage **8.10**): e.g. `docs: Final PoE README, reports, and feedback ledger`.  
- Do not leave Part 2 rows as Pending when submitting the Final PoE.
