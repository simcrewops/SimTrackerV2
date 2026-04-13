# SimCrewOps Tracker Main Window Mockup

This is the first-pass wireframe for the desktop tracker app. The goal is a tray-first ops console that answers three questions quickly:

- What flight is this?
- What is happening right now?
- Is anything wrong or waiting on me?

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│ SimCrewOps Tracker                    Connected • MSFS • ACARS Online       │
│ DAL1423  JFK → MIA                    Phase: APPROACH                       │
├──────────────────┬──────────────────────────────────────┬───────────────────┤
│ Flight / Career  │ Live Flight                          │ Dispatch / Events │
│ Captain Step 3   │ IAS 148   GS 151   VS -720          │ 19:42 Connected   │
│ Bid #48291       │ ALT 2400   AGL 1900                 │ 19:45 Blocks Off  │
│ A320neo          │ HDG 222   Bank 3.2   Pitch 1.1      │ 19:58 Wheels Off  │
│ Rep: Good 1.10x  │ Gear Down   Flaps 2   PB Off        │ 22:31 Descent     │
│                  │                                      │ Dispatch msgs...  │
│ Times            │ Phase Rail                           │                   │
│ OUT 19:45        │ PREFLT > TAXI > TO > CLB > CRZ      │ Alerts            │
│ OFF 19:58        │ DES > APP > LDG > TAXI IN > ARR     │ - Unstable 500AGL │
│ ON  --:--        │ [current phase highlighted]          │ - Overspeed x1    │
│ IN  --:--        │                                      │                   │
├──────────────────┼──────────────────────────────────────┼───────────────────┤
│ Score Preview    │ Map / Runway / Approach View         │ Session Status    │
│ Preflight   5/5  │                                      │ Autosave OK       │
│ Taxi Out    8/8  │                                      │ API Queue 0       │
│ Approach   10/12 │                                      │ Logs Ready        │
│ Landing      --  │                                      │ Update Deferred   │
├──────────────────┴──────────────────────────────────────┴───────────────────┤
│ [Open ACARS] [Open PIREP Draft] [Diagnostics] [Settings]                    │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Interaction Notes

- Default app state is hidden in the system tray.
- The window is compact and dense, more dispatch console than consumer dashboard.
- The center panel is the operational anchor: live telemetry, current phase, and runway/map context.
- The right panel changes importance during flight:
  - dispatch thread and event feed during active ops
  - warnings and recovery items when something goes wrong
- Score Preview is informative mid-flight and becomes final after `Arrival`.

## Planned Secondary Screens

- `ACARS / Dispatch`
- `PIREP Review`
- `Diagnostics / Recovery`
- `Settings`

## Before UI Implementation

- Confirm whether the first WPF shell should open directly to this dashboard or to a smaller tray-status popup.
- Confirm whether the center panel should prioritize a moving map, runway view, or phase/metrics card in the MVP.
- Keep the app shell thin: runtime, persistence, and sync continue living outside the UI.
