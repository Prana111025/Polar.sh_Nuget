using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.Drilldown;
using PolarSharp.Reporting.Reports;

namespace PolarSharp.Reporting.EntityFrameworkCore;

/// <summary>
/// EF-backed <see cref="IPolarReportingClient"/> — reads from the local snapshot tables.
/// </summary>
/// <remarks>
/// <para>
/// Aggregate methods (<see cref="GetTransactionsAsync"/> etc.) compose queries over the
/// snapshot tables and return the rolled-up shapes. Drilldown methods page directly off
/// indexed snapshot tables with pre-aggregated columns — top-level customer grid loads in
/// milliseconds even on tenants with 10k+ customers.
/// </para>
/// <para>
/// JSON variants serialise the report record via <see cref="JsonSerializer"/> — no double
/// hop through DTO mapping.
/// </para>
/// </remarks>
public sealed class EfPolarReportingClient : IPolarReportingClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const int MaxPageSize = 500;

    private readonly PolarReportingDbContext _db;
    private readonly ILogger<EfPolarReportingClient> _logger;

    /// <summary>Initializes the client.</summary>
    /// <param name="db">The reporting snapshot DbContext.</param>
    /// <param name="logger">
    /// Optional logger. When omitted (e.g. in tests that construct manually), falls back to
    /// <see cref="NullLogger{T}.Instance"/>. The DI container injects the registered logger
    /// automatically.
    /// </param>
    public EfPolarReportingClient(PolarReportingDbContext db, ILogger<EfPolarReportingClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
        _logger = logger ?? NullLogger<EfPolarReportingClient>.Instance;
    }

    // ── Aggregate / KPI ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<TransactionReport, PolarError>> GetTransactionsAsync(TransactionReportRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var range = _db.Orders.Where(o =>
            o.Currency == request.Currency &&
            o.CreatedAt >= request.PeriodStart && o.CreatedAt < request.PeriodEnd);

        var gross = await range.SumAsync(o => (long?)o.Amount, ct).ConfigureAwait(false) ?? 0L;
        var refunded = await range.SumAsync(o => (long?)o.RefundedAmount, ct).ConfigureAwait(false) ?? 0L;
        var orderCount = await range.CountAsync(ct).ConfigureAwait(false);
        var refundCount = await _db.OrderRefunds
            .Where(r => r.Currency == request.Currency && r.CreatedAt >= request.PeriodStart && r.CreatedAt < request.PeriodEnd)
            .CountAsync(ct).ConfigureAwait(false);

        var report = new TransactionReport
        {
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            Currency = request.Currency,
            GrossRevenue = gross,
            RefundedAmount = refunded,
            NetRevenue = gross - refunded,
            OrderCount = orderCount,
            RefundCount = refundCount,
            AverageOrderValue = orderCount == 0 ? 0 : gross / orderCount,
        };
        return Result<TransactionReport, PolarError>.Success(report);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <strong>Partial implementation.</strong> Counts that are directly derivable from the
    /// snapshot table (<see cref="PolarReportingDbContext.Subscriptions"/>) are computed
    /// honestly: <see cref="SubscriptionReport.ActiveSubscriptions"/>,
    /// <see cref="SubscriptionReport.NewSubscriptions"/>,
    /// <see cref="SubscriptionReport.CanceledSubscriptions"/>, and
    /// <see cref="SubscriptionReport.ChurnRate"/>.
    /// </para>
    /// <para>
    /// <strong>Returned as zero (and logged as a warning at runtime):</strong>
    /// <see cref="SubscriptionReport.Mrr"/>, <see cref="SubscriptionReport.Arr"/>, and
    /// <see cref="SubscriptionReport.ExpansionRevenue"/> require data the current snapshot
    /// schema does not carry (per-subscription monthly amount and change history). Extending
    /// <c>ReportSubscriptionEntity</c> with a <c>MonthlyAmount</c> column + recording it on
    /// every snapshot tick is the tracked-but-not-yet-scheduled path. The warning ensures
    /// callers know the fields are placeholders rather than legitimate zeros.
    /// </para>
    /// <para>
    /// <see cref="SubscriptionReport.Cohorts"/> is returned empty for the same "computable
    /// but non-trivial; defer until needed" reason — opt-in via a future patch when a host
    /// surfaces a concrete cohort dashboard requirement.
    /// </para>
    /// </remarks>
    public async Task<Result<SubscriptionReport, PolarError>> GetSubscriptionsAsync(SubscriptionReportRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subs = _db.Subscriptions.AsQueryable();

        var activeAtEnd = await subs.CountAsync(
            s => s.StartedAt < request.PeriodEnd
              && (s.CanceledAt == null || s.CanceledAt >= request.PeriodEnd)
              && (s.Status == "active" || s.Status == "trialing"),
            ct).ConfigureAwait(false);

        var newCount = await subs.CountAsync(
            s => s.StartedAt >= request.PeriodStart && s.StartedAt < request.PeriodEnd,
            ct).ConfigureAwait(false);

        var canceledCount = await subs.CountAsync(
            s => s.CanceledAt != null
              && s.CanceledAt >= request.PeriodStart
              && s.CanceledAt < request.PeriodEnd,
            ct).ConfigureAwait(false);

        var activeAtStart = await subs.CountAsync(
            s => s.StartedAt < request.PeriodStart
              && (s.CanceledAt == null || s.CanceledAt >= request.PeriodStart)
              && (s.Status == "active" || s.Status == "trialing"),
            ct).ConfigureAwait(false);

        var churnRate = activeAtStart == 0 ? 0m : Math.Round((decimal)canceledCount / activeAtStart, 4);

        _logger.LogWarning(
            "EfPolarReportingClient.GetSubscriptionsAsync returned zero for Mrr/Arr/ExpansionRevenue: " +
            "the snapshot schema (ReportSubscriptionEntity) does not currently carry per-subscription " +
            "monthly amount or change history. Active/new/canceled counts and churn rate ARE computed. " +
            "To populate the missing fields, extend ReportSubscriptionEntity with MonthlyAmount + record " +
            "it on every snapshot tick. Period: {PeriodStart:o} -> {PeriodEnd:o}, Currency: {Currency}.",
            request.PeriodStart, request.PeriodEnd, request.Currency);

        return Result<SubscriptionReport, PolarError>.Success(new SubscriptionReport
        {
            Mrr = 0,
            Arr = 0,
            ActiveSubscriptions = activeAtEnd,
            NewSubscriptions = newCount,
            CanceledSubscriptions = canceledCount,
            ChurnRate = churnRate,
            ExpansionRevenue = 0,
            Cohorts = [],
        });
    }

    /// <inheritdoc/>
    public async Task<Result<OrderReport, PolarError>> GetOrdersAsync(OrderReportRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var range = _db.Orders.Where(o => o.CreatedAt >= request.PeriodStart && o.CreatedAt < request.PeriodEnd);
        var total = await range.CountAsync(ct).ConfigureAwait(false);
        var fulfilled = await range.CountAsync(o => o.FulfilledAt != null, ct).ConfigureAwait(false);
        var failed = await range.CountAsync(o => o.Status == "void", ct).ConfigureAwait(false);

        return Result<OrderReport, PolarError>.Success(new OrderReport
        {
            Total = total,
            Fulfilled = fulfilled,
            Pending = total - fulfilled - failed,
            Failed = failed,
            MedianFulfillmentLatency = TimeSpan.Zero,
        });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <strong>Partial implementation.</strong>
    /// <see cref="ErrorAuditReport.RecentPolarEvents"/> is populated from the snapshot
    /// <see cref="PolarReportingDbContext.Events"/> table, ordered by
    /// <c>OccurredAt</c> descending, capped at <see cref="ErrorAuditRequest.RecentEventCount"/>.
    /// </para>
    /// <para>
    /// <strong>Returned as zero (and logged as a warning at runtime):</strong> the five
    /// counter fields (<see cref="ErrorAuditReport.WebhookDeliveryFailures"/>,
    /// <see cref="ErrorAuditReport.SignatureVerificationFailures"/>,
    /// <see cref="ErrorAuditReport.CircuitBreakerOpenEvents"/>,
    /// <see cref="ErrorAuditReport.RateLimitHits"/>,
    /// <see cref="ErrorAuditReport.ApiErrorsByStatus"/>) require a
    /// <c>System.Diagnostics.Metrics</c> listener that observes PolarSharp's runtime
    /// counters and aggregates them over the request period. The reporting client does not
    /// wire that listener today; the warning ensures callers know the counter fields are
    /// placeholders rather than legitimate zeros.
    /// </para>
    /// </remarks>
    public async Task<Result<ErrorAuditReport, PolarError>> GetErrorAuditAsync(ErrorAuditRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var recentEvents = await _db.Events
            .Where(e => e.OccurredAt >= request.PeriodStart && e.OccurredAt < request.PeriodEnd)
            .OrderByDescending(e => e.OccurredAt)
            .Take(Math.Max(request.RecentEventCount, 0))
            .Select(e => new PolarEventLogEntry(e.PolarEventId, e.Type, e.OccurredAt, ""))
            .ToListAsync(ct).ConfigureAwait(false);

        _logger.LogWarning(
            "EfPolarReportingClient.GetErrorAuditAsync returned zero for counter fields " +
            "(WebhookDeliveryFailures, SignatureVerificationFailures, CircuitBreakerOpenEvents, " +
            "RateLimitHits, ApiErrorsByStatus): these require a System.Diagnostics.Metrics listener " +
            "that aggregates PolarSharp's runtime counters over the request period — the reporting " +
            "client does not wire that listener today. RecentPolarEvents IS populated from the " +
            "snapshot Events table. Period: {PeriodStart:o} -> {PeriodEnd:o}, RecentEventCount: {Count}.",
            request.PeriodStart, request.PeriodEnd, request.RecentEventCount);

        return Result<ErrorAuditReport, PolarError>.Success(new ErrorAuditReport
        {
            WebhookDeliveryFailures = 0,
            SignatureVerificationFailures = 0,
            CircuitBreakerOpenEvents = 0,
            RateLimitHits = 0,
            ApiErrorsByStatus = 0,
            RecentPolarEvents = recentEvents,
        });
    }

    /// <inheritdoc/>
    public async Task<Result<CustomerReport, PolarError>> GetCustomersAsync(CustomerReportRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var total = await _db.Customers.CountAsync(ct).ConfigureAwait(false);
        var newCount = await _db.Customers
            .CountAsync(c => c.CreatedAt >= request.PeriodStart && c.CreatedAt < request.PeriodEnd, ct)
            .ConfigureAwait(false);
        var avgLtvDouble = await _db.Customers
            .Where(c => c.LifetimeValue > 0)
            .AverageAsync(c => (double?)c.LifetimeValue, ct).ConfigureAwait(false) ?? 0d;
        var avgLtv = (long)Math.Round(avgLtvDouble);

        return Result<CustomerReport, PolarError>.Success(new CustomerReport
        {
            TotalCustomers = total,
            NewCustomers = newCount,
            AverageLifetimeValue = avgLtv,
        });
    }

    /// <inheritdoc/>
    public async Task<Result<CustomerEntitlementsReport, PolarError>> GetCustomerEntitlementsAsync(string customerId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(customerId);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.PolarCustomerId == customerId, ct).ConfigureAwait(false);
        if (customer is null)
        {
            return Result<CustomerEntitlementsReport, PolarError>.Failure(new NotFoundError($"Customer '{customerId}' not in snapshot.", string.Empty));
        }

        var grants = await _db.BenefitGrants.Where(g => g.CustomerId == customerId).ToListAsync(ct).ConfigureAwait(false);
        var active = grants.Where(g => g.IsGranted)
            .Select(g => new ActiveEntitlement(g.BenefitId, g.BenefitName, g.BenefitKind, g.GrantedAt ?? DateTimeOffset.MinValue))
            .ToList();
        var revoked = grants.Where(g => !g.IsGranted && g.RevokedAt != null)
            .Select(g => new RevokedEntitlement(g.BenefitId, g.BenefitName, g.RevokedAt!.Value, "revoked"))
            .ToList();

        return Result<CustomerEntitlementsReport, PolarError>.Success(new CustomerEntitlementsReport
        {
            CustomerId = customer.PolarCustomerId,
            CustomerEmail = customer.Email,
            ActiveBenefits = active,
            RevokedBenefits = revoked,
        });
    }

    // ── JSON variants ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<string, PolarError>> GetTransactionsAsJsonAsync(TransactionReportRequest request, CancellationToken ct = default)
    {
        var result = await GetTransactionsAsync(request, ct).ConfigureAwait(false);
        return ToJson(result);
    }

    /// <inheritdoc/>
    public async Task<Result<string, PolarError>> GetSubscriptionsAsJsonAsync(SubscriptionReportRequest request, CancellationToken ct = default)
    {
        var result = await GetSubscriptionsAsync(request, ct).ConfigureAwait(false);
        return ToJson(result);
    }

    /// <inheritdoc/>
    public async Task<Result<string, PolarError>> GetOrdersAsJsonAsync(OrderReportRequest request, CancellationToken ct = default)
    {
        var result = await GetOrdersAsync(request, ct).ConfigureAwait(false);
        return ToJson(result);
    }

    /// <inheritdoc/>
    public async Task<Result<string, PolarError>> GetErrorAuditAsJsonAsync(ErrorAuditRequest request, CancellationToken ct = default)
    {
        var result = await GetErrorAuditAsync(request, ct).ConfigureAwait(false);
        return ToJson(result);
    }

    /// <inheritdoc/>
    public async Task<Result<string, PolarError>> GetCustomersAsJsonAsync(CustomerReportRequest request, CancellationToken ct = default)
    {
        var result = await GetCustomersAsync(request, ct).ConfigureAwait(false);
        return ToJson(result);
    }

    // ── Drilldown ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<PagedResult<CustomerListRow>, PolarError>> ListCustomersAsync(CustomerListRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var q = _db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = $"%{request.SearchTerm}%";
            q = q.Where(c => EF.Functions.Like(c.Email, term) || (c.Name != null && EF.Functions.Like(c.Name, term)));
        }
        if (request.CreatedAfter is { } after) q = q.Where(c => c.CreatedAt >= after);
        if (request.CreatedBefore is { } before) q = q.Where(c => c.CreatedAt < before);

        q = (request.SortBy ?? "LastOrderAt") switch
        {
            "Email" => request.SortDescending ? q.OrderByDescending(c => c.Email) : q.OrderBy(c => c.Email),
            "LifetimeValue" => request.SortDescending ? q.OrderByDescending(c => c.LifetimeValue) : q.OrderBy(c => c.LifetimeValue),
            "OrderCount" => request.SortDescending ? q.OrderByDescending(c => c.OrderCount) : q.OrderBy(c => c.OrderCount),
            "CreatedAt" => request.SortDescending ? q.OrderByDescending(c => c.CreatedAt) : q.OrderBy(c => c.CreatedAt),
            _ => request.SortDescending ? q.OrderByDescending(c => c.LastOrderAt) : q.OrderBy(c => c.LastOrderAt),
        };

        var total = await q.CountAsync(ct).ConfigureAwait(false);
        var rows = await q.Skip(request.Page * pageSize).Take(pageSize)
            .Select(c => new CustomerListRow
            {
                CustomerId = c.PolarCustomerId,
                Email = c.Email,
                Name = c.Name,
                OrderCount = c.OrderCount,
                LifetimeValue = c.LifetimeValue,
                Currency = c.Currency,
                FirstOrderAt = c.FirstOrderAt,
                LastOrderAt = c.LastOrderAt,
                CreatedAt = c.CreatedAt,
            })
            .ToListAsync(ct).ConfigureAwait(false);

        return Result<PagedResult<CustomerListRow>, PolarError>.Success(new PagedResult<CustomerListRow>
        {
            Rows = rows,
            TotalCount = total,
            Page = request.Page,
            PageSize = pageSize,
        });
    }

    /// <inheritdoc/>
    public async Task<Result<PagedResult<OrderSummaryRow>, PolarError>> ListOrdersForCustomerAsync(string customerId, OrderListRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(customerId);
        ArgumentNullException.ThrowIfNull(request);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var q = _db.Orders.Where(o => o.CustomerId == customerId);
        if (request.CreatedAfter is { } after) q = q.Where(o => o.CreatedAt >= after);
        if (request.CreatedBefore is { } before) q = q.Where(o => o.CreatedAt < before);
        if (!string.IsNullOrEmpty(request.Status)) q = q.Where(o => o.Status == request.Status);

        q = request.SortDescending ? q.OrderByDescending(o => o.CreatedAt) : q.OrderBy(o => o.CreatedAt);

        var total = await q.CountAsync(ct).ConfigureAwait(false);
        var rows = await q.Skip(request.Page * pageSize).Take(pageSize)
            .Select(o => new OrderSummaryRow
            {
                OrderId = o.PolarOrderId,
                OrderNumber = o.OrderNumber,
                Status = o.Status,
                Amount = o.Amount,
                TaxAmount = o.TaxAmount,
                RefundedAmount = o.RefundedAmount,
                Currency = o.Currency,
                LineItemCount = o.LineItemCount,
                InvoiceUrl = o.InvoiceUrl,
                CreatedAt = o.CreatedAt,
                FulfilledAt = o.FulfilledAt,
            })
            .ToListAsync(ct).ConfigureAwait(false);

        return Result<PagedResult<OrderSummaryRow>, PolarError>.Success(new PagedResult<OrderSummaryRow>
        {
            Rows = rows,
            TotalCount = total,
            Page = request.Page,
            PageSize = pageSize,
        });
    }

    /// <inheritdoc/>
    public async Task<Result<OrderDrilldownDetail, PolarError>> GetOrderDrilldownAsync(string orderId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(orderId);

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.PolarOrderId == orderId, ct).ConfigureAwait(false);
        if (order is null)
        {
            return Result<OrderDrilldownDetail, PolarError>.Failure(new NotFoundError($"Order '{orderId}' not in snapshot.", string.Empty));
        }

        var customerEmail = await _db.Customers
            .Where(c => c.PolarCustomerId == order.CustomerId)
            .Select(c => c.Email).FirstOrDefaultAsync(ct).ConfigureAwait(false) ?? "";

        var items = await _db.OrderLineItems.Where(li => li.OrderId == order.Id)
            .Select(li => new OrderLineItemRow(li.ProductId, li.ProductName, li.PriceId, li.Quantity, li.UnitAmount, li.LineTotal, li.DiscountAmount, li.TaxAmount))
            .ToListAsync(ct).ConfigureAwait(false);

        var refunds = await _db.OrderRefunds.Where(r => r.OrderId == order.Id)
            .Select(r => new OrderRefundRow(r.PolarRefundId, r.Amount, r.Currency, r.Reason, r.CreatedAt))
            .ToListAsync(ct).ConfigureAwait(false);

        var grants = await _db.BenefitGrants.Where(g => g.OrderId == order.Id)
            .Select(g => new BenefitGrantRow(g.BenefitId, g.BenefitName, g.BenefitKind, g.IsGranted, g.GrantedAt, g.RevokedAt))
            .ToListAsync(ct).ConfigureAwait(false);

        return Result<OrderDrilldownDetail, PolarError>.Success(new OrderDrilldownDetail
        {
            OrderId = order.PolarOrderId,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            CustomerEmail = customerEmail,
            Status = order.Status,
            Amount = order.Amount,
            TaxAmount = order.TaxAmount,
            Currency = order.Currency,
            LineItems = items,
            Refunds = refunds,
            BenefitGrants = grants,
            InvoiceUrl = order.InvoiceUrl,
            CreatedAt = order.CreatedAt,
            FulfilledAt = order.FulfilledAt,
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static Result<string, PolarError> ToJson<T>(Result<T, PolarError> result) =>
        result.Match(
            onSuccess: v => Result<string, PolarError>.Success(JsonSerializer.Serialize(v, Json)),
            onFailure: e => Result<string, PolarError>.Failure(e));
}

