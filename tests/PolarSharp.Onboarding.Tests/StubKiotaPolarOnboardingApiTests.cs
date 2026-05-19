using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.Onboarding;
using PolarSharp.Onboarding.Extensions;

namespace PolarSharp.Onboarding.Tests;

/// <summary>
/// Regression tests for the DI trap that shipped pre-fix: <see cref="IPolarOnboardingApi"/>
/// had no concrete registration, so resolving <see cref="IPolarOnboardingClient"/> threw
/// <see cref="InvalidOperationException"/> at runtime on first use. Now the default
/// <see cref="StubKiotaPolarOnboardingApi"/> is registered by
/// <see cref="OnboardingBuilderExtensions.AddPolarOnboarding"/>, so DI resolution succeeds
/// and a clearer <see cref="NotSupportedException"/> surfaces only when an onboarding
/// call is actually made.
/// </summary>
public sealed class StubKiotaPolarOnboardingApiTests
{
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddPolarOnboarding(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddPolarOnboarding_registers_a_default_IPolarOnboardingApi_so_DI_resolution_succeeds()
    {
        using var sp = BuildServiceProvider();
        using var scope = sp.CreateScope();

        // Pre-fix this would throw InvalidOperationException because IPolarOnboardingApi
        // had no registration. The whole point of the stub is to make this succeed.
        var client = scope.ServiceProvider.GetRequiredService<IPolarOnboardingClient>();
        Assert.NotNull(client);

        var api = scope.ServiceProvider.GetRequiredService<IPolarOnboardingApi>();
        Assert.IsType<StubKiotaPolarOnboardingApi>(api);
    }

    [Fact]
    public async Task Stub_CreateOrganizationAsync_throws_NotSupportedException_with_TASK_V20_006_pointer()
    {
        using var sp = BuildServiceProvider();
        using var scope = sp.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IPolarOnboardingApi>();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            api.CreateOrganizationAsync(new ProgrammaticOnboardingRequest
            {
                OrganizationName = "Acme",
                OrganizationSlug = "acme",
                Email = "ops@acme.example.com",
                CountryCode = "US",
                Currency = "USD",
                WebhookCallbackUrl = "https://app.example.com/hooks/polar",
                WebhookEvents = ["order.created"],
            }));

        Assert.Contains("TASK-V20-006", ex.Message);
        Assert.Contains("CreateOrganizationAsync", ex.Message);
        Assert.Contains("supply your own IPolarOnboardingApi", ex.Message);
    }

    [Fact]
    public async Task Stub_CreateOrganizationAccessTokenAsync_throws_NotSupportedException()
    {
        using var sp = BuildServiceProvider();
        using var scope = sp.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IPolarOnboardingApi>();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            api.CreateOrganizationAccessTokenAsync("org_test_001", ["products:write"]));

        Assert.Contains("TASK-V20-006", ex.Message);
        Assert.Contains("CreateOrganizationAccessTokenAsync", ex.Message);
    }

    [Fact]
    public async Task Stub_CreateWebhookEndpointAsync_throws_NotSupportedException()
    {
        using var sp = BuildServiceProvider();
        using var scope = sp.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IPolarOnboardingApi>();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            api.CreateWebhookEndpointAsync("org_test_001", "https://app.example.com/hooks", ["order.created"]));

        Assert.Contains("TASK-V20-006", ex.Message);
        Assert.Contains("CreateWebhookEndpointAsync", ex.Message);
    }

    [Fact]
    public async Task Stub_ExchangeOAuthCodeAsync_throws_NotSupportedException()
    {
        using var sp = BuildServiceProvider();
        using var scope = sp.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IPolarOnboardingApi>();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            api.ExchangeOAuthCodeAsync("auth_code", "client_id", "client_secret", "https://app.example.com/callback"));

        Assert.Contains("TASK-V20-006", ex.Message);
        Assert.Contains("ExchangeOAuthCodeAsync", ex.Message);
    }

    [Fact]
    public void Host_supplied_IPolarOnboardingApi_takes_precedence_over_the_stub()
    {
        // Hosts that already have a real impl wire it BEFORE AddPolarOnboarding; the
        // TryAdd in AddPolarOnboarding must NOT overwrite their registration.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IPolarOnboardingApi, FakePolarOnboardingApi>();

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddPolarOnboarding(config);

        using ServiceProvider sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IPolarOnboardingApi>();

        Assert.IsType<FakePolarOnboardingApi>(api);
        Assert.IsNotType<StubKiotaPolarOnboardingApi>(api);
    }
}
