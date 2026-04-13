using SimCrewOps.PhaseEngine.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Tracking.Models;

namespace SimCrewOps.PhaseEngine.PhaseEngine;

/// <summary>
/// Stateful state machine that labels each <see cref="TelemetryFrame"/> with a
/// <see cref="FlightPhase"/> and detects the four block-time events.
/// Call <see cref="Process"/> once per frame, in chronological order.
/// Call <see cref="Reset"/> to start a new session.
/// </summary>
public sealed class FlightPhaseEngine
{
    private FlightPhase _currentPhase = FlightPhase.Preflight;
    private TelemetryFrame? _previousFrame;
    private readonly List<BlockEvent> _blockEvents = new();

    // Sustained-condition timers
    private DateTimeOffset? _levelFlightConditionStart;  // CLIMB → CRUISE  (VS in [-200, +200])
    private DateTimeOffset? _descentConditionStart;       // CRUISE → DESCENT (VS < -200)
    private DateTimeOffset? _slowOnGroundStart;           // LANDING → TAXI IN (GS < 40)

    // WheelsOn timestamp — used to detect touch-and-go within 3 s
    private DateTimeOffset? _wheelsOnAt;
    private bool _approachRecoveryActive;

    public FlightPhase CurrentPhase => _currentPhase;

    public IReadOnlyList<BlockEvent> BlockEvents => _blockEvents.AsReadOnly();

    /// <summary>
    /// Process a single telemetry frame. Returns a <see cref="PhaseFrame"/> carrying
    /// the engine-assigned phase and, when it fires, the block event for this frame.
    /// </summary>
    public PhaseFrame Process(TelemetryFrame frame)
    {
        var blockEvent = Advance(frame);
        _previousFrame = frame;

        return new PhaseFrame
        {
            Raw = frame,
            Phase = _currentPhase,
            BlockEvent = blockEvent,
        };
    }

    /// <summary>Reset all state for a new session.</summary>
    public void Reset()
    {
        _currentPhase = FlightPhase.Preflight;
        _previousFrame = null;
        _blockEvents.Clear();
        _levelFlightConditionStart = null;
        _descentConditionStart = null;
        _slowOnGroundStart = null;
        _wheelsOnAt = null;
        _approachRecoveryActive = false;
    }

    /// <summary>
    /// Restore durable engine state from a previously persisted session.
    /// Short-lived sustain timers are intentionally reset and re-established from new telemetry.
    /// </summary>
    public void Restore(
        FlightPhase currentPhase,
        TelemetryFrame? previousFrame,
        IEnumerable<BlockEvent>? blockEvents = null)
    {
        _currentPhase = currentPhase;
        _previousFrame = previousFrame;
        _blockEvents.Clear();

        if (blockEvents is not null)
        {
            _blockEvents.AddRange(blockEvents.OrderBy(static blockEvent => blockEvent.TimestampUtc));
        }

        ResetSustainedConditions();

        var latestWheelsOn = _blockEvents.LastOrDefault(static blockEvent => blockEvent.Type == BlockEventType.WheelsOn);
        _wheelsOnAt = currentPhase == FlightPhase.Landing ? latestWheelsOn?.TimestampUtc : null;
        _approachRecoveryActive =
            currentPhase == FlightPhase.Climb &&
            previousFrame is not null &&
            !previousFrame.OnGround &&
            previousFrame.GearDown &&
            previousFrame.AltitudeAglFeet < 3000;
    }

    // -------------------------------------------------------------------------
    //  Core state-machine step
    // -------------------------------------------------------------------------

