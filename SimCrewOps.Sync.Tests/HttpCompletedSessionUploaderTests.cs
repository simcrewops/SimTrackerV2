using System.Net;
using System.Net.Http;
using System.Text;
using SimCrewOps.Persistence.Models;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Sync.Models;
using SimCrewOps.Sync.Sync;
using Xunit;

namespace SimCrewOps.Sync.Tests;

public sealed class HttpCompletedSessionUploaderTests
{
    // ── Request mapper ────────────────────────────────────────────────────────

    [Fact]
    public void RequestMapper_MapsExpectedSimSessionFields()
    {
        var session = CreatePendingSession();
        var mapper  = new SimSessionUploadRequestMapper();

        var request = mapper.Map(session, "1.2.3");

        // Nested landing shape
        var landing = request.ScoringInput.Landing;
        Assert.Equal(-188, landing.TouchdownRateFpm); // positive stored → negated in payload
        Assert.Equal(1.8,  landing.TouchdownBankDeg);
        Assert.Equal(134,  landing.TouchdownGForce, precision: 0);  // note: GForce in session = 134 for simplicity
        Assert.Equal(2,    landing.BounceCount);

        // Block times
        Assert.Equal(session.State.BlockTimes.BlocksOffUtc, request.ActualBlocksOff);
        Assert.Equal(session.State.BlockTimes.WheelsOffUtc, request.ActualWheelsOff);
        Assert.Equal(session.State.BlockTimes.WheelsOnUtc,  request.ActualWheelsOn);
        Assert.Equal(session.State.BlockTimes.BlocksOnUtc,  request.ActualBlocksOn);

        // Context fields
        Assert.Equal("1.2.3",  request.TrackerVersion);
        Assert.Equal("career", request.FlightMode);
        Assert.Equal("bid-123", request.BidId);

        // Safety
        var safety = request.ScoringInput.Safety;
        Assert.True(safety.CrashDetected);
        Assert.Equal(3, safety.OverspeedWarningCount);
        Assert.Equal(1, safety.StallWarningCount);
        Assert.Equal(2, safety.GpwsAlertCount);
    }

