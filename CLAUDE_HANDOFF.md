# SimTrackerV2 — Full Handoff (April 2026)

This is a complete rewrite of the build direction incorporating all decisions made to date. Treat this as the authoritative spec going forward.

## Repo
**GitHub:** github.com/simcrewops/SimTrackerV2
**Local:** `/Users/daryarafizadeh/Documents/Codex_SimCrewOps`

---

## What's Built and Passing CI

### Domain Core (complete — do not change contracts)
- **SimCrewOps.Scoring** — scoring engine, grades A–F, 100 pts across 10 sections
- **SimCrewOps.Tracking** — telemetry accumulator, builds FlightScoreInput from phase-labeled frames
- **SimCrewOps.PhaseEngine** — 10-phase state machine (Preflight → TaxiOut → Takeoff → Climb → Cruise → Descent → Approach → Landing → TaxiIn → Arrival), emits BlocksOff / WheelsOff / WheelsOn / BlocksOn, handles go-around / aborted takeoff / touch-and-go
- **SimCrewOps.Runways** — runway resolver + touchdown zone calculator
  - IRunwayDataProvider interface
  - FallbackRunwayDataProvider (primary → OurAirports fallback)
  - OurAirportsCsvRunwayDataProvider (bundled CSV, indexed by ICAO, displaced-threshold-aware)
  - RunwayResolver (matches runway by heading at touchdown, ±30° tolerance)
  - TouchdownZoneCalculator (projects touchdown onto centerline, FAA 3,000 ft zone rule)
- **SimCrewOps.Runtime** (commit f2dfa9a) — RuntimeCoordinator wiring full pipeline
  - ProcessFrameAsync — frame-by-frame telemetry pipeline
  - At WheelsOn: resolves runway, injects TouchdownZoneExcessDistanceFeet into enriched frame
  - Returns RuntimeFrameResult (phase frame, enriched telemetry, runway resolution, runtime state snapshot)
  - ArrivalAirportIcao lives in FlightSessionContext — NOT in FlightScoreInput
- **SimCrewOps.Persistence** (commit de21985) — file-based session persistence
  - FileSystemFlightSessionStore — atomic temp-file writes to current-session.json (crash-safe)
  - Queues completed sessions as individual JSON files for upload
  - IFlightSessionStore interface

### CI
`.github/workflows/dotnet-tests.yml` — builds and tests on Ubuntu and Windows

---

## Locked Contracts (do not modify without flagging)
- `FlightScoreInput` — organized by phase, immutable
- `TelemetryFrame` — GateArrivalDistanceFeet kept but marked reserved
- `ArrivalAirportIcao` → FlightSessionContext only, not scoring contract
- `TouchdownZoneExcessDistanceFeet` → injected from RunwayResolver into FlightScoreInput.Landing

---

## Scoring Spec (locked — Dar-approved April 2026)

### Grade Scale (A–F only — no A+)
- A: 90–100 | B: 80–89 | C: 70–79 | D: 60–69 | F: below 60

### Section Weights (100 pts total)
| Section | Points |
|---|---|
| Preflight | 5 |
| Taxi Out | 8 |
| Takeoff | 10 |
| Climb | 10 |
| Cruise | 10 |
| Descent | 10 |
| Approach | 12 |
| Landing | 20 |
| Taxi In | 8 |
| Arrival | 7 |

### Global Safety Deductions
- Overspeed event: 3 pts each, max 9
- Sustained overspeed (30s+): 5 pts each, max 10
- Stall warning: 3 pts each, max 9
- GPWS alert: 3 pts each, max 9
- Engine shutdown in flight: 5 pts each, no cap
- **Crash/sim reset: whole-flight F — the ONLY whole-flight fail condition**

### Section Fail vs Flight Fail
- Section fail = that section scores 0, rest of flight scores normally
- Landing section fails (section only) if touchdown VS > 600 fpm OR G-force > 2.0G
- Crash/sim reset = whole-flight F

