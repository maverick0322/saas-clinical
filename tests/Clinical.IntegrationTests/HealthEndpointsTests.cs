using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Clinical.IntegrationTests;

public sealed class HealthEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetHealthLive_ReturnsOkWithProcessOnlyLivenessPayload()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/health/live", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = document.RootElement;

        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.Equal("saas-clinical", root.GetProperty("service").GetString());
        Assert.Equal("live", root.GetProperty("probe").GetString());
        Assert.Equal("process-only", root.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task GetHealthReady_ReturnsOkWithProcessOnlyReadinessPayload()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/health/ready", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = document.RootElement;

        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.Equal("saas-clinical", root.GetProperty("service").GetString());
        Assert.Equal("ready", root.GetProperty("probe").GetString());
        Assert.Equal("process-only", root.GetProperty("scope").GetString());
    }

    [Theory]
    [InlineData("/health/unknown")]
    [InlineData("/")]
    [InlineData("/weatherforecast")]
    [InlineData("/v1/patients")]
    [InlineData("/api/patients")]
    public async Task GetUnmappedOrSpeculativeRoutes_ReturnNotFound(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void HealthChecksRegistration_ContainsOnlySelfProcessCheckWithLiveAndReadyTags()
    {
        var options = factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        var registration = Assert.Single(options.Registrations);
        Assert.Equal("self", registration.Name);
        Assert.Contains("live", registration.Tags);
        Assert.Contains("ready", registration.Tags);
    }
}
