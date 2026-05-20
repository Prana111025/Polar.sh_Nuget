using PolarSharp.PrepaidWallets.Abstractions;

namespace PolarSharp.PrepaidWallets.Abstractions.Tests;

public sealed class TokenAmountTests
{
    [Fact]
    public void Zero_is_zero()
    {
        Assert.Equal(0, TokenAmount.Zero.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    [InlineData(long.MaxValue)]
    public void Constructs_non_negative_values(long n) => Assert.Equal(n, new TokenAmount(n).Value);

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Rejects_negative(long n) => Assert.Throws<ArgumentOutOfRangeException>(() => new TokenAmount(n));

    [Fact]
    public void Addition_sums()
    {
        var sum = new TokenAmount(5) + new TokenAmount(10);
        Assert.Equal(15, sum.Value);
    }

    [Fact]
    public void Addition_overflow_throws()
    {
        Assert.Throws<OverflowException>(() => new TokenAmount(long.MaxValue) + new TokenAmount(1));
    }

    [Fact]
    public void Subtraction_subtracts()
    {
        var diff = new TokenAmount(10) - new TokenAmount(3);
        Assert.Equal(7, diff.Value);
    }

    [Fact]
    public void Subtraction_into_negative_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TokenAmount(3) - new TokenAmount(10));
    }

    [Fact]
    public void Comparison_operators_work()
    {
        Assert.True(new TokenAmount(3) < new TokenAmount(5));
        Assert.True(new TokenAmount(5) > new TokenAmount(3));
        Assert.True(new TokenAmount(5) >= new TokenAmount(5));
        Assert.True(new TokenAmount(5) <= new TokenAmount(5));
    }

    [Fact]
    public void Sort_via_CompareTo_works()
    {
        var values = new[] { new TokenAmount(3), new TokenAmount(1), new TokenAmount(2) };
        Array.Sort(values);
        Assert.Equal(new TokenAmount(1), values[0]);
        Assert.Equal(new TokenAmount(2), values[1]);
        Assert.Equal(new TokenAmount(3), values[2]);
    }
}
