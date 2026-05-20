using PolarSharp.PrepaidWallets.Abstractions;

namespace PolarSharp.PrepaidWallets.Abstractions.Tests;

public sealed class FundingSourceTests
{
    [Fact]
    public void Polar_tags_kind_correctly()
    {
        var s = FundingSource.Polar("order_123");
        Assert.Equal("polar", s.Kind);
        Assert.True(s.ExternalReference.TryGetValue(out var r));
        Assert.Equal("order_123", r);
    }

    [Fact]
    public void Stripe_tags_kind_correctly()
    {
        var s = FundingSource.Stripe("ch_test");
        Assert.Equal("stripe", s.Kind);
        Assert.True(s.ExternalReference.TryGetValue(out var r));
        Assert.Equal("ch_test", r);
    }

    [Fact]
    public void PayPal_tags_kind_correctly()
    {
        var s = FundingSource.PayPal("op_test");
        Assert.Equal("paypal", s.Kind);
    }

    [Fact]
    public void Manual_tags_kind_correctly()
    {
        var s = FundingSource.Manual("operator ticket #7");
        Assert.Equal("manual", s.Kind);
        Assert.True(s.ExternalReference.TryGetValue(out var r));
        Assert.Equal("operator ticket #7", r);
    }

    [Fact]
    public void Records_are_structurally_equal()
    {
        Assert.Equal(FundingSource.Polar("x"), FundingSource.Polar("x"));
        Assert.NotEqual(FundingSource.Polar("x"), FundingSource.Stripe("x"));
    }
}