### Phase Rules

**PREFLIGHT:** Beacon light ON before taxi starts

**TAXI OUT:** GS < 40 kt. Turns >45°: GS must be < 15 kt. Taxi lights ON.

**TAKEOFF:** Bounce = lifts off then returns to runway (<100 ft AGL, still in takeoff context). Tail strike = OnGround AND pitch ≥ 10° AND IAS ≥ 40 kt. Bank max 30°. Pitch max 20°. G-force: full credit ≤1.5G, linear deduction 1.5–2.5G, max 3 pts. Landing lights ON before takeoff. Landing lights OFF before FL180. Strobes ON from takeoff through landing.

**CLIMB:** Speed below FL100 max 250 kt (heavy 4-engine exception: 300 kt). Bank max 30°. G-force: full credit ≤1.7G, linear 1.7–2.5G, max 3 pts.

**CRUISE:** Altitude hold ±100 ft. Auto-detect new FL after 60s stable. Speed instability: IAS delta >15 kt OR Mach >0.03 sustained 5s, 10s cooldown, 1 pt per event max 4 pts. G-force: full credit ≤1.4G, linear 1.4–2.0G, max 2 pts.

**DESCENT:** Speed below FL100 max 250 kt. Pitch max 20°. G-force: full credit ≤1.7G, linear 1.7–2.5G, max 3 pts. Landing lights ON from FL180 to landing.

**APPROACH (FL100 to 2000 AGL):** Gear down by 1000 AGL. Flaps > 1. Stabilized by 500 AGL: VS ≤1000 fpm, bank ≤10°, pitch ≤10°, gear down, flaps >1.

**LANDING:**
- Touchdown zone: deduct based on excess distance outside TDZ (no section fail for this alone)
- Touchdown VS: ≤300 fpm = full credit. 300–600 fpm = gradual linear deduction. >600 fpm = Landing section F
- Touchdown G-force: ≤1.2G = full credit. 1.2–2.0G = gradual linear deduction. >2.0G = Landing section F
- Use 2-second rolling buffer before touchdown for VS and G capture
- Landing bounce = touchdown → airborne → touchdown again within 3 seconds

**TAXI IN:** Landing lights OFF. Strobes OFF. GS < 40 kt. Turns >45°: GS < 15 kt. Taxi lights ON.

**ARRIVAL (sequence matters):**
1. Taxi lights OFF before parking brake set
2. Parking brake set before ALL engines off (one engine may shut down before PB — single-engine taxi is acceptable; violation = ALL engines off before PB)
3. All engines off by end of session

### Bounce Concepts (must NOT be merged)
- Takeoff bounce: liftoff during takeoff → returns to runway before establishing climb
- Landing bounce: touchdown → airborne → touchdown again within 3 seconds post-landing

### Gradual Threshold Formula
```
value <= A                   → no deduction (full credit)
value > B (section-fail cap) → section failed
otherwise                    → deduction = max_metric_points × ((value - A) / (B - A))
```

---

## What to Build Next — Priority Order

### 1. Wire Runtime → Persistence (build this first)

`RuntimeCoordinator` needs to drive `FileSystemFlightSessionStore`:

- **Each frame**: autosave current-session.json throttled to max 1 write/second, only when state has changed meaningfully
- **On Arrival phase**: call `StoreCompletedSessionAsync()` to queue the session, then clear current-session.json
- **On session discard**: call `DiscardCurrentSessionAsync()`
- **On startup**: check for existing current-session.json — if found, offer resume or discard (crash recovery)

Introduce a `TrackerOrchestrator` (or extend RuntimeCoordinator) that owns this lifecycle and exposes:
```csharp
Task StartSessionAsync(FlightSessionContext context);
Task ProcessFrameAsync(TelemetryFrame frame);
Task DiscardSessionAsync();
// Arrival phase triggers auto-queue internally
```

