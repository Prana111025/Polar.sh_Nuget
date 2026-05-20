using System.Collections.Concurrent;
using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;

namespace PolarSharp.EcommerceStorefronts.Checkout;

/// <summary>
/// In-process <see cref="IStorefrontCheckoutSessionStore"/>. The default registration;
/// suitable for development and small single-process deployments.
/// </summary>
/// <remarks>
/// Production multi-process hosts replace this with a Redis / EF Core implementation
/// so sessions survive a payment redirect that bounces the customer to a different
/// app server on return.
/// </remarks>
public sealed class InMemoryStorefrontCheckoutSessionStore : IStorefrontCheckoutSessionStore
{
    private readonly ConcurrentDictionary<Guid, CheckoutSession> _sessions = new();

    /// <inheritdoc/>
    public Task SaveAsync(CheckoutSession session, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ct.ThrowIfCancellationRequested();
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<StorefrontOption<CheckoutSession>> FindByIdAsync(Guid sessionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessions.TryGetValue(sessionId, out var s)
            ? StorefrontOption<CheckoutSession>.Some(s)
            : StorefrontOption<CheckoutSession>.None);
    }
}
