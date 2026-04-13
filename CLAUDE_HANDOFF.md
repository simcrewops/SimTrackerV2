# SimTrackerV2 — Full Handoff (April 2026)

This is the authoritative updated spec for the entire build. All prior specs are superseded by this document.

---

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
  - `IRunwayDataProvider` interface
  - `FallbackRunwayDataProvider` (primary → OurAirports fallback)
  - `OurAirportsCsvRunwayDataProvider` (bundled CSV, indexed by ICAO, displaced-threshold-aware)
  - `RunwayResolver` (matches runway by heading at touchdown, ±30° tolerance)
  - `TouchdownZoneCalculator` (projects touchdown onto centerline, FAA 3,000 ft zone rule)
- **SimCrewOps.Runtime** (commit f2dfa9a)
  - `RuntimeCoordinator` wiring full pipeline
  - `ProcessFrameAsync` — frame-by-frame telemetry pipeline
  - At WheelsOn: resolves runway, injects `TouchdownZoneExcessDistanceFeet` into enriched frame
  - Returns `RuntimeFrameResult` (phase frame, enriched telemetry, runway resolution, runtime state snapshot)
  - `ArrivalAirportIcao` lives in `FlightSessionContext` — NOT in `FlightScoreInput`
- **SimCrewOps.Persistence** (commit de21985)
  - `FileSystemFlightSessionStore` — atomic temp-file writes to `current-session.json` (crash-safe)
  - Queues completed sessions as individual JSON files for upload
  - `IFlightSessionStore` interface

### CI
`.github/workflows/dotnet-tests.yml` — builds and tests on Ubuntu and Windows

---

## Locked Contracts (do not modify without flagging)
- `FlightScoreInput` — organized by phase, immutable
- `TelemetryFrame` — `GateArrivalDistanceFeet` kept but marked reserved
- `ArrivalAirportIcao` → `FlightSessionContext` only, never in scoring contract
- `TouchdownZoneExcessDistanceFeet` → injected from `RunwayResolver` into `FlightScoreInput.Landing`

---

## Scoring Spec (locked — Dar-approved April 2026)

### Grade Scale (A–F only — no A+)
| Grade | Range |
|---|---|
| A | 90–100 |
| B | 80–89 |
| C | 70–79 |
| D | 60–69 |
| F | below 60 |

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
- Overspeed event: 3 pts each, max 9 total
- Sustained overspeed (30s+): 5 pts each, max 10 total
- Stall warning: 3 pts each, max 9 total
- GPWS alert: 3 pts each, max 9 total
- Engine shutdown in flight: 5 pts each, no cap
- **Crash/sim reset: whole-flight F — the ONLY whole-flight fail condition**

### Section Fail vs Flight Fail
- Section fail = that section scores 0, rest of flight scores normally
- Landing section fails (section only) if touchdown VS > 600 fpm **or** G-force > 2.0G
- Crash/sim reset = whole-flight F (only condition)

### Gradual Threshold Formula
```
value <= A                        → no deduction (full credit)
value > B (section-fail capable)  → section failed
otherwise                         → deduction = max_metric_points × ((value - A) / (B - A))
```

### Phase Rules

#### PREFLIGHT
- Beacon light ON before taxi starts

#### TAXI OUT
- GS < 40 kt
- Turns >45°: GS must be < 15 kt (turn speed event)
- Taxi lights ON

#### TAKEOFF
- Bounce = lifts off then returns to runway (<100 ft AGL, still in takeoff context)
- Tail strike = `OnGround = true` AND pitch ≥ 10° AND IAS ≥ 40 kt
- Bank max 30°, Pitch max 20°
- G-force: full credit ≤1.5G, linear deduction 1.5–2.5G, max 3 pts deducted
- Landing lights ON before takeoff
- Landing lights OFF before FL180
- Strobes ON from takeoff through landing

#### CLIMB
- Speed below FL100: max 250 kt (heavy 4-engine exception: 300 kt)
- Bank max 30°
- G-force: full credit ≤1.7G, linear 1.7–2.5G, max 3 pts

#### CRUISE
- Altitude hold: ±100 ft of cruise FL
- Auto-detect new FL after 60 seconds stable at new altitude
- Speed instability event: IAS delta >15 kt OR Mach delta >0.03, sustained 5s, 10s cooldown, 1 pt per event, max 4 pts total
- G-force: full credit ≤1.4G, linear 1.4–2.0G, max 2 pts

#### DESCENT
- Speed below FL100: max 250 kt
- Pitch max 20°
- G-force: full credit ≤1.7G, linear 1.7–2.5G, max 3 pts
- Landing lights ON from FL180 to landing

#### APPROACH (FL100 to 2000 AGL)
- Gear down by 1000 AGL
- Flaps > 1
- Stabilized by 500 AGL: VS ≤1000 fpm, bank ≤10°, pitch ≤10°, gear down, flaps >1