### 2. API Sync Layer ← ALREADY BUILT

`SimCrewOps.Sync` is complete. Do not rebuild. What exists:

- `ICompletedSessionUploader` / `HttpCompletedSessionUploader` — POSTs to `https://simcrewops.com/api/sim-sessions` with Bearer token auth
- `ICompletedSessionSyncService` / `CompletedSessionSyncService` — sweep-and-upload with retry logic
- `SimSessionUploadRequestMapper` — maps `PendingCompletedSession` → `SimSessionUploadRequest`
- `SimCrewOpsApiUploaderOptions` — BaseUri, path, PilotApiToken, TrackerVersion
- `BackgroundSyncCoordinator` in Hosting — background service driving sync
- Full test coverage in `SimCrewOps.Sync.Tests`

**Known field mismatch to fix before going live:**  
`SimSessionUploadRequest` sends `"bounces"` but the web backend route (`sim-sessions/route.ts`) reads `bounceCount`. This will silently drop bounce data. Fix by either:
- Changing `[JsonPropertyName("bounces")]` → `[JsonPropertyName("bounceCount")]` in `SimSessionUploadRequest.cs`, OR
- Fixing the server to read `bounces`

Confirm which side to fix with Dar — prefer fixing on the C# side since the server field name is `bounceCount` everywhere else.

### 3. SimConnect Adapter (Windows, MSFS 2020/2024)

Named pipe connection, Protocol.KittyHawk. C# native.

Build `ISimConnectAdapter`:
```csharp
Task ConnectAsync();
Task DisconnectAsync();
event EventHandler<TelemetryFrame> FrameReceived;
event EventHandler ConnectionStateChanged;
```

SimVars to poll (1Hz minimum, 4Hz preferred during landing phase):
- PLANE LATITUDE, PLANE LONGITUDE, PLANE ALTITUDE
- PLANE ALT ABOVE GROUND
- AIRSPEED INDICATED, VERTICAL SPEED
- PLANE HEADING DEGREES MAGNETIC
- GROUND VELOCITY
- G FORCE
- PLANE BANK DEGREES, PLANE PITCH DEGREES
- SIM ON GROUND
- GEAR HANDLE POSITION, FLAPS HANDLE INDEX
- AUTOPILOT MASTER, AUTOPILOT ALTITUDE LOCK VAR
- LIGHT BEACON ON, LIGHT TAXI ON, LIGHT LANDING ON, LIGHT STROBE ON
- NUMBER OF ENGINES, ENGINE CONTROL (per engine)
- BRAKE PARKING INDICATOR

Adapter's only job = produce `TelemetryFrame` structs. No business logic inside the adapter.

### 4. SimConnectFacilityRunwayProvider

SimConnect Facility API as primary runway source. OurAirports CSV stays as fallback. The slot is already modeled — needs implementation only.

### 5. WPF Tray App

Windows tray-first. System tray icon with status indicator. Opens main window on click.

**CRITICAL UI DECISION:** The approach spatial diagram has been removed from the tray app. It was too small to be actionable mid-flight. Instead:

**During Approach phase**, the Live Ops Board shows a live numeric grid updating ~1s:
```
IAS         142 kt     VS       -680 fpm ⚠
ALT AGL   1,900 ft     G/S      -0.2 dot
DIST THR    4.2 nm     LOC      +0.1 dot
```
Flag unstable values with ⚠. This is faster to read mid-approach than any spatial diagram.

**The approach visualization (top-down track plot, vertical profile with 3° slope overlay, touchdown marker on runway) belongs in the SimCrewOps web app as a post-flight debrief component.** The tracker records lat/lon track + telemetry — the web app renders it after flight.