    // ── Upload success ────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_PostsExpectedRequestAndTreats201AsSuccess()
    {
        string? capturedAuth = null;
        Uri? capturedUri = null;

        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedAuth = request.Headers.Authorization?.ToString();
            capturedUri  = request.RequestUri;
            _ = request.Content is not null
                ? await request.Content.ReadAsStringAsync()
                : null;

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(LoadFixture("sim_session_upload_success.json"), Encoding.UTF8, "application/json"),
            };
        });

        var uploader = new HttpCompletedSessionUploader(
            new HttpClient(handler),
            new SimCrewOpsApiUploaderOptions
            {
                BaseUri         = new Uri("https://simcrewops.com"),
                SimSessionsPath = "/api/sim-sessions",
                PilotApiToken   = "secret-token",
                TrackerVersion  = "2.0.0",
            });

        var result = await uploader.UploadAsync(CreatePendingSession());

        Assert.Equal(SessionUploadStatus.Success, result.Status);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("Bearer secret-token", capturedAuth);
        Assert.Equal(new Uri("https://simcrewops.com/api/sim-sessions"), capturedUri);
    }

    [Fact]
    public async Task UploadAsync_Parses201ResponseBody_PopulatesCareerAndPostFlight()
    {
        var uploader = BuildUploader(HttpStatusCode.Created, LoadFixture("sim_session_upload_success.json"));

        var result = await uploader.UploadAsync(CreatePendingSession());

        Assert.Equal(SessionUploadStatus.Success, result.Status);
        Assert.Equal("cmoj6kgf201rflm66lymx24j0", result.ServerSessionId);

        Assert.NotNull(result.CareerResult);
        Assert.Equal("A",    result.CareerResult!.Grade);
        Assert.Equal(91.4,   result.CareerResult.Score, precision: 1);
        Assert.Equal(1.967,  result.CareerResult.HoursAdded, precision: 3);
        Assert.Equal(847.0,  result.CareerResult.Pay, precision: 0);
        Assert.Equal(3,      result.CareerResult.ReputationDelta);

        Assert.NotNull(result.PostFlightStatus);
        Assert.False(result.PostFlightStatus!.IsGrounded);
        Assert.Null(result.PostFlightStatus.StrikeType);
    }

    // ── Upload failure modes ──────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_ClassifiesGrounded403AsPermanentFailure()
    {
        var uploader = BuildUploader(HttpStatusCode.Forbidden, LoadFixture("sim_session_upload_grounded_403.json"));

        var result = await uploader.UploadAsync(CreatePendingSession());

        Assert.Equal(SessionUploadStatus.PermanentFailure, result.Status);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task UploadAsync_ClassifiesRetryableAndPermanentFailures()
    {
        var retryableUploader = BuildUploader(HttpStatusCode.ServiceUnavailable, "try later");
        var permanentUploader = BuildUploader(HttpStatusCode.UnprocessableEntity, "bad payload");

        var retryable = await retryableUploader.UploadAsync(CreatePendingSession());
        var permanent = await permanentUploader.UploadAsync(CreatePendingSession());

        Assert.Equal(SessionUploadStatus.RetryableFailure, retryable.Status);
        Assert.Equal(503, retryable.StatusCode);
        Assert.Contains("try later", retryable.ErrorMessage);

        Assert.Equal(SessionUploadStatus.PermanentFailure, permanent.Status);
        Assert.Equal(422, permanent.StatusCode);
        Assert.Contains("bad payload", permanent.ErrorMessage);
    }

    [Fact]
    public async Task UploadAsync_TreatsNetworkExceptionsAsRetryable()
    {
        var uploader = new HttpCompletedSessionUploader(
            new HttpClient(new ThrowingHttpMessageHandler(new HttpRequestException("connection lost"))),
            new SimCrewOpsApiUploaderOptions { PilotApiToken = "token", TrackerVersion = "2.0.0" });

        var result = await uploader.UploadAsync(CreatePendingSession());

        Assert.Equal(SessionUploadStatus.RetryableFailure, result.Status);
        Assert.Equal("connection lost", result.ErrorMessage);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpCompletedSessionUploader BuildUploader(HttpStatusCode status, string responseBody)
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
        }));

        return new HttpCompletedSessionUploader(
            new HttpClient(handler),
            new SimCrewOpsApiUploaderOptions { PilotApiToken = "token", TrackerVersion = "2.0.0" });
    }

    private static string LoadFixture(string fileName)
    {
        var assembly = typeof(HttpCompletedSessionUploaderTests).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static PendingCompletedSession CreatePendingSession() =>
        new()
        {
            SessionId = "session-1",
            SavedUtc  = new DateTimeOffset(2026, 4, 13, 15, 0, 0, TimeSpan.Zero),
            State = new FlightSessionRuntimeState
            {
                Context = new FlightSessionContext
                {
                    BidId                = "bid-123",
                    DepartureAirportIcao = "KJFK",
                    ArrivalAirportIcao   = "KMIA",
                    FlightMode           = "career",
                    ScheduledBlockHours  = 3.1,
                },
                CurrentPhase = FlightPhase.Arrival,
                BlockTimes   = new FlightSessionBlockTimes
                {
                    BlocksOffUtc = new DateTimeOffset(2026, 4, 13, 12,  0, 0, TimeSpan.Zero),
                    WheelsOffUtc = new DateTimeOffset(2026, 4, 13, 12, 15, 0, TimeSpan.Zero),
                    WheelsOnUtc  = new DateTimeOffset(2026, 4, 13, 14, 45, 0, TimeSpan.Zero),
                    BlocksOnUtc  = new DateTimeOffset(2026, 4, 13, 14, 52, 0, TimeSpan.Zero),
                },
                ScoreInput = new FlightScoreInput
                {
                    Landing = new LandingMetrics
                    {
                        BounceCount                       = 2,
                        TouchdownVerticalSpeedFpm         = 188, // positive magnitude → payload will be -188
                        TouchdownBankAngleDegrees         = 1.8,
                        TouchdownIndicatedAirspeedKnots   = 134,
                        TouchdownPitchAngleDegrees        = 2.4,
                        TouchdownGForce                   = 134, // simplified for test
                    },
                    Safety = new SafetyMetrics
                    {
                        CrashDetected   = true,
                        OverspeedEvents = 3,
                        StallEvents     = 1,
                        GpwsEvents      = 2,
                    },
                },
                ScoreResult = new ScoreResult(100, 84.5, "B", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
            },
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            sendAsync(request);
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }
}
