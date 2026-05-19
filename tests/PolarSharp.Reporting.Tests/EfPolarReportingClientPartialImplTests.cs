using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore.Entities;
using PolarSharp.Reporting.Reports;
using PolarSharp.Reporting.Tests.Infrastructure;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Regression tests for the silent-zeros bug surfaced by the codebase stub audit:
/// pre-fix <see cref="EfPolarReportingClient.GetSubscriptionsAsync"/> and
/// <see cref="EfPolarReportingClient.GetErrorAuditAsync"/> returned hardcoded all-zero
/// report objects with no logged warning, no XML doc indication, and no honest computation
/// of fields the snapshot data already supported.
///
/// Post-fix: both methods compute every field that's directly derivable from snapshot
/// data, return zero for the genuinely deferred fields, AND log a warning so dashboards
/// can't misrepresent the data as legitimate. These tests lock both behaviours.
/// </summary>
public sealed class EfPolarReportingClientPartialImplTests
{
    private static readonly DateTimeOffset PeriodStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEnd = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    // ── GetSubscriptionsAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetSubscriptions_with_empty_snapshot_returns_all_zeros_AND_logs_warning()
    {
        await using var ctx = await ReportingTestContext.CreateAsync();
        var (client, log) = BuildClientWithRecordingLogger(ctx);

        var report = Unwrap(await client.GetSubscriptionsAsync(NewSubRequest()));

        Assert.Equal(0, report.ActiveSubscriptions);
        Assert.Equal(0, report.NewSubscriptions);
        Assert.Equal(0, report.CanceledSubscriptions);
        Assert.Equal(0m, report.ChurnRate);
        AssertDeferredFieldsAreZero(report);
        AssertWarningLogged(log, "Mrr/Arr/ExpansionRevenue");
    }

    [Fact]
    public async Task GetSubscriptions_counts_active_at_end_correctly_from_snapshot()
    {
        await using var ctx = await ReportingTestContext.CreateAsync();
        await SeedSubscriptionsAsync(ctx,
            // active, started before period: COUNTED in activeAtEnd
            ("sub_1", "active", PeriodStart.AddMonths(-1), null),
            // trialing, started inside period: COUNTED
            ("sub_2", "trialing", PeriodStart.AddDays(5), null),
            // active, canceled inside period: NOT counted at end
            ("sub_3", "active", PeriodStart.AddMonths(-2), PeriodStart.AddDays(10)),
            // active, canceled AFTER period end: COUNTED at end (still active during period)
            ("sub_4", "active", PeriodStart.AddMonths(-1), PeriodEnd.AddDays(5)),
            // status=past_due, not counted
            ("sub_5", "past_due", PeriodStart.AddMonths(-1), null));

        var (client, _) = BuildClientWithRecordingLogger(ctx);
        var report = Unwrap(await client.GetSubscriptionsAsync(NewSubRequest()));

        Assert.Equal(3, report.ActiveSubscriptions);  // sub_1, sub_2, sub_4
    }

    [Fact]
    public async Task GetSubscriptions_counts_new_and_canceled_within_period()
    {
        await using var ctx = await ReportingTestContext.CreateAsync();
        await SeedSubscriptionsAsync(ctx,
            ("sub_1", "active", PeriodStart.AddMonths(-1), null),                   // pre-period, neither new nor canceled
            ("sub_2", "active", PeriodStart.AddDays(2), null),                       // new in period
            ("sub_3", "active", PeriodStart.AddDays(15), null),                      // new in period
            ("sub_4", "canceled", PeriodStart.AddMonths(-2), PeriodStart.AddDays(7)),// canceled in period
            ("sub_5", "active", PeriodEnd.AddDays(3), null));                        // started AFTER period — neither

        var (client, _) = BuildClientWithRecordingLogger(ctx);
        var report = Unwrap(await client.GetSubscriptionsAsync(NewSubRequest()));

        Assert.Equal(2, report.NewSubscriptions);       // sub_2, sub_3
        Assert.Equal(1, report.CanceledSubscriptions);  // sub_4
    }

    [Fact]
    public async Task GetSubscriptions_computes_churn_rate_as_canceled_over_active_at_start()
    {
        await using var ctx = await ReportingTestContext.CreateAsync();
        // 4 active at start; 1 cancels in period → churn = 0.25
        await SeedSubscriptionsAsync(ctx,
            ("sub_a", "active", PeriodStart.AddMonths(-2), null),
            ("sub_b", "active", PeriodStart.AddMonths(-2), null),
            ("sub_c", "active", PeriodStart.AddMonths(-2), null),
            ("sub_d", "active", PeriodStart.AddMonths(-2), PeriodStart.AddDays(10)));

        var (client, _) = BuildClientWithRecordingLogger(ctx);
        var report = Unwrap(await client.GetSubscriptionsAsync(NewSubRequest()));

        Assert.Equal(0.25m, report.ChurnRate);
    }

    [Fact]
    public async Task GetSubscriptions_explicitly_keeps_MRR_ARR_ExpansionRevenue_at_zero()
    {
        // Lock the deferred-fields-stay-zero posture so a future implementer doesn't
        // partially wire MRR without also wiring the snapshot column it requires.
        await using var ctx = await ReportingTestContext.CreateAsync();
        await SeedSubscriptionsAsync(ctx,
            ("sub_paid_a", "active", PeriodStart.AddMonths(-1), null),
            ("sub_paid_b", "active", PeriodStart.AddMonths(-1), null));

        var (client, _) = BuildClientWithRecordingLogger(ctx);
        var report = Unwrap(await client.GetSubscriptionsAsync(NewSubRequest()));

        AssertDeferredFieldsAreZero(report);
    }

    // ── GetErrorAuditAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetErrorAudit_with_empty_snapshot_returns_zero_counters_empty_events_AND_logs_warning()
    {
        await using var ctx = await ReportingTestContext.CreateAsync();
        var (client, log) = BuildClientWithRecordingLogger(ctx);

        var report = Unwrap(await client.GetErrorAuditAsync(NewErrorAuditRequest()));

        AssertCounterFieldsAreZero(report);
        Assert.Empty(report.RecentPolarEvents);
        AssertWarningLogged(log, "counter fields");
    }

    [Fact]
    public async Task GetErrorAudit_populates_RecentPolarEvents_from_snapshot_ordered_desc_capped()
    {
        await using var ctx = await ReportingTestContext.CreateAsync();
        await SeedEventsAsync(ctx,
            ("evt_1", "order.created", PeriodStart.AddDays(1)),
            ("evt_2", "order.paid", PeriodStart.AddDays(5)),
            ("evt_3", "refund.created", PeriodStart.AddDays(10)),
            ("evt_4", "subscription.canceled", PeriodStart.AddDays(15)),
            // outside the period — must be excluded:
            ("evt_5", "order.created", PeriodStart.AddDays(-3)),
            ("evt_6", "order.created", PeriodEnd.AddDays(5)));

        var (client, _) = BuildClientWithRecordingLogger(ctx);
        var request = NewErrorAuditRequest() with { RecentEventCount = 2 };

        var report = Unwrap(await client.GetErrorAuditAsync(request));

        Assert.Equal(2, report.RecentPolarEvents.Count);  // capped by RecentEventCount
        Assert.Equal("evt_4", report.RecentPolarEvents[0].EventId);  // newest within period first
        Assert.Equal("evt_3", report.RecentPolarEvents[1].EventId);
        AssertCounterFieldsAreZero(report);  // partial-impl posture preserved
    }

    // ── Helpers ────────────────────────────────────────────────────────

    /// <summary>Test-only convenience: extract the success value or fail the test with the error message.</summary>
    private static T Unwrap<T>(Result<T, PolarError> result) =>
        result.Match<T>(
            onSuccess: v => v,
            onFailure: e => throw new Xunit.Sdk.XunitException($"Expected Success, got Failure: {e}"));

    private static (EfPolarReportingClient Client, RecordingLogger<EfPolarReportingClient> Log) BuildClientWithRecordingLogger(ReportingTestContext ctx)
    {
        var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var log = new RecordingLogger<EfPolarReportingClient>();
        return (new EfPolarReportingClient(db, log), log);
    }

    private static SubscriptionReportRequest NewSubRequest() => new()
    {
        PeriodStart = PeriodStart,
        PeriodEnd = PeriodEnd,
        Currency = "USD",
    };

    private static ErrorAuditRequest NewErrorAuditRequest() => new()
    {
        PeriodStart = PeriodStart,
        PeriodEnd = PeriodEnd,
        RecentEventCount = 50,
    };

    private static async Task SeedSubscriptionsAsync(
        ReportingTestContext ctx,
        params (string PolarId, string Status, DateTimeOffset StartedAt, DateTimeOffset? CanceledAt)[] subscriptions)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        foreach (var (polarId, status, startedAt, canceledAt) in subscriptions)
        {
            db.Subscriptions.Add(new ReportSubscriptionEntity
            {
                Id = Guid.NewGuid(),
                TenantId = ctx.CurrentTenantId,
                PolarSubscriptionId = polarId,
                CustomerId = "cust_test",
                ProductId = "prod_test",
                Status = status,
                StartedAt = startedAt,
                CanceledAt = canceledAt,
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedEventsAsync(
        ReportingTestContext ctx,
        params (string PolarId, string Type, DateTimeOffset OccurredAt)[] events)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        foreach (var (polarId, type, occurredAt) in events)
        {
            db.Events.Add(new ReportEventEntity
            {
                Id = Guid.NewGuid(),
                TenantId = ctx.CurrentTenantId,
                PolarEventId = polarId,
                Type = type,
                OccurredAt = occurredAt,
            });
        }
        await db.SaveChangesAsync();
    }

    private static void AssertDeferredFieldsAreZero(SubscriptionReport report)
    {
        Assert.Equal(0L, report.Mrr);
        Assert.Equal(0L, report.Arr);
        Assert.Equal(0L, report.ExpansionRevenue);
        Assert.Empty(report.Cohorts);
    }

    private static void AssertCounterFieldsAreZero(ErrorAuditReport report)
    {
        Assert.Equal(0, report.WebhookDeliveryFailures);
        Assert.Equal(0, report.SignatureVerificationFailures);
        Assert.Equal(0, report.CircuitBreakerOpenEvents);
        Assert.Equal(0, report.RateLimitHits);
        Assert.Equal(0, report.ApiErrorsByStatus);
    }

    private static void AssertWarningLogged(RecordingLogger<EfPolarReportingClient> log, string mustContain)
    {
        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains(mustContain, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
