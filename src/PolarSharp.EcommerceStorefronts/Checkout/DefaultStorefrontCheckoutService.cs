using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Identity;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;
using PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing;
using CartEntity = PolarSharp.EcommerceStorefronts.Abstractions.Cart.Cart;

namespace PolarSharp.EcommerceStorefronts.Checkout;

/// <summary>
/// Default implementation of <see cref="IStorefrontCheckoutService"/>. Registered by
/// <c>AddPolarStorefrontsCore()</c>.
/// </summary>
/// <remarks>
/// <see cref="InitiateCheckoutAsync"/> snapshots the current cart into a
/// <see cref="CheckoutSession"/> and persists it via <see cref="IStorefrontCheckoutSessionStore"/>;
/// <see cref="GetSessionAsync"/> loads it back. <see cref="ProcessCheckoutAsync"/> folds
/// the order-processing pipeline output into <see cref="CheckoutPipelineEvent"/>
/// values for the storefront UI to stream.
/// <para>
/// When <c>AddPolarOrderProcessingPipeline()</c> has not been called, the service yields
/// a single <see cref="CheckoutFailed"/> event with the reason key
/// <c>Checkout.PipelineNotRegistered</c> so the host UI can render a coherent
/// "checkout unavailable" surface rather than crashing.
/// </para>
/// </remarks>
public sealed class DefaultStorefrontCheckoutService : IStorefrontCheckoutService
{
    private readonly IStorefrontCartStore _cartStore;
    private readonly IStorefrontCheckoutSessionStore _sessionStore;
    private readonly IStorefrontIdentityProvider _identity;
    private readonly IGuestSessionAccessor _guestSessions;
    private readonly IStorefrontIdempotencyCache _idempotency;
    private readonly StorefrontOptions _options;
    private readonly OrderProcessingPipeline? _pipeline;
    private readonly TimeProvider _clock;

