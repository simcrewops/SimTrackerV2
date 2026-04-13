# SimCrewOps.Tracking

This project sits between live simulator telemetry and the scoring library.

## Purpose

- ingest normalized telemetry frames from MSFS/X-Plane adapters
- accumulate phase metrics automatically over the life of a flight
- produce a `FlightScoreInput` object for `SimCrewOps.Scoring`
- optionally calculate a score directly through `FlightSessionScoringTracker.CalculateScore()`

## Main flow

1. The simulator adapter emits `TelemetryFrame`
2. `FlightSessionScoringTracker.Ingest(frame)` updates the scoring state
3. At arrival, call `BuildScoreInput()` or `CalculateScore()`

## Auto-detected items

- beacon before taxi
- taxi speed and high-speed turns
- takeoff bounce and tail strike
- takeoff max G-force
- climb/descent speed compliance below FL100
- cruise altitude drift and sustained speed instability events
- gear/flaps/stabilized approach samples at 1000/500 AGL
- touchdown vertical speed from the 2-second pre-touchdown buffer
- touchdown G-force from the same low-AGL window
- landing bounce
- arrival shutdown-order checks
- crash, overspeed, stall, GPWS, engine shutdowns in flight

## Notes

- touchdown zone excess distance is accepted from telemetry frames as an optional field
- the tracker assumes phases are already determined by the session/phase engine
- this project is intentionally simulator-agnostic
