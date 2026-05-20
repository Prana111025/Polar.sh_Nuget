using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Commands;
using PolarSharp.PrepaidWallets.Abstractions.Stores;
using PolarSharp.PrepaidWallets.Behaviors;
using PolarSharp.PrepaidWallets.Domain;
using PolarSharp.PrepaidWallets.Handlers;
using PolarSharp.PrepaidWallets.Serialization;
using PolarSharp.PrepaidWallets.Stores;
using PolarSharp.PrepaidWallets.Validation;

namespace PolarSharp.PrepaidWallets;

/// <summary>
/// Registration extensions for the PolarSharp PrepaidWallets feature.
/// </summary>
/// <remarks>
/// <para>
/// Per Case Study 02 "Event-Sourced Wallet with Comprehensive Economic Modeling", the wallet ships
/// as an event-sourced aggregate + MediatR + CQRS + projections + snapshot strategy. The core
/// package contains the domain logic; storage backends (Marten or EF Core) plug in via separate
/// provider packages.
/// </para>
/// <para>
/// The default <c>AddPolarPrepaidWallets()</c> registers the aggregate loader, the MediatR
/// pipeline (concurrency-retry → validation → logging → handler), the JSON event serializer, the
/// stride snapshot policy, and the in-memory event/snapshot stores. Hosts override the in-memory
/// stores by calling, for example, <c>UseMartenWalletEventStore(...)</c> from
/// <c>PolarSharp.PrepaidWallets.EventStore.Marten</c>.
/// </para>
/// </remarks>
public static class PrepaidWalletsBuilderExtensions
{
    /// <summary>Registers the PolarSharp PrepaidWallets feature.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="snapshotStride">Number of events between automatic snapshots (defaults to 50; tune to 10 for Cosmos).</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddPolarPrepaidWallets(
        this IServiceCollection services,
        int snapshotStride = StrideSnapshotPolicy.DefaultStride)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(PrepaidWalletsBuilderExtensions).Assembly));

        services.TryAddSingleton<ISystemClock, SystemClock>();
        services.TryAddSingleton<IWalletEventSerializer, JsonWalletEventSerializer>();
        services.TryAddSingleton<ISnapshotPolicy>(_ => new StrideSnapshotPolicy(snapshotStride));

        services.TryAddSingleton<IWalletEventStore, InMemoryWalletEventStore>();
        services.TryAddSingleton<IWalletSnapshotStore, InMemoryWalletSnapshotStore>();

        services.TryAddScoped<WalletAggregateLoader>();

        services.AddSingleton<IValidator<OpenWalletCommand>, OpenWalletCommandValidator>();
        services.AddSingleton<IValidator<FundWalletCommand>, FundWalletCommandValidator>();
        services.AddSingleton<IValidator<DebitWalletCommand>, DebitWalletCommandValidator>();
        services.AddSingleton<IValidator<CreditWalletCommand>, CreditWalletCommandValidator>();
        services.AddSingleton<IValidator<RefundWalletCommand>, RefundWalletCommandValidator>();
        services.AddSingleton<IValidator<FreezeWalletCommand>, FreezeWalletCommandValidator>();
        services.AddSingleton<IValidator<UnfreezeWalletCommand>, UnfreezeWalletCommandValidator>();
        services.AddSingleton<IValidator<CloseWalletCommand>, CloseWalletCommandValidator>();

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(LoggingBehavior<,>));
        services.AddTransient(
            typeof(IPipelineBehavior<OpenWalletCommand, WalletCommandResult>),
            typeof(ConcurrencyRetryBehavior<OpenWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<OpenWalletCommand, WalletCommandResult>),
            typeof(ValidationBehavior<OpenWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<FundWalletCommand, WalletCommandResult>),
            typeof(ConcurrencyRetryBehavior<FundWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<FundWalletCommand, WalletCommandResult>),
            typeof(ValidationBehavior<FundWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<DebitWalletCommand, WalletCommandResult>),
            typeof(ConcurrencyRetryBehavior<DebitWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<DebitWalletCommand, WalletCommandResult>),
            typeof(ValidationBehavior<DebitWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<CreditWalletCommand, WalletCommandResult>),
            typeof(ConcurrencyRetryBehavior<CreditWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<CreditWalletCommand, WalletCommandResult>),
            typeof(ValidationBehavior<CreditWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<RefundWalletCommand, WalletCommandResult>),
            typeof(ConcurrencyRetryBehavior<RefundWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<RefundWalletCommand, WalletCommandResult>),
            typeof(ValidationBehavior<RefundWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<FreezeWalletCommand, WalletCommandResult>),
            typeof(ConcurrencyRetryBehavior<FreezeWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<FreezeWalletCommand, WalletCommandResult>),
            typeof(ValidationBehavior<FreezeWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<UnfreezeWalletCommand, WalletCommandResult>),
            typeof(ConcurrencyRetryBehavior<UnfreezeWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<UnfreezeWalletCommand, WalletCommandResult>),
            typeof(ValidationBehavior<UnfreezeWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<CloseWalletCommand, WalletCommandResult>),
            typeof(ConcurrencyRetryBehavior<CloseWalletCommand>));
        services.AddTransient(
            typeof(IPipelineBehavior<CloseWalletCommand, WalletCommandResult>),
            typeof(ValidationBehavior<CloseWalletCommand>));

        return services;
    }
}