    /// <summary>Initialises the service.</summary>
    /// <param name="cartStore">Cart persistence (used to retrieve the cart being checked out).</param>
    /// <param name="sessionStore">Session persistence.</param>
    /// <param name="identity">Resolves the current customer + tenant.</param>
    /// <param name="guestSessions">Resolves the current guest session.</param>
    /// <param name="idempotency">Idempotency cache used to short-circuit retried checkout initiations.</param>
    /// <param name="options">Storefront tunables (idempotency TTL).</param>
    /// <param name="pipeline">
    /// The order-processing pipeline, or <see langword="null"/> when
    /// <c>AddPolarOrderProcessingPipeline()</c> was not called.
    /// </param>
    /// <param name="clock">Clock used for timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any required argument is <see langword="null"/>.
    /// </exception>
    public DefaultStorefrontCheckoutService(
        IStorefrontCartStore cartStore,
        IStorefrontCheckoutSessionStore sessionStore,
        IStorefrontIdentityProvider identity,
        IGuestSessionAccessor guestSessions,
        IStorefrontIdempotencyCache idempotency,
        IOptions<StorefrontOptions> options,
        OrderProcessingPipeline? pipeline = null,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(cartStore);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(guestSessions);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(options);

        _cartStore = cartStore;
        _sessionStore = sessionStore;
        _identity = identity;
        _guestSessions = guestSessions;
        _idempotency = idempotency;
        _options = options.Value;
        _pipeline = pipeline;
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async Task<StorefrontResult<CheckoutSession>> InitiateCheckoutAsync(
        InitiateCheckoutCommand cmd,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var (owner, tenantId, error) = ResolveOwner();
        if (error is not null)
        {
            return StorefrontResult<CheckoutSession>.Failure(error);
        }

        var idemKey = BuildIdempotencyKey(owner!.Value, cmd.IdempotencyToken);
        if (idemKey is not null)
        {
            var cached = await _idempotency
                .TryGetAsync<StorefrontResult<CheckoutSession>>(idemKey, ct)
                .ConfigureAwait(false);
            if (cached.HasValue)
            {
                return cached.GetValueOrDefault(default);
            }
        }

        var cartOpt = await _cartStore.FindByOwnerAsync(owner.Value, tenantId, ct).ConfigureAwait(false);
        if (!cartOpt.HasValue)
        {
            return StorefrontResult<CheckoutSession>.Failure(new StorefrontNotFoundError(
                Message: "No cart found for the current owner — add at least one item before initiating checkout.",
                CorrelationId: Guid.NewGuid().ToString("N")));
        }

        var cart = cartOpt.GetValueOrDefault(default!);
        if (cart.LineItems.Count == 0)
        {
            return StorefrontResult<CheckoutSession>.Failure(new StorefrontValidationError(
                Message: "Cannot initiate checkout on an empty cart.",
                CorrelationId: Guid.NewGuid().ToString("N"),
                Fields: new[] { new StorefrontFieldError("Cart.LineItems", "Cart.Empty") }));
        }

        if (!_identity.IsAuthenticated && string.IsNullOrWhiteSpace(cmd.CustomerEmail))
        {
            return StorefrontResult<CheckoutSession>.Failure(new StorefrontValidationError(
                Message: "Guest checkout requires CustomerEmail.",
                CorrelationId: Guid.NewGuid().ToString("N"),
                Fields: new[] { new StorefrontFieldError(nameof(cmd.CustomerEmail), "Checkout.EmailRequired") }));
        }

        var session = new CheckoutSession
        {
            Id = Guid.NewGuid(),
            CartId = cart.Id,
            CustomerId = cart.CustomerId,
            TenantId = cart.TenantId,
            Status = CheckoutStatus.Initiated,
            CreatedAt = _clock.GetUtcNow(),
        };
        await _sessionStore.SaveAsync(session, ct).ConfigureAwait(false);
        var result = StorefrontResult<CheckoutSession>.Success(session);
        if (idemKey is not null)
        {
            await _idempotency
                .SetAsync(idemKey, result, _options.IdempotencyCacheTtl, ct)
                .ConfigureAwait(false);
        }
        return result;
    }

    /// <inheritdoc/>
    public async Task<StorefrontResult<CheckoutSession>> GetSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var found = await _sessionStore.FindByIdAsync(sessionId, ct).ConfigureAwait(false);
        return found.HasValue
            ? StorefrontResult<CheckoutSession>.Success(found.GetValueOrDefault(default!))
            : StorefrontResult<CheckoutSession>.Failure(new StorefrontNotFoundError(
                Message: $"Checkout session {sessionId:N} not found.",
                CorrelationId: Guid.NewGuid().ToString("N")));
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<CheckoutPipelineEvent> ProcessCheckoutAsync(
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (_pipeline is null)
        {
            yield return new CheckoutFailed
            {
                SessionId = sessionId,
                At = _clock.GetUtcNow(),
                Stage = CheckoutStatus.Initiated,
                Error = new StorefrontProviderError(
                    Message: "The order-processing pipeline has not been registered. Call AddPolarOrderProcessingPipeline() during DI setup.",
                    CorrelationId: sessionId.ToString("N"),
                    Provider: "storefront:checkout-pipeline"),
            };
            yield break;
        }

        var sessionResult = await _sessionStore.FindByIdAsync(sessionId, ct).ConfigureAwait(false);
        if (!sessionResult.HasValue)
        {
            yield return new CheckoutFailed
            {
                SessionId = sessionId,
                At = _clock.GetUtcNow(),
                Stage = CheckoutStatus.Initiated,
                Error = new StorefrontNotFoundError(
                    Message: $"Checkout session {sessionId:N} not found.",
                    CorrelationId: sessionId.ToString("N")),
            };
            yield break;
        }
        var session = sessionResult.GetValueOrDefault(default!);

        var cartOpt = await _cartStore.FindByIdAsync(session.CartId, ct).ConfigureAwait(false);
        if (!cartOpt.HasValue)
        {
            yield return new CheckoutFailed
            {
                SessionId = sessionId,
                At = _clock.GetUtcNow(),
                Stage = CheckoutStatus.Initiated,
                Error = new StorefrontNotFoundError(
                    Message: $"Cart {session.CartId:N} no longer exists.",
                    CorrelationId: sessionId.ToString("N")),
            };
            yield break;
        }
        var cart = cartOpt.GetValueOrDefault(default!);

        var startedAt = _clock.GetUtcNow();
        yield return new CheckoutStageStarted
        {
            SessionId = sessionId,
            At = startedAt,
            Stage = CheckoutStatus.Initiated,
        };

        var seed = new OrderInProcess
        {
            OrderId = sessionId,
            Cart = cart,
            CustomerId = session.CustomerId.HasValue
                ? StorefrontOption<Guid>.Some(session.CustomerId.Value)
                : StorefrontOption<Guid>.None,
            TenantId = session.TenantId.HasValue
                ? StorefrontOption<Guid>.Some(session.TenantId.Value)
                : StorefrontOption<Guid>.None,
            Currency = cart.Totals.Currency,
            Status = CheckoutStatus.Initiated,
        };

        var context = new PipelineStageContext
        {
            TenantId = seed.TenantId,
            CustomerId = seed.CustomerId,
            CorrelationId = sessionId,
            StartedAt = startedAt,
        };

        CheckoutStatus latestStatus = CheckoutStatus.Initiated;
        await foreach (var state in _pipeline.RunAsync(seed, context, ct).WithCancellation(ct).ConfigureAwait(false))
        {
            latestStatus = state.Status;

            if (state.Outcome == PipelineOutcome.Failed)
            {
                var failedSession = session with
                {
                    Status = CheckoutStatus.Failed,
                    CompletedAt = _clock.GetUtcNow(),
                };
                await _sessionStore.SaveAsync(failedSession, ct).ConfigureAwait(false);

                yield return new CheckoutFailed
                {
                    SessionId = sessionId,
                    At = _clock.GetUtcNow(),
                    Stage = state.Status,
                    Error = new StorefrontProviderError(
                        Message: state.FailureReasonKey.GetValueOrDefault("Checkout.PipelineStageFailed"),
                        CorrelationId: sessionId.ToString("N"),
                        Provider: "storefront:checkout-pipeline"),
                };
                yield break;
            }

            if (state.Status == CheckoutStatus.Completed)
            {
                var orderId = state.OrderId.ToString("N");
                var completedSession = session with
                {
                    Status = CheckoutStatus.Completed,
                    CompletedAt = _clock.GetUtcNow(),
                    OrderId = orderId,
                };
                await _sessionStore.SaveAsync(completedSession, ct).ConfigureAwait(false);
                yield return new CheckoutSucceeded
                {
                    SessionId = sessionId,
                    At = _clock.GetUtcNow(),
                    OrderId = orderId,
                };
                yield break;
            }

            yield return new CheckoutStageCompleted
            {
                SessionId = sessionId,
                At = _clock.GetUtcNow(),
                Stage = state.Status,
            };
        }

        // If the pipeline halted before reaching a terminal status, mark the session
        // with whatever the last status was so subsequent GetSessionAsync calls reflect
        // the partial progress.
        var partialSession = session with
        {
            Status = latestStatus,
        };
        await _sessionStore.SaveAsync(partialSession, ct).ConfigureAwait(false);
    }

    private (CartOwner? owner, StorefrontOption<Guid> tenantId, StorefrontError? error) ResolveOwner()
    {
        var tenantId = _identity.CurrentTenantId;
        if (_identity.IsAuthenticated && _identity.CurrentCustomerId.HasValue)
        {
            return (CartOwner.FromCustomer(_identity.CurrentCustomerId.GetValueOrDefault(Guid.Empty)), tenantId, null);
        }
        if (_guestSessions.CurrentGuestSessionId.HasValue)
        {
            return (CartOwner.FromGuest(_guestSessions.CurrentGuestSessionId.GetValueOrDefault(Guid.Empty)), tenantId, null);
        }
        return (null, tenantId, new StorefrontAuthenticationError(
            Message: "No customer or guest session resolved for the current request.",
            CorrelationId: Guid.NewGuid().ToString("N")));
    }

    private static string? BuildIdempotencyKey(CartOwner owner, string? token) =>
        string.IsNullOrWhiteSpace(token)
            ? null
            : $"InitiateCheckout|{(int)owner.Kind}|{owner.Id:N}|{token}";
}
