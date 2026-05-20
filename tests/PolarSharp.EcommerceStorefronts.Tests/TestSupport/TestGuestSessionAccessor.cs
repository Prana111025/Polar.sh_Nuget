using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Identity;

namespace PolarSharp.EcommerceStorefronts.Tests.TestSupport;

internal sealed class TestGuestSessionAccessor : IGuestSessionAccessor
{
    public StorefrontOption<Guid> CurrentGuestSessionId { get; init; } = StorefrontOption<Guid>.None;

    public static TestGuestSessionAccessor WithSession(Guid id) =>
        new() { CurrentGuestSessionId = StorefrontOption<Guid>.Some(id) };

    public static TestGuestSessionAccessor None => new();
}
