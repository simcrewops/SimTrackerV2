using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SimCrewOps.Sync.Models;
using SimCrewOps.Sync.Sync;
using Xunit;

namespace SimCrewOps.Sync.Tests;

public sealed class HttpLivePositionUploaderTests
{
    [Fact]
    public async Task SendPositionAsync_PostsExpectedRequestAndTreats200AsSuccess()
    {
        var handler = new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        var uploader = new HttpLivePositionUploader(
            httpClient,
            new SimCrewOpsApiUploaderOptions
            {
                BaseUri = new Uri("https://simcrewops.com", UriKind.Absolute),
                PilotApiToken = "token-123",
                TrackerVersion = "2.0.0",
            });

        var sent = await uploader.SendPositionAsync(new LivePositionPayload
        {
            Latitude = 40.64,
            Longitude = -73.78,
            HeadingMagnetic = 271.4,
            AltitudeFt = 4200,
            AltitudeAglFt = 1800,
            IndicatedAirspeedKts = 155,
            GroundSpeedKts = 162,
            VerticalSpeedFpm = -720,
            Phase = "Approach",
            FlightMode = "career",
            BidId = "48291",
        });

        Assert.True(sent);
        Assert.NotNull(handler.RecordedRequest);
        Assert.Equal(HttpMethod.Post, handler.RecordedRequest!.Method);
        Assert.Equal("https://simcrewops.com/api/tracker/position", handler.RecordedRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.RecordedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("token-123", handler.RecordedRequest.Headers.Authorization.Parameter);

        var payload = JsonSerializer.Deserialize<LivePositionPayload>(
            await handler.RecordedRequest.Content!.ReadAsStringAsync(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(payload);
        Assert.Equal(271.4, payload!.HeadingMagnetic);
        Assert.Equal("Approach", payload.Phase);
        Assert.Equal("career", payload.FlightMode);
        Assert.Equal("48291", payload.BidId);
    }

    [Fact]
    public async Task SendPositionAsync_ReturnsFalseOnNonSuccessStatus()
    {
        var handler = new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var httpClient = new HttpClient(handler);
        var uploader = new HttpLivePositionUploader(
            httpClient,
            new SimCrewOpsApiUploaderOptions
            {
                BaseUri = new Uri("https://simcrewops.com", UriKind.Absolute),
                PilotApiToken = "token-123",
            });

        var sent = await uploader.SendPositionAsync(new LivePositionPayload());

        Assert.False(sent);
    }

    [Fact]
    public async Task SendPositionAsync_ReturnsFalseOnNetworkException()
    {
        using var httpClient = new HttpClient(new ThrowingHttpMessageHandler(new HttpRequestException("offline")));
        var uploader = new HttpLivePositionUploader(
            httpClient,
            new SimCrewOpsApiUploaderOptions
            {
                BaseUri = new Uri("https://simcrewops.com", UriKind.Absolute),
                PilotApiToken = "token-123",
            });

        var sent = await uploader.SendPositionAsync(new LivePositionPayload());

        Assert.False(sent);
    }

    private sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? RecordedRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RecordedRequest = CloneRequest(request);
            return Task.FromResult(response);
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            if (request.Headers.Authorization is AuthenticationHeaderValue authorization)
            {
                clone.Headers.Authorization = new AuthenticationHeaderValue(authorization.Scheme, authorization.Parameter);
            }

            if (request.Content is not null)
            {
                clone.Content = new StringContent(request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }

            return clone;
        }
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }
}
