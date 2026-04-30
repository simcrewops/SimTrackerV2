using SimCrewOps.SimConnect.Models;
using SimCrewOps.SimConnect.Services;
using Xunit;

namespace SimCrewOps.SimConnect.Tests;

public sealed class SimConnectTelemetryMapperTests
{
    [Fact]
    public void Map_ConvertsRawFrameToTelemetryFrame()
    {
        var mapper = new SimConnectTelemetryMapper();

        var frame = mapper.Map(new SimConnectRawTelemetryFrame
        {
            TimestampUtc = new DateTimeOffset(2026, 4, 13, 15, 0, 0, TimeSpan.Zero),
            Latitude = 25.79,
            Longitude = -80.29,
            AltitudeAglFeet = 1200,
            AltitudeFeet = 4700,
            IndicatedAltitudeFeet = 3500,
            IndicatedAirspeedKnots = 145,
            TrueAirspeedKnots = 153,
            Mach = 0.54,
            GroundSpeedKnots = 151,
            VerticalSpeedFpm = -720,
            BankAngleDegrees = 3.2,
            PitchAngleDegrees = 1.1,
            HeadingMagneticDegrees = 219,
            HeadingTrueDegrees = 222,
            GForce = 1.18,
            ParkingBrakePosition = 100,  // BRAKE PARKING POSITION is 0–100; use 100 (fully set) to assert ParkingBrakeSet=true
            OnGround = 0,
            CrashFlag = 0,
            FlapsHandleIndex = 2,
            GearPosition = 1,
            BeaconLightOn = 1,
            TaxiLightsOn = 1,
            LandingLightsOn = 1,
            StrobesOn = 1,
            StallWarning = 0,
            GpwsAlert = 1,
            OverspeedWarning = 0,
            Engine1Running = 1,
            Engine2Running = 1,
            Engine3Running = 0,
            Engine4Running = 0,
        });

        Assert.Equal(25.79, frame.Latitude);
        Assert.Equal(4700, frame.AltitudeFeet);
        Assert.Equal(145, frame.IndicatedAirspeedKnots);
        Assert.Equal(219, frame.HeadingMagneticDegrees);
        Assert.Equal(2, frame.FlapsHandleIndex);
        Assert.True(frame.ParkingBrakeSet);
        Assert.True(frame.GearDown);
        Assert.True(frame.GpwsAlert);
        Assert.True(frame.Engine1Running);
        Assert.False(frame.Engine3Running);
    }

    [Fact]
    public void Map_MapsExtendedContextFieldsCorrectly()
    {
        var mapper = new SimConnectTelemetryMapper();

        var frame = mapper.Map(new SimConnectRawTelemetryFrame
        {
            TimestampUtc = new DateTimeOffset(2026, 4, 13, 15, 30, 0, TimeSpan.Zero),

            // Extended context fields under test
            AutopilotMaster          = 1,      // true
            FuelTotalLbs             = 42_500,
            AmbientWindSpeedKnots    = 18.5,
            AmbientWindDirectionDegrees = 270,
            AmbientTemperatureCelsius = -12.3,
            SpoilerHandlePosition    = 0.75,
            SpoilersArmed            = 1,      // true
            Engine1N1Pct             = 83.2,
            Engine2N1Pct             = 83.4,
            Engine3N1Pct             = 0,      // engine not installed / idle
            Engine4N1Pct             = 0,
            Nav1IlsSignalValid       = 1,      // true

            // Minimum required fields so Map() produces a valid frame
            Engine1Running = 1,
            Engine2Running = 1,
        });

        Assert.True(frame.AutopilotEngaged);
        Assert.Equal(42_500, frame.FuelTotalLbs);
        Assert.Equal(18.5,  frame.WindSpeedKnots);
        Assert.Equal(270,   frame.WindDirectionDegrees);
        Assert.Equal(-12.3, frame.OutsideAirTempCelsius);
        Assert.Equal(0.75,  frame.SpoilerHandlePosition);
        Assert.True(frame.SpoilersArmed);
        Assert.Equal(83.2, frame.Engine1N1Pct, precision: 6);
        Assert.Equal(83.4, frame.Engine2N1Pct, precision: 6);
        Assert.Equal(0,    frame.Engine3N1Pct);
        Assert.Equal(0,    frame.Engine4N1Pct);
        Assert.True(frame.Nav1IlsSignalValid);
    }
}
