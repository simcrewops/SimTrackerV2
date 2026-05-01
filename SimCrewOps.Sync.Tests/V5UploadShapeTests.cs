using System.Text.Json;
using SimCrewOps.Persistence.Models;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Sync.Sync;
using Xunit;

namespace SimCrewOps.Sync.Tests;

public sealed class V5UploadShapeTests
{
    private static readonly string TrackerVersion = "3.0.0";

    private static PendingCompletedSession Session(FlightScoreInput? scoreInput = null) =>
        new()
        {
            SessionId = "test-session",
            SavedUtc  = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero),
            State     = new FlightSessionRuntimeState
            {
                Context = new FlightSessionContext { FlightMode = "career" },
                CurrentPhase = FlightPhase.Arrival,
                BlockTimes = new FlightSessionBlockTimes
                {
                    BlocksOffUtc = new DateTimeOffset(2026, 4, 15, 8, 0, 0, TimeSpan.Zero),
                    BlocksOnUtc  = new DateTimeOffset(2026, 4, 15, 11, 0, 0, TimeSpan.Zero),
                },
                ScoreInput  = scoreInput ?? new FlightScoreInput(),
                ScoreResult = new ScoreResult(100, 91, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
            },
        };

    [Fact]
    public void MapScoringInput_BeaconOnBeforeTaxi_PassesThroughAsTrue()
    {
        var session = Session(new FlightScoreInput
        {
            Preflight = new PreflightMetrics { BeaconOnBeforeTaxi = true },
        });

        var request = new SimSessionUploadRequestMapper().Map(session, TrackerVersion);
        var json = JsonSerializer.Serialize(request.ScoringInput);

        Assert.Contains("\"beaconOnBeforeTaxi\":true", json);
    }

    [Fact]
    public void MapScoringInput_GearDownAt1500Agl_ReportsCorrectAgl()
    {
        var session = Session(new FlightScoreInput
        {
            Approach = new ApproachMetrics { GearDownAglFt = 1500.0 },
        });

        var request = new SimSessionUploadRequestMapper().Map(session, TrackerVersion);
        var json = JsonSerializer.Serialize(request.ScoringInput);

        Assert.Contains("\"gearDownAglFt\":1500", json);
        Assert.Equal(1500.0, request.ScoringInput.Approach.GearDownAglFt);
    }

    [Fact]
    public void MapScoringInput_ProducesAllThirteenPhases()
    {
        var session = Session(new FlightScoreInput
        {
            Arrival = new ArrivalMetrics { ArrivalReached = true },
        });

        var request = new SimSessionUploadRequestMapper().Map(session, TrackerVersion);
        var json = JsonSerializer.Serialize(request.ScoringInput);

        var expectedKeys = new[]
        {
            "\"preflight\"",
            "\"taxiOut\"",
            "\"takeoff\"",
            "\"climb\"",
            "\"cruise\"",
            "\"descent\"",
            "\"approach\"",
            "\"stabilizedApproach\"",
            "\"landing\"",
            "\"taxiIn\"",
            "\"arrival\"",
            "\"lightsSystems\"",
            "\"safety\"",
        };

        foreach (var key in expectedKeys)
            Assert.Contains(key, json);
    }

    [Fact]
    public void MapScoringInput_ArrivalIsNullWhenArrivalNotReached()
    {
        var session = Session(new FlightScoreInput
        {
            Arrival = new ArrivalMetrics { ArrivalReached = false },
        });

        var request = new SimSessionUploadRequestMapper().Map(session, TrackerVersion);

        Assert.Null(request.ScoringInput.Arrival);
    }

    [Fact]
    public void MapScoringInput_StrobeLightOnDuringTaxi_InvertsStrobesOff()
    {
        // StrobesOff = false means strobes were incorrectly ON during taxi.
        // The wire format inverts: strobeLightOnDuringTaxi = !StrobesOff = true.
        var session = Session(new FlightScoreInput
        {
            TaxiOut = new TaxiMetrics { StrobesOff = false },
            TaxiIn  = new TaxiInMetrics { StrobesOff = false },
        });

        var request = new SimSessionUploadRequestMapper().Map(session, TrackerVersion);

        Assert.True(request.ScoringInput.TaxiOut.StrobeLightOnDuringTaxi);
        Assert.True(request.ScoringInput.TaxiIn.StrobeLightOnDuringTaxi);
    }
}
