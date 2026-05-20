using PolarSharp.PrepaidWallets.Abstractions;

namespace PolarSharp.PrepaidWallets.Abstractions.Tests;

public sealed class OptionTests
{
    [Fact]
    public void None_has_no_value()
    {
        Assert.False(Option<int>.None.HasValue);
        Assert.False(Option<int>.None.TryGetValue(out _));
    }

    [Fact]
    public void Some_exposes_value()
    {
        var opt = Option<int>.Some(42);
        Assert.True(opt.HasValue);
        Assert.True(opt.TryGetValue(out var v));
        Assert.Equal(42, v);
        Assert.Equal(42, opt.Value);
    }

    [Fact]
    public void Value_throws_on_none()
    {
        Assert.Throws<InvalidOperationException>(() => _ = Option<int>.None.Value);
    }

    [Fact]
    public void Some_rejects_null_reference()
    {
        Assert.Throws<ArgumentNullException>(() => Option<string>.Some(null!));
    }

    [Fact]
    public void GetValueOrDefault_returns_fallback_on_none()
    {
        Assert.Equal("fallback", Option<string>.None.GetValueOrDefault("fallback"));
    }

    [Fact]
    public void GetValueOrDefault_returns_value_on_some()
    {
        Assert.Equal("value", Option<string>.Some("value").GetValueOrDefault("fallback"));
    }

    [Fact]
    public void Equality_is_structural()
    {
        Assert.Equal(Option<int>.Some(7), Option<int>.Some(7));
        Assert.NotEqual(Option<int>.Some(7), Option<int>.Some(8));
        Assert.NotEqual(Option<int>.Some(7), Option<int>.None);
        Assert.Equal(Option<int>.None, Option<int>.None);
        Assert.True(Option<int>.Some(7) == Option<int>.Some(7));
        Assert.True(Option<int>.Some(7) != Option<int>.None);
    }

    [Fact]
    public void GetHashCode_matches_equals()
    {
        Assert.Equal(Option<int>.Some(5).GetHashCode(), Option<int>.Some(5).GetHashCode());
        Assert.Equal(Option<int>.None.GetHashCode(), Option<int>.None.GetHashCode());
    }
}