#### LANDING
- Touchdown zone: deduct based on excess distance outside TDZ (no section fail for this alone)
- Touchdown VS: ≤300 fpm = full credit | 300–600 fpm = gradual linear deduction | >600 fpm = Landing section F
- Touchdown G-force: ≤1.2G = full credit | 1.2–2.0G = gradual linear deduction | >2.0G = Landing section F
- Use 2-second rolling buffer before touchdown for VS and G capture
- Landing bounce = touchdown → airborne → touchdown again within 3 seconds

#### TAXI IN
- Landing lights OFF
- Strobes OFF
- GS < 40 kt, Turns >45°: GS < 15 kt
- Taxi lights ON

#### ARRIVAL (sequence matters — order is enforced)
1. Taxi lights OFF before parking brake set
2. Parking brake set before ALL engines off  
   - One engine may shut down before PB (single-engine taxi is acceptable)
   - Violation = ALL engines off before parking brake set
3. All engines off by end of session

### Bounce Concepts (must NOT be merged in logic)
- **Takeoff bounce**: liftoff during takeoff → returns to runway before establishing climb
- **Landing bounce**: touchdown → airborne → touchdown again within 3 seconds post-landing

---

## Build Priority Order

### 1. Wire Runtime → Persistence ← BUILD THIS FIRST

`RuntimeCoordinator` must drive `FileSystemFlightSessionStore`:

- **Each frame**: autosave `current-session.json`, throttled to max 1 write/second, only when state has changed meaningfully
- **On Arrival phase**: call `StoreCompletedSessionAsync()` to queue session for upload, then clear `current-session.json`
- **On session discard**: call `DiscardCurrentSessionAsync()`
- **On startup**: check for existing `current-session.json` — if found, offer resume or discard (crash recovery path)

Introduce a `TrackerOrchestrator` (or extend `RuntimeCoordinator`) that owns this lifecycle:

```csharp
public interface ITrackerOrchestrator
{
    Task StartSessionAsync(FlightSessionContext context);
    Task ProcessFrameAsync(TelemetryFrame frame);
    Task DiscardSessionAsync();
    // Arrival phase triggers auto-queue internally
}
```

### 2. API Sync Layer

- `ISessionUploadService` interface
- `SimCrewOpsApiClient` implementation stub
- Retry logic: exponential backoff, 3 attempts before marking failed
- Upload triggered on session queue event + sweep on app startup for leftover pending sessions
- After successful upload: `IFlightSessionStore.RemoveCompletedSessionAsync()`
- **DO NOT implement actual HTTP calls yet** — confirm backend endpoint with Dar first. Build interface and client stub only.

### 3. SimConnect Adapter (Windows, MSFS 2020/2024)

Named pipe only. Protocol.KittyHawk. C# native — do NOT use node-simconnect.

```csharp
public interface ISimConnectAdapter
{
    Task ConnectAsync();
    Task DisconnectAsync();
    event EventHandler<TelemetryFrame> FrameReceived;
    event EventHandler ConnectionStateChanged;
}
```

SimVars to poll (1Hz minimum, 4Hz during landing phase):
- `PLANE LATITUDE`, `PLANE LONGITUDE`, `PLANE ALTITUDE`
- `PLANE ALT ABOVE GROUND`
- `AIRSPEED INDICATED`, `VERTICAL SPEED`
- `PLANE HEADING DEGREES MAGNETIC`
- `GROUND VELOCITY`
- `G FORCE`
- `PLANE BANK DEGREES`, `PLANE PITCH DEGREES`
- `SIM ON GROUND`
- `GEAR HANDLE POSITION`, `FLAPS HANDLE INDEX`
- `AUTOPILOT MASTER`, `AUTOPILOT ALTITUDE LOCK VAR`
- `LIGHT BEACON ON`, `LIGHT TAXI ON`, `LIGHT LANDING ON`, `LIGHT STROBE ON`
- `NUMBER OF ENGINES`, `ENGINE CONTROL` (per engine)
- `BRAKE PARKING INDICATOR`

Adapter's only job = produce `TelemetryFrame` structs. No business logic inside the adapter.

### 4. SimConnectFacilityRunwayProvider

SimConnect Facility API as primary runway source. `OurAirportsCsvRunwayDataProvider` stays as fallback. The slot is already modeled in `FallbackRunwayDataProvider` — needs implementation only.

### 5. WPF Tray App

Windows tray-first. System tray icon with connection status indicator. Opens main window on click.

---

## CRITICAL UI DECISION: Approach Diagram Removed from Tray App

**The spatial approach diagram has been removed from the WPF tray app.** It was too small to be actionable mid-flight and added unnecessary WPF complexity.

**During Approach phase**, the Live Ops Board shows a live numeric readout grid updating ~1s:

```
IAS         142 kt     VS       -680 fpm  ⚠
ALT AGL   1,900 ft     G/S      -0.2 dot
DIST THR    4.2 nm     LOC      +0.1 dot
```