    private BlockEvent? Advance(TelemetryFrame frame)
    {
        // ----- 1. Edge-case backward / lateral transitions -----

        // Go-around: while established on final/landing, aircraft climbs back through 400 ft AGL.
        if (_currentPhase is FlightPhase.Approach or FlightPhase.Landing
            && _previousFrame is not null
            && !_previousFrame.OnGround
            && !frame.OnGround
            && _previousFrame.AltitudeAglFeet <= 400
            && frame.AltitudeAglFeet > 400)
        {
            _currentPhase = FlightPhase.Climb;
            _approachRecoveryActive = true;
            ResetSustainedConditions();
            return null;
        }

        // Aborted takeoff: TAKEOFF, IAS drops below 40 kts while still on ground
        if (_currentPhase == FlightPhase.Takeoff
            && frame.IndicatedAirspeedKnots < 40 && frame.OnGround)
        {
            _currentPhase = FlightPhase.TaxiOut;
            return null;
        }

        // Touch-and-go: aircraft becomes airborne within 3 s of WheelsOn
        if (_wheelsOnAt is not null
            && !frame.OnGround
            && frame.TimestampUtc - _wheelsOnAt.Value <= TimeSpan.FromSeconds(3))
        {
            // Cancel the WheelsOn event — remove it from the permanent list
            _blockEvents.RemoveAll(e => e.Type == BlockEventType.WheelsOn);
            _wheelsOnAt = null;
            _currentPhase = FlightPhase.Climb;
            _approachRecoveryActive = true;
            ResetSustainedConditions();
            return null;
        }

        // ----- 2. OnGround-transition block events (WheelsOff / WheelsOn) -----

        BlockEvent? blockEvent = null;

        if (_previousFrame is not null)
        {
            // WheelsOff — first time the aircraft leaves the ground
            if (_previousFrame.OnGround && !frame.OnGround
                && !_blockEvents.Any(e => e.Type == BlockEventType.WheelsOff))
            {
                blockEvent = MakeBlockEvent(BlockEventType.WheelsOff, frame);
                _blockEvents.Add(blockEvent);
            }

            // WheelsOn — first time the aircraft touches down (or re-fires after a cancelled touch-and-go)
            if (!_previousFrame.OnGround && frame.OnGround
                && !_blockEvents.Any(e => e.Type == BlockEventType.WheelsOn))
            {
                blockEvent = MakeBlockEvent(BlockEventType.WheelsOn, frame);
                _blockEvents.Add(blockEvent);
                _wheelsOnAt = frame.TimestampUtc;
            }
        }

        // ----- 3. Forward phase transitions -----

        switch (_currentPhase)
        {
            case FlightPhase.Preflight:
                // Transition fires as soon as the parking brake is released with motion
                if (!frame.ParkingBrakeSet && frame.GroundSpeedKnots > 0.5)
                {
                    _currentPhase = FlightPhase.TaxiOut;
                    blockEvent = MakeBlockEvent(BlockEventType.BlocksOff, frame);
                    _blockEvents.Add(blockEvent);
                }
                break;

            case FlightPhase.TaxiOut:
                if (frame.IndicatedAirspeedKnots > 40)
                {
                    _currentPhase = FlightPhase.Takeoff;
                }
                break;

            case FlightPhase.Takeoff:
                if (frame.AltitudeAglFeet > 400)
                {
                    _currentPhase = FlightPhase.Climb;
                    ResetSustainedConditions();
                }
                break;

            case FlightPhase.Climb:
                if (_approachRecoveryActive && !frame.OnGround && frame.AltitudeAglFeet < 3000 && frame.GearDown)
                {
                    _currentPhase = FlightPhase.Approach;
                    _approachRecoveryActive = false;
                    ResetSustainedConditions();
                    break;
                }

                // Require VS between -200 and +200 fpm sustained for 30 s → Cruise
                if (frame.VerticalSpeedFpm >= -200 && frame.VerticalSpeedFpm <= 200)
                {
                    _levelFlightConditionStart ??= frame.TimestampUtc;
                    if (frame.TimestampUtc - _levelFlightConditionStart.Value >= TimeSpan.FromSeconds(30))
                    {
                        _currentPhase = FlightPhase.Cruise;
                        _levelFlightConditionStart = null;
                    }
                }
                else
                {
                    _levelFlightConditionStart = null;
                }
                break;

            case FlightPhase.Cruise:
                // Require VS < -200 fpm sustained for 30 s → Descent
                if (frame.VerticalSpeedFpm < -200)
                {
                    _descentConditionStart ??= frame.TimestampUtc;
                    if (frame.TimestampUtc - _descentConditionStart.Value >= TimeSpan.FromSeconds(30))
                    {
                        _currentPhase = FlightPhase.Descent;
                        _descentConditionStart = null;
                    }
                }
                else
                {
                    _descentConditionStart = null;
                }
                break;

            case FlightPhase.Descent:
                // Gear must be down and altitude below 3000 ft AGL
                if (frame.AltitudeAglFeet < 3000 && frame.GearDown)
                {
                    _currentPhase = FlightPhase.Approach;
                    _approachRecoveryActive = false;
                }
                break;

            case FlightPhase.Approach:
                // Touchdown (OnGround) fires the WheelsOn event above; phase follows here
                if (frame.OnGround)
                {
                    _currentPhase = FlightPhase.Landing;
                }
                break;

            case FlightPhase.Landing:
                // Require GS < 40 kts sustained for 5 s → Taxi In
                if (frame.GroundSpeedKnots < 40)
                {
                    _slowOnGroundStart ??= frame.TimestampUtc;
                    if (frame.TimestampUtc - _slowOnGroundStart.Value >= TimeSpan.FromSeconds(5))
                    {
                        _currentPhase = FlightPhase.TaxiIn;
                        _slowOnGroundStart = null;
                    }
                }
                else
                {
                    _slowOnGroundStart = null;
                }
                break;

            case FlightPhase.TaxiIn:
                // Parking brake set and essentially stationary → Arrival + BlocksOn
                if (frame.ParkingBrakeSet && frame.GroundSpeedKnots < 0.5)
                {
                    _currentPhase = FlightPhase.Arrival;
                    blockEvent = MakeBlockEvent(BlockEventType.BlocksOn, frame);
                    _blockEvents.Add(blockEvent);
                }
                break;

            case FlightPhase.Arrival:
                // Terminal state — no further automatic transitions
                break;
        }

        return blockEvent;
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    private void ResetSustainedConditions()
    {
        _levelFlightConditionStart = null;
        _descentConditionStart = null;
        _slowOnGroundStart = null;
    }

    private static BlockEvent MakeBlockEvent(BlockEventType type, TelemetryFrame frame) =>
        new()
        {
            Type = type,
            TimestampUtc = frame.TimestampUtc,
            LatitudeDeg = frame.Latitude,
            LongitudeDeg = frame.Longitude,
        };
}
