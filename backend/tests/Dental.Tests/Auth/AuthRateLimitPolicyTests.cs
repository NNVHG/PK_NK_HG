using System.Net;
using Dental.Api.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dental.Tests.Auth;

public sealed class AuthRateLimitPolicyTests
{
    [Fact]
    public async Task Login_AllowsRequestsWithinLimit_ThenReturnsVietnamese429()
    {
        await using var host = await StartHostAsync(loginLimit: 2);

        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/login")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/login")).StatusCode);

        using var response = await host.Client.GetAsync("/api/auth/login");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Contains("Bạn đã gửi quá nhiều yêu cầu", body);
    }

    [Fact]
    public async Task RegisterHasSeparateLimit_AndOtherEndpointsAreNotLimited()
    {
        await using var host = await StartHostAsync(loginLimit: 1, registerLimit: 1);

        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/register")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await host.Client.GetAsync("/api/auth/register")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/login")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await host.Client.GetAsync("/api/auth/login")).StatusCode);

        for (var requestNumber = 0; requestNumber < 5; requestNumber++)
        {
            Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/other")).StatusCode);
        }
    }

    private static async Task<RateLimitTestHost> StartHostAsync(int loginLimit, int registerLimit = 1)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:Auth:Login:PermitLimit"] = loginLimit.ToString(),
            ["RateLimiting:Auth:Login:WindowSeconds"] = "60",
            ["RateLimiting:Auth:Register:PermitLimit"] = registerLimit.ToString(),
            ["RateLimiting:Auth:Register:WindowSeconds"] = "60",
        });
        builder.Services.AddRateLimiter(options => AuthRateLimitPolicies.Configure(options, builder.Configuration));

        var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.MapGet("/api/auth/login", () => Results.Ok())
            .RequireRateLimiting(AuthRateLimitPolicies.Login);
        app.MapGet("/api/auth/register", () => Results.Ok())
            .RequireRateLimiting(AuthRateLimitPolicies.Register);
        app.MapGet("/api/other", () => Results.Ok());

        try
        {
            await app.StartAsync();
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new RateLimitTestHost(app, new HttpClient { BaseAddress = new Uri(address) });
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    private sealed class RateLimitTestHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }
}