**WPF Main Window Layout:**
```
┌──────────────────────────────────────────────────────────┐
│ SimCrewOps Tracker           ● MSFS CONNECTED  ● SYNCED  │
│ DAL1423 · JFK→MIA · A320neo · Captain Step 3             │
│ [Legacy Tier]  [Bid #48291]  [Rep 1.10x]                 │
├────────────────────┬─────────────────────────────────────┤
│ FLIGHT STACK       │ LIVE OPS BOARD                      │
│                    │                                     │
│ Career Status      │  [Phase-appropriate live display]   │
│ Legacy Tier        │  See phase display rules below      │
│ Bid #48291         │                                     │
│ Rep 1.10x          │                                     │
│                    │                                     │
│ FLIGHT TIMES       │                                     │
│ OUT  19:45         │                                     │
│ OFF  19:58         │                                     │
│ ON   --:--         │                                     │
│ IN   --:--         │                                     │
│                    ├─────────────────────────────────────┤
│ PHASE RAIL         │ SCOREBOARD                          │
│ ● PREFLT TAXI...   │ 88  Grade B                         │
│   CLB CRZ DES...   │ Section score bars (8px min height) │
│   APP LDG TAXI IN  │                                     │
│   ARR              │                                     │
├────────────────────┴─────────────────────────────────────┤
│  [Dispatch]  [PIREP Draft]  [Diagnostics]  [Settings]    │
└──────────────────────────────────────────────────────────┘
```

**Phase Display Rules for Live Ops Board:**
- Preflight/TaxiOut/TaxiIn: Ground speed, heading, phase status, light compliance
- Takeoff: IAS, VS, pitch, bank, G-force live
- Climb/Cruise/Descent: Altitude, IAS (or Mach above FL280), VS, heading
- Approach: The 6-metric numeric grid above
- Landing: VS and G-force at touchdown — held on screen for 10s after WheelsOn
- Arrival: PB status, engine status, light compliance ticking off as completed

**WPF UI Rules:**
- Bottom nav must have a proper active state — 2px accent bottom border, not just text
- Left panel: use hairline section dividers (not separate cards) between Career Status / Flight Times / Phase Rail
- Score bars: 8px minimum height, adequate vertical spacing between rows
- Legible at 1080p

---

## Data Flow Summary
```
MSFS 2024
  ↓ SimConnect (named pipe, Protocol.KittyHawk)
SimConnectAdapter → TelemetryFrame
  ↓
TrackerOrchestrator / RuntimeCoordinator.ProcessFrameAsync()
  ├→ FlightPhaseEngine (phase transitions + events)
  ├→ FlightSessionScoringTracker (accumulates FlightScoreInput)
  ├→ RunwayResolver (at WheelsOn → TDZ calculation)
  ├→ ScoringEngine (scores per phase → FlightScoreResult)
  └→ FileSystemFlightSessionStore (autosave current-session.json)
         ↓ on Arrival phase
     PendingCompletedSession queue
         ↓
     SimCrewOpsApiClient → SimCrewOps web backend
         ↓ post-flight
     Web app: approach visualization, score debrief, PIREP generation
```

---

## NOT Building in Tracker (belongs in web app)
- Approach path diagram — render from stored telemetry in web debrief
- Full PIREP editor — tracker captures notes/flags only
- Career management — tracker shows read-only context only
- Gate arrival precision — no gate database; arrival scoring uses operational sequence only

---

## Confirmed Architecture Decisions
- Named pipe only for SimConnect (no TCP). Protocol.KittyHawk.
- OurAirports CSV as runway fallback — bundled, no network dependency
- SimConnect Facility API as primary runway source (NOT X-Plane apt.dat)
- ArrivalAirportIcao in FlightSessionContext only — never in FlightScoreInput
- No whole-flight fail except crash/sim reset
- Landing section fail (not whole-flight) for VS >600 fpm or G >2.0G
- A–F grading only — no A+

---

**Start with task 1: wire runtime → persistence. When done, move to task 2 (API sync stub). Do not implement HTTP calls until Dar confirms the backend endpoint.**
