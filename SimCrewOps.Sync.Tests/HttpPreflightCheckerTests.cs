using System.Net;
using System.Reflection;
using System.Text;
using SimCrewOps.Sync.Models;
using SimCrewOps.Sync.Sync;
using Xunit;

namespace SimCrewOps.Sync.Tests;

public sealed class HttpPreflightCheckerTests
{
    private static readonly SimCrewOpsApiUploaderOptions DefaultOptions = new()
    {
        BaseUri       = new Uri("https://simcrewops.com"),
        PilotApiToken = "test-token",
        TrackerVersion = "3.0.0",
    };

    [Fact]
    public async Task CheckAsync_Parses200WithIsGroundedFalse()
    {
        var json = LoadFixture("preflight_success.json");
        var checker = BuildChecker(HttpStatusCode.OK, json);

        var result = await checker.CheckAsync();

        Assert.NotNull(result);
        Assert.Equal("citationx8",  result!.PilotUsername);
        Assert.Equal("CitationX8",  result.PilotDisplayName);
        Assert.Equal("Captain",     result.Rank);
        Assert.False(result.IsGrounded);
        Assert.Null(result.StrikeId);
        Assert.Null(result.GroundingReason);
        Assert.False(result.InCrewRest);
    }

    [Fact]
    public async Task CheckAsync_Parses200WithIsGroundedTrue()
    {
        var json = LoadFixture("preflight_grounded.json");
        var checker = BuildChecker(HttpStatusCode.OK, json);

        var result = await checker.CheckAsync();

        Assert.NotNull(result);
        Assert.True(result!.IsGrounded);
        Assert.Equal("cmoj6kgih01rllm66afr2id7i", result.StrikeId);
        Assert.Equal("tail_strike",                result.StrikeType);
        Assert.Equal("Unresolved immediate strike: Tail Strike", result.GroundingReason);
        Assert.NotNull(result.GroundingAction);
    }

    [Fact]
    public async Task CheckAsync_ReturnsNullOnNetworkFailure()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("connection refused"));
        var checker = new HttpPreflightChecker(new HttpClient(handler), DefaultOptions);

        var result = await checker.CheckAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckAsync_ReturnsNullOnNon2xxResponse()
    {
        var checker = BuildChecker(HttpStatusCode.Unauthorized, "{}");

        var result = await checker.CheckAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckAsync_UsesCorrectPathAndBearerToken()
    {
        string? capturedAuth = null;
        Uri? capturedUri = null;

        var handler = new StubHttpMessageHandler(request =>
        {
            capturedAuth = request.Headers.Authorization?.ToString();
            capturedUri  = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(LoadFixture("preflight_success.json"), Encoding.UTF8, "application/json"),
            };
        });

        var checker = new HttpPreflightChecker(new HttpClient(handler), DefaultOptions);
        await checker.CheckAsync();

        Assert.Equal("Bearer test-token", capturedAuth);
        Assert.Equal(
            new Uri("https://simcrewops.com" + HttpPreflightChecker.PreflightPath),
            capturedUri);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IPreflightChecker BuildChecker(HttpStatusCode status, string responseBody)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
        });

        return new HttpPreflightChecker(new HttpClient(handler), DefaultOptions);
    }

    private static string LoadFixture(string fileName)
    {
        var assembly = typeof(HttpPreflightCheckerTests).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }
}
