using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Commands;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Abstractions.Queries;
using PolarSharp.PrepaidWallets.Abstractions.Stores;
using PolarSharp.PrepaidWallets.Stores;

namespace PolarSharp.PrepaidWallets.Tests;

/// <summary>
/// End-to-end tests through the MediatR pipeline using the default in-memory stores. The
/// invariants exercised: handler routing, idempotency short-circuit, validation error, command
/// success path, snapshot policy firing.
/// </summary>
public sealed class CommandHandlerTests : IAsyncDisposable
{
    private readonly IServiceProvider _sp;
    private readonly IServiceScope _scope;
    private readonly IMediator _mediator;

    public CommandHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPolarPrepaidWallets(snapshotStride: 2);
        _sp = services.BuildServiceProvider();
        _scope = _sp.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Open_then_fund_then_debit_round_trip_via_mediator()
    {
        var id = WalletId.NewId();
        var open = WalletFixture.OpenCommand(id);
        var openResult = await _mediator.Send(open);
        Assert.True(openResult.Outcome.IsSuccess);

        var fundResult = await _mediator.Send(WalletFixture.FundCommand(id, 1_000));
        Assert.True(fundResult.Outcome.IsSuccess);

        var debitResult = await _mediator.Send(WalletFixture.DebitCommand(id, 300));
        Assert.True(debitResult.Outcome.IsSuccess);

        var stateOpt = await _mediator.Send(new GetWalletStateQuery(id));
        Assert.True(stateOpt.TryGetValue(out var state));
        Assert.Equal(700, state.Balance.Value);
        Assert.Equal(3, state.Version);
    }

    [Fact]
    public async Task Replaying_the_same_command_returns_the_original_event()
    {
        var id = WalletId.NewId();
        await _mediator.Send(WalletFixture.OpenCommand(id));
        var fund = WalletFixture.FundCommand(id, 500);

        var first = await _mediator.Send(fund);
        var second = await _mediator.Send(fund);

        Assert.True(first.Outcome.IsSuccess);
        Assert.True(second.Outcome.IsSuccess);
        Assert.Equal(first.Outcome.Value.Event.SequenceNo, second.Outcome.Value.Event.SequenceNo);
        Assert.Equal(first.Outcome.Value.ResultingVersion, second.Outcome.Value.ResultingVersion);

        var balance = await _mediator.Send(new GetWalletBalanceQuery(id));
        Assert.Equal(500, balance.Value.Value);
    }

    [Fact]
    public async Task Validation_failure_surfaces_as_typed_error()
    {
        var bad = WalletFixture.OpenCommand() with { Currency = "DOLLAR" };
        var result = await _mediator.Send(bad);
        Assert.True(result.Outcome.IsFailure);
        Assert.IsType<CommandError.ValidationFailed>(result.Outcome.Error);
    }

    [Fact]
    public async Task Insufficient_funds_surfaces_as_typed_error()
    {
        var id = WalletId.NewId();
        await _mediator.Send(WalletFixture.OpenCommand(id));
        await _mediator.Send(WalletFixture.FundCommand(id, 50));
        var result = await _mediator.Send(WalletFixture.DebitCommand(id, 100));
        Assert.True(result.Outcome.IsFailure);
        var err = Assert.IsType<CommandError.InsufficientFunds>(result.Outcome.Error);
        Assert.Equal(50, err.Balance.Value);
    }

    [Fact]
    public async Task Snapshot_fires_at_stride_boundary()
    {
        var id = WalletId.NewId();
        await _mediator.Send(WalletFixture.OpenCommand(id));
        await _mediator.Send(WalletFixture.FundCommand(id, 100, "fund-1"));
        // After 2 events, the snapshot policy with stride 2 should have fired.
        var snapshotStore = _scope.ServiceProvider.GetRequiredService<IWalletSnapshotStore>();
        var loaded = await snapshotStore.LoadLatestAsync(id);
        Assert.True(loaded.HasValue);
        Assert.True(loaded.Value.Version >= 2);
    }

    [Fact]
    public async Task GetWalletHistory_returns_events_in_order()
    {
        var id = WalletId.NewId();
        await _mediator.Send(WalletFixture.OpenCommand(id));
        await _mediator.Send(WalletFixture.FundCommand(id, 100));
        await _mediator.Send(WalletFixture.DebitCommand(id, 50));

        var history = await _mediator.Send(new GetWalletHistoryQuery(id, FromSequenceNoInclusive: 1, MaxEvents: 50));
        Assert.Equal(3, history.Count);
        Assert.IsType<WalletOpened>(history[0]);
        Assert.IsType<WalletFunded>(history[1]);
        Assert.IsType<WalletDebited>(history[2]);
    }

    [Fact]
    public async Task Query_on_missing_wallet_returns_None()
    {
        var stateOpt = await _mediator.Send(new GetWalletStateQuery(WalletId.NewId()));
        Assert.False(stateOpt.HasValue);

        var balanceOpt = await _mediator.Send(new GetWalletBalanceQuery(WalletId.NewId()));
        Assert.False(balanceOpt.HasValue);
    }

    public ValueTask DisposeAsync()
    {
        _scope.Dispose();
        if (_sp is IAsyncDisposable a)
        {
            return a.DisposeAsync();
        }

        if (_sp is IDisposable d)
        {
            d.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
