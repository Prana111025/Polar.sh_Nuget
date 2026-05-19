using PolarSharp.Reporting.Drilldown;
using PolarSharp.Reporting.Reports;

namespace PolarSharp.Reporting.GraphQL;

/// <summary>
/// GraphQL input shape mirroring <see cref="TransactionReportRequest"/>.
/// </summary>
/// <remarks>
/// Hot Chocolate's schema generator dislikes <see langword="required"/> properties on input
/// types because GraphQL input types must be parameterless-constructible during binding. This
/// POCO carries explicit defaults — Hot Chocolate uses <see cref="DefaultValueAttribute"/> to
/// mark fields as optional in the schema and supplies the default when the caller omits the
/// argument. The <see cref="ToRequest"/> converter maps onto the wire-format record so the
/// REST DTO stays the single source of truth.
/// </remarks>
public sealed class TransactionReportRequestInput
{
    /// <summary>Inclusive start of the reporting period (UTC).</summary>
    public DateTimeOffset PeriodStart { get; set; }
    /// <summary>Exclusive end of the reporting period (UTC).</summary>
    public DateTimeOffset PeriodEnd { get; set; }
    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; set; } = string.Empty;
    /// <summary>Time-series bucket granularity.</summary>
    [System.ComponentModel.DefaultValue(TimeBucketGranularity.Daily)]
    public TimeBucketGranularity Granularity { get; set; } = TimeBucketGranularity.Daily;
    /// <summary>How many entries to include in <see cref="TransactionReport.TopProducts"/>.</summary>
    [System.ComponentModel.DefaultValue(10)]
    public int TopProductCount { get; set; } = 10;

    /// <summary>Projects the input onto the wire-format request record.</summary>
    /// <returns>A <see cref="TransactionReportRequest"/> the underlying client consumes.</returns>
    public TransactionReportRequest ToRequest() => new()
    {
        PeriodStart = PeriodStart,
        PeriodEnd = PeriodEnd,
        Currency = Currency,
        Granularity = Granularity,
        TopProductCount = TopProductCount,
    };
}

/// <summary>GraphQL input mirroring <see cref="SubscriptionReportRequest"/>.</summary>
public sealed class SubscriptionReportRequestInput
{
    /// <summary>Inclusive start of the reporting period (UTC).</summary>
    public DateTimeOffset PeriodStart { get; set; }
    /// <summary>Exclusive end of the reporting period (UTC).</summary>
    public DateTimeOffset PeriodEnd { get; set; }
    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; set; } = string.Empty;
    /// <summary>How many months of cohort-retention data to include.</summary>
    [System.ComponentModel.DefaultValue(12)]
    public int CohortMonths { get; set; } = 12;

    /// <summary>Projects the input onto the wire-format request record.</summary>
    /// <returns>A <see cref="SubscriptionReportRequest"/> the underlying client consumes.</returns>
    public SubscriptionReportRequest ToRequest() => new()
    {
        PeriodStart = PeriodStart,
        PeriodEnd = PeriodEnd,
        Currency = Currency,
        CohortMonths = CohortMonths,
    };
}

/// <summary>GraphQL input mirroring <see cref="OrderReportRequest"/>.</summary>
public sealed class OrderReportRequestInput
{
    /// <summary>Inclusive start of the reporting period (UTC).</summary>
    public DateTimeOffset PeriodStart { get; set; }
    /// <summary>Exclusive end of the reporting period (UTC).</summary>
    public DateTimeOffset PeriodEnd { get; set; }
    /// <summary>Time-series bucket granularity.</summary>
    [System.ComponentModel.DefaultValue(TimeBucketGranularity.Daily)]
    public TimeBucketGranularity Granularity { get; set; } = TimeBucketGranularity.Daily;

    /// <summary>Projects the input onto the wire-format request record.</summary>
    /// <returns>An <see cref="OrderReportRequest"/> the underlying client consumes.</returns>
    public OrderReportRequest ToRequest() => new()
    {
        PeriodStart = PeriodStart,
        PeriodEnd = PeriodEnd,
        Granularity = Granularity,
    };
}

/// <summary>GraphQL input mirroring <see cref="CustomerReportRequest"/>.</summary>
public sealed class CustomerReportRequestInput
{
    /// <summary>Inclusive start of the reporting period (UTC).</summary>
    public DateTimeOffset PeriodStart { get; set; }
    /// <summary>Exclusive end of the reporting period (UTC).</summary>
    public DateTimeOffset PeriodEnd { get; set; }

