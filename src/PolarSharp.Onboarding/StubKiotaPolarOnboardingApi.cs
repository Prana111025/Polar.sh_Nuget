using Microsoft.Extensions.Logging;

namespace PolarSharp.Onboarding;

/// <summary>
/// Fail-loud default <see cref="IPolarOnboardingApi"/> registered by
/// <see cref="Extensions.OnboardingBuilderExtensions.AddPolarOnboarding"/> so that DI
/// resolution of <see cref="IPolarOnboardingClient"/> succeeds even before the real Kiota
/// wiring lands under TASK-V20-006.
/// </summary>
/// <remarks>
/// <para>
/// Every method throws <see cref="NotSupportedException"/> with a message pointing at
/// TASK-V20-006 so a host that follows the package quickstart gets a clear, actionable
/// error rather than an opaque DI <see cref="InvalidOperationException"/> or a silent
/// no-op. Mirrors the <c>PolarClientPublishingApi</c> "stub returns UnexpectedFailure"
/// pattern, adapted for the throwing return type — <see cref="IPolarOnboardingApi"/>
/// methods return <see cref="Task{TResult}"/> directly (no <c>Result</c> envelope), so a
/// throw is the only failure shape available.
/// </para>
/// <para>
/// Hosts that need real onboarding today supply their own <see cref="IPolarOnboardingApi"/>
/// registration BEFORE calling <c>AddPolarOnboarding</c> — the registration uses
/// <c>TryAddScoped</c> so the host's wiring wins:
/// <code>
/// services.AddScoped&lt;IPolarOnboardingApi, MyKiotaPolarOnboardingApi&gt;();
/// services.AddPolarOnboarding(configuration);
/// </code>
/// </para>
/// </remarks>
internal sealed class StubKiotaPolarOnboardingApi : IPolarOnboardingApi
{
    private const string TaskReference = "TASK-V20-006";

    private readonly ILogger<StubKiotaPolarOnboardingApi> _logger;

    public StubKiotaPolarOnboardingApi(ILogger<StubKiotaPolarOnboardingApi> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<PolarOrganizationCreated> CreateOrganizationAsync(
        ProgrammaticOnboardingRequest request,
        CancellationToken ct = default) =>
        ThrowAsync<PolarOrganizationCreated>(nameof(CreateOrganizationAsync), request?.OrganizationSlug ?? "(null)");

    /// <inheritdoc/>
    public Task<PolarOrganizationAccessTokenCreated> CreateOrganizationAccessTokenAsync(
        string organizationId,
        IReadOnlyList<string> scopes,
        CancellationToken ct = default) =>
        ThrowAsync<PolarOrganizationAccessTokenCreated>(nameof(CreateOrganizationAccessTokenAsync), organizationId);

    /// <inheritdoc/>
    public Task<PolarWebhookEndpointCreated> CreateWebhookEndpointAsync(
        string organizationId,
        string callbackUrl,
        IReadOnlyList<string> events,
        CancellationToken ct = default) =>
        ThrowAsync<PolarWebhookEndpointCreated>(nameof(CreateWebhookEndpointAsync), organizationId);

    /// <inheritdoc/>
    public Task<PolarOAuthTokenResponse> ExchangeOAuthCodeAsync(
        string code,
        string clientId,
        string clientSecret,
        string redirectUri,
        CancellationToken ct = default) =>
        ThrowAsync<PolarOAuthTokenResponse>(nameof(ExchangeOAuthCodeAsync), clientId);

    private Task<T> ThrowAsync<T>(string method, string identifier)
    {
        _logger.LogError(
            "StubKiotaPolarOnboardingApi.{Method} called for '{Identifier}' but the real Kiota wiring is deferred to {TaskRef} — throwing NotSupportedException.",
            method, identifier, TaskReference);
        throw new NotSupportedException(
            $"PolarSharp.Onboarding's default IPolarOnboardingApi is a fail-loud stub: the Kiota-backed implementation is deferred to {TaskReference}. " +
            $"Either supply your own IPolarOnboardingApi via services.AddScoped<IPolarOnboardingApi, YourImpl>() BEFORE calling AddPolarOnboarding, " +
            $"or wait for the sandbox-validated wrapper. Called method: {method}.");
    }
}
