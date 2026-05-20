using PolarSharp.PrepaidWallets.Abstractions;

namespace PolarSharp.PrepaidWallets.Abstractions.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_carries_value()
    {
        var r = Result<int, string>.Success(7);
        Assert.True(r.IsSuccess);
        Assert.False(r.IsFailure);
        Assert.Equal(7, r.Value);
    }

    [Fact]
    public void Failure_carries_error()
    {
        var r = Result<int, string>.Failure("nope");
        Assert.True(r.IsFailure);
        Assert.False(r.IsSuccess);
        Assert.Equal("nope", r.Error);
    }

    [Fact]
    public void Value_on_failure_throws()
    {
        var r = Result<int, string>.Failure("nope");
        Assert.Throws<InvalidOperationException>(() => _ = r.Value);
    }

    [Fact]
    public void Error_on_success_throws()
    {
        var r = Result<int, string>.Success(1);
        Assert.Throws<InvalidOperationException>(() => _ = r.Error);
    }

    [Fact]
    public void Match_routes_to_success_branch()
    {
        var matched = Result<int, string>.Success(2).Match(v => $"ok:{v}", e => $"err:{e}");
        Assert.Equal("ok:2", matched);
    }

    [Fact]
    public void Match_routes_to_failure_branch()
    {
        var matched = Result<int, string>.Failure("bad").Match(v => $"ok:{v}", e => $"err:{e}");
        Assert.Equal("err:bad", matched);
    }

    [Fact]
    public void Equality_is_structural()
    {
        Assert.Equal(Result<int, string>.Success(1), Result<int, string>.Success(1));
        Assert.NotEqual(Result<int, string>.Success(1), Result<int, string>.Success(2));
        Assert.NotEqual(Result<int, string>.Success(1), Result<int, string>.Failure("x"));
    }
}
