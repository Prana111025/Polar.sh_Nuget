using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.Onboarding.Wizard;

namespace PolarSharp.Onboarding.Extensions;

/// <summary>DI registration helpers for <c>PolarSharp.Onboarding</c>.</summary>
public static class OnboardingBuilderExtensions
{
    /// <summary>
    /// Registers the onboarding client (<see cref="IPolarOnboardingClient"/>) and core
    /// services. The default sink is the no-op sink; host wires
    /// <see cref="EfMultiTenantStoreSink"/> (or its own) via <see cref="UseEfTenantStoreSink"/>.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configuration">Application configuration — bound to <see cref="PolarOnboardingOptions"/>.</param>
    /// <returns>A builder for chaining further onboarding registrations.</returns>
    public static PolarOnboardingBuilder AddPolarOnboarding(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<PolarOnboardingOptions>()
            .Bind(configuration.GetSection(PolarOnboardingOptions.SectionName))
            .ValidateOnStart();

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<IOnboardedTenantSink, NoOpOnboardedTenantSink>();

        // Fail-loud default for IPolarOnboardingApi so DI resolution of
        // IPolarOnboardingClient succeeds without the host having to wire anything for the
        // common case. The stub throws NotSupportedException on first call with a clear
        // pointer at TASK-V20-006. TryAdd lets a host that already registered their own
        // IPolarOnboardingApi (e.g. a custom Kiota wrapper) win. See
        // StubKiotaPolarOnboardingApi for the rationale.
        services.TryAddScoped<IPolarOnboardingApi, StubKiotaPolarOnboardingApi>();

        services.TryAddScoped<IPolarOnboardingClient, PolarOnboardingClient>();

        return new PolarOnboardingBuilder(services, configuration);
    }

    /// <summary>Replaces the default no-op sink with the EF-backed tenant-store sink.</summary>
    /// <param name="builder">The onboarding builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <remarks>
    /// The EF-backed sink requires the host to have already registered
    /// <c>PolarSharp.MultiTenant.EntityFrameworkCore</c> via <c>.UseSqlServer()</c> /
    /// <c>.UseSqlite()</c> / <c>.UsePostgreSql()</c>.
    /// </remarks>
    public static PolarOnboardingBuilder UseEfTenantStoreSink(this PolarOnboardingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.RemoveAll<IOnboardedTenantSink>();
        builder.Services.AddScoped<IOnboardedTenantSink, EfMultiTenantStoreSink>();
        return builder;
    }

    /// <summary>Registers an <see cref="IOnboardingPostProcessor"/> implementation.</summary>
    /// <typeparam name="TProcessor">The post-processor type.</typeparam>
    public static PolarOnboardingBuilder AddPostProcessor<TProcessor>(this PolarOnboardingBuilder builder)
        where TProcessor : class, IOnboardingPostProcessor
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<IOnboardingPostProcessor, TProcessor>();
        return builder;
    }

    /// <summary>Enables the step-by-step wizard API (<see cref="IOnboardingWizard"/>).</summary>
    /// <param name="builder">The onboarding builder.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Adds the <see cref="OnboardingSessionEntity"/> mapping to the registered
    /// <see cref="PolarSharp.MultiTenant.EntityFrameworkCore.PolarTenantDbContext"/> via
    /// <see cref="OnboardingModelCustomizer"/>, registers the wizard service, and starts
    /// the <see cref="OnboardingSessionExpirationCleaner"/> hosted service.
    /// </para>
    /// <para>
    /// Requires the host to have already called <c>AddDataProtection()</c> — the wizard
    /// encrypts in-flight translation API keys via the Data Protection API.
    /// </para>
    /// </remarks>
    public static PolarOnboardingBuilder AddWizard(this PolarOnboardingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddDataProtection();
        builder.Services.TryAddScoped<IOnboardingWizard, OnboardingWizard>();

        // Layer the OnboardingSessionEntity onto whichever DbContext is resolved for the
        // tenant store. EF's IModelCustomizer is process-wide; this picks up any
        // PolarTenantDbContext registered upstream by the EF provider package.
        builder.Services.AddSingleton<IModelCustomizer, OnboardingModelCustomizer>();

        builder.Services.AddHostedService<OnboardingSessionExpirationCleaner>();

        return builder;
    }
}

/// <summary>Fluent builder returned by <see cref="OnboardingBuilderExtensions.AddPolarOnboarding"/>.</summary>
public sealed class PolarOnboardingBuilder
{
    /// <summary>The underlying DI service collection.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Application configuration handle.</summary>
    public IConfiguration Configuration { get; }

    /// <summary>Initializes a new builder.</summary>
    public PolarOnboardingBuilder(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        Services = services;
        Configuration = configuration;
    }
}
