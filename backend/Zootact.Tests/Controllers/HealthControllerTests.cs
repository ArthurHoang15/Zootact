using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Zootact.API.Controllers;
using Zootact.API.Services;

namespace Zootact.Tests.Controllers;

public sealed class HealthControllerTests
{
    [Fact]
    public async Task GetLive_ReturnsOk()
    {
        var controller = new HealthController(new StubHealthStatusService(
            liveResponse: new HealthStatusResponse(
                "healthy",
                DateTimeOffset.UtcNow,
                "zootact-api",
                1234,
                [])));

        var result = await controller.GetLive(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<HealthStatusResponse>(okResult.Value);
        Assert.Equal("healthy", payload.Status);
    }

    [Fact]
    public async Task GetReady_ReturnsOkWhenCoreDependenciesAreHealthy()
    {
        var controller = new HealthController(new StubHealthStatusService(
            readyResponse: new HealthStatusResponse(
                "healthy",
                DateTimeOffset.UtcNow,
                "zootact-api",
                1234,
                [new DependencyStatusResponse("postgres", true, "ok"), new DependencyStatusResponse("redis", true, "ok")])));

        var result = await controller.GetReady(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<HealthStatusResponse>(okResult.Value);
        Assert.Equal("healthy", payload.Status);
    }

    [Fact]
    public async Task GetReady_ReturnsServiceUnavailableWhenCoreDependencyFails()
    {
        var controller = new HealthController(new StubHealthStatusService(
            readyResponse: new HealthStatusResponse(
                "unhealthy",
                DateTimeOffset.UtcNow,
                "zootact-api",
                1234,
                [new DependencyStatusResponse("postgres", false, "down"), new DependencyStatusResponse("redis", true, "ok")])));

        var result = await controller.GetReady(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        var payload = Assert.IsType<HealthStatusResponse>(objectResult.Value);
        Assert.Equal("unhealthy", payload.Status);
    }

    [Fact]
    public async Task GetDependencies_ReturnsOk()
    {
        var controller = new HealthController(new StubHealthStatusService(
            dependenciesResponse: new HealthStatusResponse(
                "degraded",
                DateTimeOffset.UtcNow,
                "zootact-api",
                1234,
                [
                    new DependencyStatusResponse("postgres", true, "ok"),
                    new DependencyStatusResponse("redis", true, "ok"),
                    new DependencyStatusResponse("ai-service", false, "offline"),
                ])));

        var result = await controller.GetDependencies(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<HealthStatusResponse>(okResult.Value);
        Assert.Equal("degraded", payload.Status);
        Assert.Equal(3, payload.Dependencies.Count);
    }

    private sealed class StubHealthStatusService(
        HealthStatusResponse? readyResponse = null,
        HealthStatusResponse? liveResponse = null,
        HealthStatusResponse? dependenciesResponse = null) : IHealthStatusService
    {
        public Task<HealthStatusResponse> GetLiveAsync()
        {
            return Task.FromResult(liveResponse ?? new HealthStatusResponse("healthy", DateTimeOffset.UtcNow, "zootact-api", 1, []));
        }

        public Task<HealthStatusResponse> GetReadyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(readyResponse ?? new HealthStatusResponse("healthy", DateTimeOffset.UtcNow, "zootact-api", 1, []));
        }

        public Task<HealthStatusResponse> GetDependenciesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(dependenciesResponse ?? new HealthStatusResponse("healthy", DateTimeOffset.UtcNow, "zootact-api", 1, []));
        }
    }
}
