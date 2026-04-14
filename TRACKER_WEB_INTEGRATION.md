# Tracker ↔ Web App Integration Spec

## Architecture Rule (MVP)

**Do not wire the web app directly to the desktop app process.**  
Wire both to the same backend contract.

```
Desktop Tracker  →  POST /api/sim-sessions  →  Backend DB
Web App          →  same backend DB/API
```

The tracker is already implemented as a backend client — not a browser extension or localhost companion. Keep it that way.

---

## What's Already Built (Desktop Side)

### Settings stored locally
`%LOCALAPPDATA%\SimCrewOps\SimTrackerV2\settings.json`  
File: `TrackerShellBootstrapper.cs`

Settings already supported:
- `BaseUrl`
- `SimSessionsPath`
- `PilotApiToken`
- `TrackerVersion`
- Background sync settings

Editor: `SettingsEditorViewModel.cs`

### Upload flow
- Completed flights queued locally → uploaded in background
- `CompletedSessionSyncService.cs`
- Auth: `Authorization: Bearer <pilot_api_token>`
- `HttpCompletedSessionUploader.cs`

### Success semantics (do not change)
- Tracker treats **only HTTP 201 Created** as success
- **408, 429, 5xx** → retryable failure
- **Everything else non-201** → permanent failure

---

## Existing Upload Contract

```
POST /api/sim-sessions
Authorization: Bearer {pilot_api_token}
Content-Type: application/json
```

### Payload (`SimSessionUploadRequest.cs`)

| Field | Type | Notes |
|---|---|---|
| `bounces` | int | **Note: server should read `bounces`, not `bounceCount`** |
| `touchdownVS` | double | fpm |
| `touchdownBank` | double | degrees |
| `touchdownIAS` | double | knots |
| `touchdownPitch` | double | degrees |
| `actualBlocksOff` | DateTimeOffset? | UTC |
| `actualWheelsOff` | DateTimeOffset? | UTC |
| `actualWheelsOn` | DateTimeOffset? | UTC |
| `actualBlocksOn` | DateTimeOffset? | UTC |
| `blockTimeActual` | double? | hours, 4 decimal places |
| `blockTimeScheduled` | double? | hours |
| `crashDetected` | bool | |
| `overspeedEvents` | int | |
| `stallEvents` | int | |
| `gpwsEvents` | int | |
| `grade` | string | A–F |
| `scoreFinal` | double | 0–100 |
| `trackerVersion` | string | e.g. "2.0.0" |
| `flightMode` | string | "free_flight" or bid-linked |
| `bidId` | string? | null if free flight |

Mapping source: `SimSessionUploadRequestMapper.cs`

**Do not redesign this payload unless you intentionally rev both sides at the same time.**  
The desktop app ships this contract. Adapt the backend to it first.

---

## What the Backend Needs to Build

### 1. Session Ingest Endpoint

```
POST /api/sim-sessions
```

- Authenticate via Bearer token → resolve pilot
- Accept the payload above
- Persist to DB (associate to pilot, bid if `bidId` present)
- Return **201 Created** on success (tracker requires exactly 201)
- Return 4xx for validation failures (permanent)
- Return 5xx or 429 for transient failures (tracker will retry)

### 2. Pilot Token Issuance

Each pilot needs a Bearer token the tracker can store locally.

**MVP:** Show/copy token on the web app settings page  
**Better:** `GET /api/tracker/bootstrap` returns:
```json
{
  "baseUrl": "https://simcrewops.com",
  "simSessionsPath": "/api/sim-sessions",
  "pilotApiToken": "...",
  "trackerVersion": "2.0.0"
}
```
**Best:** One-click download of `tracker-bootstrap.json` or deep link `simcrewops://connect?token=...`

### 3. Minimum DB Fields

```sql
pilot_id           -- from bearer token
bid_id             -- nullable
flight_mode        -- "free_flight" or bid mode
grade              -- A–F
score_final        -- 0–100
bounces
touchdown_vs
touchdown_bank
touchdown_ias
touchdown_pitch
actual_blocks_off  -- timestamptz
actual_wheels_off  -- timestamptz
actual_wheels_on   -- timestamptz
actual_blocks_on   -- timestamptz
block_time_actual  -- hours
block_time_scheduled
crash_detected     -- bool
overspeed_events
stall_events
gpws_events
tracker_version
created_at         -- timestamptz, server-set
```

---

## What the Web App Needs to Build

### "Connect Tracker" flow

```
1. Pilot signs into web app
2. Web app shows "Connect Tracker" section in settings
3. Pilot copies their API token (or downloads bootstrap file)
4. Pilot pastes token into tracker Settings tab
5. Tracker uploads completed sessions automatically
6. Web app shows sessions in pilot history
```

### Pages to surface tracker sessions

- **Pilot flight log** — list of completed sessions with grade/score
- **Bid/career history** — sessions linked to bid ID show under that bid
- **Session detail page** — full score breakdown, landing metrics, block times
- **Score/grade summary cards** — grade badge, score, key landing stats

---

## MVP Scope (do these first)

1. `POST /api/sim-sessions` — ingest endpoint returning 201
2. Pilot token display in web app settings
3. Session list in pilot flight log
4. Session detail page

## Post-MVP

- `GET /api/tracker/bootstrap` authenticated endpoint
- Deep link / one-click connect
- Approach path visualization (from stored telemetry — web debrief)
- PIREP generation from session data
