using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;
using PolarSharp.Reporting.Snapshot;
using PolarSharp.Reporting.Tests.Infrastructure;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// V20-005 acceptance: snapshot a tenant with seeded sandbox data, re-run, assert
/// the second run is a no-op (zero new rows, same counts on every snapshot table).
/// Gated on <c>POLAR_SANDBOX_TOKEN</c>; no-op when absent.
/// </summary>
/// <remarks>
/// Goes user-code → ReportSnapshotService → live PolarClientReportingApi → live
/// <c>https://sandbox-api.polar.sh</c>. The first call ingests whatever the sandbox
/// holds for the org pinned by the token; the second call should advance the per-resource
/// checkpoints to the same point and ingest nothing new.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class SnapshotIdempotencyIntegrationTests
{
    private static string? Token => Environment.GetEnvironmentVariable("POLAR_SANDBOX_TOKEN");

    [SkippableFact]
    public async Task RunSnapshot_against_live_sandbox_is_idempotent_on_second_invocation()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        await using var ctx = await ReportingTestContext.CreateAsync(configureServices: ConfigureLivePolar);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IReportSnapshotService>();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();

        // First snapshot: ingest whatever's in the sandbox for the token's org.
        var first = await svc.RunSnapshotAsync(ReportingTestContext.DefaultTenantId);
        var snapshot1 = await CountAllAsync(db);

        // Second snapshot: per-resource checkpoints are now at the latest seen ids; the
        // wrapper's "items past sinceId" cursor logic should yield nothing new.
        var second = await svc.RunSnapshotAsync(ReportingTestContext.DefaultTenantId);
        var snapshot2 = await CountAllAsync(db);

        // Per-resource ingestion counters from the second SnapshotReport must all be zero.
        Assert.Equal(0, second.EventsIngested);
        Assert.Equal(0, second.OrdersIngested);
        Assert.Equal(0, second.SubscriptionsIngested);
        Assert.Equal(0, second.CustomersIngested);
        Assert.Equal(0, second.BenefitGrantsIngested);
        Assert.Equal(0, second.ProductsIngested);
        Assert.Equal(0, second.CustomerMetersIngested);
        Assert.Equal(0, second.LicenseKeysIngested);
        Assert.Equal(0, second.BenefitsIngested);
        Assert.Equal(0, second.MetersIngested);
        Assert.Equal(0, second.CheckoutLinksIngested);
        Assert.Equal(0, second.DiscountsIngested);

        // And the persisted DB row counts must be unchanged across the rerun.
        Assert.Equal(snapshot1.events, snapshot2.events);
        Assert.Equal(snapshot1.orders, snapshot2.orders);
        Assert.Equal(snapshot1.subscriptions, snapshot2.subscriptions);
        Assert.Equal(snapshot1.customers, snapshot2.customers);
        Assert.Equal(snapshot1.grants, snapshot2.grants);
        Assert.Equal(snapshot1.benefits, snapshot2.benefits);
        Assert.Equal(snapshot1.discounts, snapshot2.discounts);
        Assert.Equal(snapshot1.checkoutLinks, snapshot2.checkoutLinks);
        Assert.Equal(snapshot1.products, snapshot2.products);
        Assert.Equal(snapshot1.licenseKeys, snapshot2.licenseKeys);
        Assert.Equal(snapshot1.meters, snapshot2.meters);
        Assert.Equal(snapshot1.customerMeters, snapshot2.customerMeters);
    }

    private static void ConfigureLivePolar(IServiceCollection services)
    {
        var http = new HttpClient { BaseAddress = new Uri("https://sandbox-api.polar.sh") };
        http.DefaultRequestHeaders.Authorization = new("Bearer", Token);
        var polar = new PolarClient(http);
        services.AddSingleton(polar);
        services.AddScoped<IPolarReportingApi>(sp => new PolarClientReportingApi(
            sp.GetRequiredService<PolarClient>(),
            NullLogger<PolarClientReportingApi>.Instance));
        services.AddScoped<IReportSnapshotService, ReportSnapshotService>();
    }

    private static async Task<(int events, int orders, int subscriptions, int customers, int grants,
        int benefits, int discounts, int checkoutLinks, int products, int licenseKeys, int meters, int customerMeters)>
        CountAllAsync(PolarReportingDbContext db) =>
        (
            await db.Events.AsNoTracking().CountAsync(),
            await db.Orders.AsNoTracking().CountAsync(),
            await db.Subscriptions.AsNoTracking().CountAsync(),
            await db.Customers.AsNoTracking().CountAsync(),
            await db.BenefitGrants.AsNoTracking().CountAsync(),
            await db.Benefits.AsNoTracking().CountAsync(),
            await db.Discounts.AsNoTracking().CountAsync(),
            await db.CheckoutLinks.AsNoTracking().CountAsync(),
            await db.Products.AsNoTracking().CountAsync(),
            await db.LicenseKeys.AsNoTracking().CountAsync(),
            await db.Meters.AsNoTracking().CountAsync(),
            await db.CustomerMeters.AsNoTracking().CountAsync()
        );
}
