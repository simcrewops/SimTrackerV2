using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using SimCrewOps.Persistence.Models;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Sync.Models;
using SimCrewOps.Sync.Sync;
using Xunit;

namespace SimCrewOps.Sync.Tests;

public sealed class HttpCompletedSessionUploaderTests
{
    [Fact]
    public void RequestMapper_MapsExpectedSimSessionFields()
    {
        var session = CreatePendingSession();
        var mapper = new SimSessionUploadRequestMapper();

        var request = mapper.Map(session, "1.2.3");

        Assert.Equal(2, request.Bounces);
        Assert.Equal(188, request.TouchdownVS);
        Assert.Equal(1.8, request.TouchdownBank);
        Assert.Equal(134, request.TouchdownIAS);
        Assert.Equal(2.4, request.TouchdownPitch);
        Assert.Equal(session.State.BlockTimes.BlocksOffUtc, request.ActualBlocksOff);
        Assert.Equal(session.State.BlockTimes.WheelsOffUtc, request.ActualWheelsOff);
        Assert.Equal(session.State.BlockTimes.WheelsOnUtc, request.ActualWheelsOn);
        Assert.Equal(session.State.BlockTimes.BlocksOnUtc, request.ActualBlocksOn);
        Assert.Equal(2.8667, request.BlockTimeActual);
        Assert.Equal(3.1, request.BlockTimeScheduled);
        Assert.True(request.CrashDetected);
        Assert.Equal(3, request.OverspeedEvents);
        Assert.Equal(1, request.StallEvents);
        Assert.Equal(2, request.GpwsEvents);
        Assert.Equal("B", request.Grade);
        Assert.Equal(84.5, request.ScoreFinal);
        Assert.Equal("1.2.3", request.TrackerVersion);
        Assert.Equal("career", request.FlightMode);
        Assert.Equal("bid-123", request.BidId);
    }

    [Fact]
    public async Task UploadAsync_PostsExpectedRequestAndTreats201AsSuccess()
    {
        string? capturedAuthorization = null;
        string? capturedBody = null;
        Uri? capturedUri = null;

        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedAuthorization = request.Headers.Authorization?.ToString();
            capturedUri = request.RequestUri;
            capturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var uploader = new HttpCompletedSessionUploader(
            new HttpClient(handler),
            new SimCrewOpsApiUploaderOptions
            {
                BaseUri = new Uri("https://simcrewops.com"),
                SimSessionsPath = "/api/sim-sessions",
                PilotApiToken = "secret-token",
                TrackerVersion = "2.0.0",
            });

        var result = await uploader.UploadAsync(CreatePendingSession());

        Assert.Equal(SessionUploadStatus.Success, result.Status);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("Bearer secret-token", capturedAuthorization);
        Assert.Equal(new Uri("https://simcrewops.com/api/sim-sessions"), capturedUri);
        Assert.NotNull(capturedBody);

        var request = JsonSerializer.Deserialize<SimSessionUploadRequest>(capturedBody!);
        Assert.NotNull(request);
        Assert.Equal(2, request!.Bounces);
        Assert.Equal(188, request.TouchdownVS);
        Assert.Equal(1.8, request.TouchdownBank);
        Assert.Equal(134, request.TouchdownIAS);
        Assert.Equal(2.4, request.TouchdownPitch);
        Assert.Equal(84.5, request.ScoreFinal);
        Assert.Equal("2.0.0", request.TrackerVersion);
    }

    [Fact]
    public async Task UploadAsync_ClassifiesRetryableAndPermanentFailures()
    {
        var retryableUploader = new HttpCompletedSessionUploader(
            new HttpClient(new StubHttpMessageHandler(_ => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("try later", Encoding.UTF8, "text/plain"),
                }))),
            new SimCrewOpsApiUploaderOptions
            {
                PilotApiToken = "token",
                TrackerVersion = "2.0.0",
            });

        var permanentUploader = new HttpCompletedSessionUploader(
            new HttpClient(new StubHttpMessageHandler(_ => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
                {
                    Content = new StringContent("bad payload", Encoding.UTF8, "text/plain"),
                }))),
            new SimCrewOpsApiUploaderOptions
            {
                PilotApiToken = "token",
                TrackerVersion = "2.0.0",
            });

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
            new HttpClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("connection lost"))),
            new SimCrewOpsApiUploaderOptions
            {
                PilotApiToken = "token",
                TrackerVersion = "2.0.0",
            });

        var result = await uploader.UploadAsync(CreatePendingSession());

        Assert.Equal(SessionUploadStatus.RetryableFailure, result.Status);
        Assert.Equal("connection lost", result.ErrorMessage);
    }

    private static PendingCompletedSession CreatePendingSession() =>
        new()
        {
            SessionId = "session-1",
            SavedUtc = new DateTimeOffset(2026, 4, 13, 15, 0, 0, TimeSpan.Zero),
            State = new FlightSessionRuntimeState
            {
                Context = new FlightSessionContext
                {
                    BidId = "bid-123",
                    DepartureAirportIcao = "KJFK",
                    ArrivalAirportIcao = "KMIA",
                    FlightMode = "career",
                    ScheduledBlockHours = 3.1,
                },
                CurrentPhase = FlightPhase.Arrival,
                BlockTimes = new FlightSessionBlockTimes
                {
                    BlocksOffUtc = new DateTimeOffset(2026, 4, 13, 12, 0, 0, TimeSpan.Zero),
                    WheelsOffUtc = new DateTimeOffset(2026, 4, 13, 12, 15, 0, TimeSpan.Zero),
                    WheelsOnUtc = new DateTimeOffset(2026, 4, 13, 14, 45, 0, TimeSpan.Zero),
                    BlocksOnUtc = new DateTimeOffset(2026, 4, 13, 14, 52, 0, TimeSpan.Zero),
                },
                ScoreInput = new FlightScoreInput
                {
                    Landing = new LandingMetrics
                    {
                        BounceCount = 2,
                        TouchdownVerticalSpeedFpm = 188,
                        TouchdownBankAngleDegrees = 1.8,
                        TouchdownIndicatedAirspeedKnots = 134,
                        TouchdownPitchAngleDegrees = 2.4,
                    },
                    Safety = new SafetyMetrics
                    {
                        CrashDetected = true,
                        OverspeedEvents = 3,
                        StallEvents = 1,
                        GpwsEvents = 2,
                    },
                },
                ScoreResult = new(100, 84.5, "B", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
            },
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            sendAsync(request);
    }
}
