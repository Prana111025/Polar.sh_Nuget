using PolarSharp.PrepaidWallets.Abstractions;

namespace PolarSharp.PrepaidWallets.Abstractions.Tests;

public sealed class IdempotencyKeyTests
{
    [Fact]
    public void Create_returns_key_with_value()
    {
        var k = IdempotencyKey.Create("abc-123");
        Assert.Equal("abc-123", k.Value);
        Assert.Equal("abc-123", k.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_or_whitespace(string input)
    {
        Assert.Throws<ArgumentException>(() => IdempotencyKey.Create(input));
    }

    [Fact]
    public void Create_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => IdempotencyKey.Create(null!));
    }

    [Fact]
    public void Create_rejects_oversize_keys()
    {
        var oversize = new string('x', IdempotencyKey.MaxLength + 1);
        Assert.Throws<ArgumentException>(() => IdempotencyKey.Create(oversize));
    }

    [Fact]
    public void Create_accepts_exactly_max_length()
    {
        var exact = new string('x', IdempotencyKey.MaxLength);
        var k = IdempotencyKey.Create(exact);
        Assert.Equal(exact, k.Value);
    }

    [Fact]
    public void Two_keys_with_same_string_are_equal()
    {
        var a = IdempotencyKey.Create("same");
        var b = IdempotencyKey.Create("same");
        Assert.Equal(a, b);
    }
}