Flag values outside stable approach criteria with ⚠. Numbers are faster to read mid-approach than any spatial diagram.

**The approach visualization belongs in the SimCrewOps web app as a post-flight debrief component:**
- Top-down track plot showing where they intercepted
- Vertical profile with ideal 3° glideslope overlaid
- Touchdown point marked on runway geometry
- Score breakdown by phase

The tracker records lat/lon track + telemetry every frame. The web app renders the debrief after flight is complete.

---

## WPF Main Window Layout

```
┌──────────────────────────────────────────────────────────┐
│ SimCrewOps Tracker           ● MSFS CONNECTED  ● SYNCED  │
│ DAL1423 · JFK→MIA · A320neo · Captain Step 3             │
│ [Legacy Tier]  [Bid #48291]  [Rep 1.10x]                 │
├────────────────────┬─────────────────────────────────────┤
│ FLIGHT STACK       │ LIVE OPS BOARD                      │
│                    │                                     │
│ — Career Status    │  [Phase-appropriate live display]   │
│ Legacy Tier        │  (see phase display rules below)    │
│ Bid #48291         │                                     │
│ Rep 1.10x          │                                     │
│                    │                                     │
│ — Flight Times     │                                     │
│ OUT  19:45         │                                     │
│ OFF  19:58         │                                     │
│ ON   --:--         │                                     │
│ IN   --:--         │                                     │
│                    ├─────────────────────────────────────┤
│ — Phase Rail       │ SCOREBOARD                          │
│ ● PREFLT TAXI...   │ 88  Grade B                         │
│   CLB CRZ DES...   │ [Section score bars, 8px min]       │
│   APP LDG TAXI IN  │                                     │
│   ARR              │                                     │
├────────────────────┴─────────────────────────────────────┤
│  [Dispatch▸]  [PIREP Draft]  [Diagnostics]  [Settings]   │
└──────────────────────────────────────────────────────────┘
```

### Phase Display Rules — Live Ops Board

| Phase | Display |
|---|---|
| Preflight / Taxi Out / Taxi In | Ground speed, heading, phase status, light compliance |
| Takeoff | IAS, VS, pitch, bank, G-force live |
| Climb / Cruise / Descent | Altitude, IAS (or Mach above FL280), VS, heading |
| Approach | 6-metric numeric grid (IAS, VS, ALT AGL, DIST THR, G/S dot, LOC dot) |
| Landing | VS and G-force at touchdown — held on screen 10s after WheelsOn |
| Arrival | PB status, engine status, light compliance ticking off as completed |

### WPF UI Rules
- **Bottom nav**: must have a proper active state — 2px accent bottom border on active tab, not just text buttons
- **Left panel**: use hairline section dividers (`—`) between Career Status / Flight Times / Phase Rail — not separate card borders. Reduces visual noise.
- **Score bars**: 8px minimum height. Adequate vertical spacing between rows. Must be legible at 1080p.

---

## Data Flow Summary

```
MSFS 2024
  ↓ SimConnect (named pipe, Protocol.KittyHawk)
SimConnectAdapter  →  TelemetryFrame
  ↓
TrackerOrchestrator.ProcessFrameAsync()
  ├→ FlightPhaseEngine         (phase transitions + events)
  ├→ FlightSessionScoringTracker  (accumulates FlightScoreInput)
  ├→ RunwayResolver            (at WheelsOn → TDZ calculation)
  ├→ ScoringEngine             (scores per phase → FlightScoreResult)
  └→ FileSystemFlightSessionStore  (autosave current-session.json)
         ↓ on Arrival phase
     PendingCompletedSession queue
         ↓
     SimCrewOpsApiClient  →  SimCrewOps web backend
         ↓ post-flight
     Web app: approach path visualization, score debrief, PIREP editor
```

---

## NOT Building in Tracker App (belongs in web app)
- **Approach path diagram** — render from stored telemetry in web debrief after flight
- **Full PIREP editor** — tracker captures notes/flags only; full editor is on web
- **Career management** — tracker shows read-only career context only
- **Gate arrival precision** — no gate database; arrival scoring uses operational sequence only (taxi lights → PB → engines off)

---

## Confirmed Architecture Decisions
- Named pipe only for SimConnect — no TCP. Protocol.KittyHawk.
- OurAirports CSV as runway fallback — bundled, no network dependency at runtime
- SimConnect Facility API as primary runway source — NOT X-Plane apt.dat (this is MSFS 2024)
- `ArrivalAirportIcao` in `FlightSessionContext` only — never in `FlightScoreInput`
- No whole-flight fail except crash/sim reset
- Landing section fail (section only, not whole-flight) for VS >600 fpm or G >2.0G
- A–F grading only — no A+

---

**Start with task 1: wire runtime → persistence. When done, move to task 2 (API sync stub). Do not implement HTTP calls until Dar confirms the backend endpoint.**
