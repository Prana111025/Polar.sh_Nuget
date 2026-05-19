namespace PolarSharp.Onboarding;

/// <summary>
/// Thin abstraction over the three Polar HTTP calls that compose programmatic onboarding.
/// Lets <see cref="IPolarOnboardingClient"/> orchestrate without depending directly on the
/// Kiota-generated client surface — and lets tests substitute a fake without standing up
/// real HTTP traffic to Polar.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Default registration is a fail-loud stub</strong>
/// (<c>StubKiotaPolarOnboardingApi</c>) that throws <see cref="NotSupportedException"/>
/// on every call with a message pointing at TASK-V20-006. This lets DI resolution of
/// <see cref="IPolarOnboardingClient"/> succeed without the host wiring anything up, but
/// the first onboarding call will throw with a clear, actionable message. Until the
/// Kiota-backed implementation lands under TASK-V20-006, hosts that need real onboarding
/// must supply their own implementation:
/// <code>
/// services.AddScoped&lt;IPolarOnboardingApi, MyKiotaPolarOnboardingApi&gt;();
/// services.AddPolarOnboarding(configuration);
/// </code>
/// </para>
/// <para>
/// The real implementation, when it lands, will delegate to
/// <see cref="PolarClient"/>'s typed resource clients (Organizations,
/// OrganizationAccessTokens, Webhooks). Hosts can still substitute their own
/// implementation post-V20-006 when they need custom HTTP routing, request signing, or
/// instrumentation.
/// </para>
/// </remarks>
public interface IPolarOnboardingApi
{
    /// <summary>POST <c>/v1/organizations/</c>. Returns the newly-created organization's id and slug.</summary>
    Task<PolarOrganizationCreated> CreateOrganizationAsync(
        ProgrammaticOnboardingRequest request,
        CancellationToken ct = default);

    /// <summary>POST <c>/v1/organization-access-tokens/</c>. Returns the OAT (the token value is readable ONLY in this response).</summary>
    Task<PolarOrganizationAccessTokenCreated> CreateOrganizationAccessTokenAsync(
        string organizationId,
        IReadOnlyList<string> scopes,
        CancellationToken ct = default);

    /// <summary>POST <c>/v1/webhooks/endpoints/</c>. Returns the endpoint id and shared signing secret.</summary>
    Task<PolarWebhookEndpointCreated> CreateWebhookEndpointAsync(
        string organizationId,
        string callbackUrl,
        IReadOnlyList<string> events,
        CancellationToken ct = default);

    /// <summary>POST <c>/v1/oauth2/token/</c> (form-urlencoded, grant_type=authorization_code). Returns the issued access + refresh tokens and the resolved organization id.</summary>
    Task<PolarOAuthTokenResponse> ExchangeOAuthCodeAsync(
        string code,
        string clientId,
        string clientSecret,
        string redirectUri,
        CancellationToken ct = default);
}

/// <summary>Returned by <see cref="IPolarOnboardingApi.CreateOrganizationAsync"/>.</summary>
public sealed record PolarOrganizationCreated(string Id, string Slug);

/// <summary>Returned by <see cref="IPolarOnboardingApi.CreateOrganizationAccessTokenAsync"/>. The <see cref="Token"/> string is readable ONLY here — capture it immediately.</summary>
public sealed record PolarOrganizationAccessTokenCreated(string Id, string Token, IReadOnlyList<string> Scopes);

/// <summary>Returned by <see cref="IPolarOnboardingApi.CreateWebhookEndpointAsync"/>.</summary>
public sealed record PolarWebhookEndpointCreated(string Id, string Secret);

/// <summary>Returned by <see cref="IPolarOnboardingApi.ExchangeOAuthCodeAsync"/>.</summary>
public sealed record PolarOAuthTokenResponse(string AccessToken, string? RefreshToken, string OrganizationId, IReadOnlyList<string> GrantedScopes);