    /// <summary>Projects the input onto the wire-format request record.</summary>
    /// <returns>A <see cref="CustomerReportRequest"/> the underlying client consumes.</returns>
    public CustomerReportRequest ToRequest() => new()
    {
        PeriodStart = PeriodStart,
        PeriodEnd = PeriodEnd,
    };
}

/// <summary>GraphQL input mirroring <see cref="ErrorAuditRequest"/>.</summary>
public sealed class ErrorAuditRequestInput
{
    /// <summary>Inclusive start of the reporting period (UTC).</summary>
    public DateTimeOffset PeriodStart { get; set; }
    /// <summary>Exclusive end of the reporting period (UTC).</summary>
    public DateTimeOffset PeriodEnd { get; set; }
    /// <summary>How many recent Polar events to include in the report.</summary>
    [System.ComponentModel.DefaultValue(50)]
    public int RecentEventCount { get; set; } = 50;

    /// <summary>Projects the input onto the wire-format request record.</summary>
    /// <returns>An <see cref="ErrorAuditRequest"/> the underlying client consumes.</returns>
    public ErrorAuditRequest ToRequest() => new()
    {
        PeriodStart = PeriodStart,
        PeriodEnd = PeriodEnd,
        RecentEventCount = RecentEventCount,
    };
}

/// <summary>GraphQL input mirroring <see cref="CustomerListRequest"/>.</summary>
public sealed class CustomerListRequestInput
{
    /// <summary>0-based page index.</summary>
    [System.ComponentModel.DefaultValue(0)]
    public int Page { get; set; }
    /// <summary>Rows per page (server caps at 500).</summary>
    [System.ComponentModel.DefaultValue(50)]
    public int PageSize { get; set; } = 50;
    /// <summary>Optional sort field. Per-query allow-list applies.</summary>
    public string? SortBy { get; set; }
    /// <summary>True for newest-first / largest-first sort.</summary>
    [System.ComponentModel.DefaultValue(true)]
    public bool SortDescending { get; set; } = true;
    /// <summary>Optional substring filter on email or name.</summary>
    public string? SearchTerm { get; set; }
    /// <summary>Lower bound on customer creation date.</summary>
    public DateTimeOffset? CreatedAfter { get; set; }
    /// <summary>Upper bound on customer creation date.</summary>
    public DateTimeOffset? CreatedBefore { get; set; }

    /// <summary>Projects the input onto the wire-format request record.</summary>
    /// <returns>A <see cref="CustomerListRequest"/> the underlying client consumes.</returns>
    public CustomerListRequest ToRequest() => new()
    {
        Page = Page,
        PageSize = PageSize,
        SortBy = SortBy,
        SortDescending = SortDescending,
        SearchTerm = SearchTerm,
        CreatedAfter = CreatedAfter,
        CreatedBefore = CreatedBefore,
    };
}

/// <summary>GraphQL input mirroring <see cref="OrderListRequest"/>.</summary>
public sealed class OrderListRequestInput
{
    /// <summary>0-based page index.</summary>
    [System.ComponentModel.DefaultValue(0)]
    public int Page { get; set; }
    /// <summary>Rows per page (server caps at 500).</summary>
    [System.ComponentModel.DefaultValue(50)]
    public int PageSize { get; set; } = 50;
    /// <summary>Optional sort field. Per-query allow-list applies.</summary>
    public string? SortBy { get; set; }
    /// <summary>True for newest-first / largest-first sort.</summary>
    [System.ComponentModel.DefaultValue(true)]
    public bool SortDescending { get; set; } = true;
    /// <summary>Lower bound on order creation date.</summary>
    public DateTimeOffset? CreatedAfter { get; set; }
    /// <summary>Upper bound on order creation date.</summary>
    public DateTimeOffset? CreatedBefore { get; set; }
    /// <summary>Optional Polar wire-format status filter.</summary>
    public string? Status { get; set; }

    /// <summary>Projects the input onto the wire-format request record.</summary>
    /// <returns>An <see cref="OrderListRequest"/> the underlying client consumes.</returns>
    public OrderListRequest ToRequest() => new()
    {
        Page = Page,
        PageSize = PageSize,
        SortBy = SortBy,
        SortDescending = SortDescending,
        CreatedAfter = CreatedAfter,
        CreatedBefore = CreatedBefore,
        Status = Status,
    };
}
