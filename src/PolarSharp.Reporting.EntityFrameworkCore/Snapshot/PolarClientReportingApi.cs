using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;

namespace PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

/// <summary>
/// Default <see cref="IPolarReportingApi"/> implementation backed by the Kiota
/// <see cref="PolarClient"/>. V20-005 Phase 1 wires the 7 new resources
/// (benefits, discounts, checkout-links, products, license-keys, meters, customer-meters)
/// to their live Polar HTTP endpoints; V20-005 Phase 1.5 wires the original 5
/// (events, orders, subscriptions, customers, benefit-grants).
/// </summary>
/// <remarks>
/// Every fetcher follows the same shape: page=1 + Limit=pageSize against the live
/// resource endpoint, reverse to ascending order (Polar returns DESC by created_at),
/// skip past <c>sinceId</c> if found, map subtypes via discriminator probing where
/// applicable, surface failures as typed <see cref="PolarReportingApiError"/>.
/// </remarks>
internal sealed class PolarClientReportingApi(PolarClient polar, ILogger<PolarClientReportingApi> logger) : IPolarReportingApi
{
    private readonly PolarClient _polar = polar ?? throw new ArgumentNullException(nameof(polar));
    private readonly ILogger<PolarClientReportingApi> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // ── V20-005 Phase 1.5: original 5 resources (live) ───────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1.5: live wiring against <c>GET /v1/events/</c>. Polar's
    /// <c>Event</c> is a 2-way discriminated union (<c>SystemEvent</c> + <c>UserEvent</c>);
    /// both subtypes share the same shape (Id, Name, Source, Timestamp, OrganizationId).
    /// We probe for whichever variant is populated and surface the event source as the
    /// <c>Type</c> ("system" / "user") since Polar doesn't expose a richer discriminator
    /// at the list-payload level. The <c>PayloadJson</c> field stays null in the snapshot
    /// at this phase — Polar's list endpoint doesn't include the per-event payload blob;
    /// fetching that requires per-event GETs which is deferred to a v2.x enrichment.
    /// </remarks>
    public async Task<Result<IReadOnlyList<EventPayload>, PolarReportingApiError>> FetchEventsSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            // Events uses the newer GetAsGetResponseAsync API; `GetResponse` is itself a
            // discriminated union of `ListResource_Event_` (page-based pagination) and
            // `ListResourceWithCursorPagination_Event_` (cursor-based). Both variants
            // expose `.Items` — extract from whichever is populated.
            var response = await _polar.Events.EmptyPathSegment.GetAsGetResponseAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items =
                response?.ListResourceEvent?.Items
                ?? response?.ListResourceWithCursorPaginationEvent?.Items
                ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<EventPayload>, PolarReportingApiError>.Success(Array.Empty<EventPayload>());

            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ExtractEventId(ascending[i]), sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<EventPayload>(ascending.Count);
            foreach (var e in ascending)
            {
                var payload = MapEvent(e);
                if (payload is not null) mapped.Add(payload);
            }
            return Result<IReadOnlyList<EventPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<EventPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "events"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching events.");
            return Result<IReadOnlyList<EventPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static string? ExtractEventId(global::PolarSharp.Generated.Models.Event e) =>
        e.SystemEvent?.Id ?? e.UserEvent?.Id;

    private EventPayload? MapEvent(global::PolarSharp.Generated.Models.Event e)
    {
        if (e.SystemEvent is { } s) return new EventPayload(
            Id: s.Id ?? string.Empty,
            Type: $"system:{s.Name ?? "unknown"}",
            OccurredAt: s.Timestamp ?? DateTimeOffset.UtcNow,
            PayloadJson: null);
        if (e.UserEvent is { } u) return new EventPayload(
            Id: u.Id ?? string.Empty,
            Type: $"user:{u.Name ?? "unknown"}",
            OccurredAt: u.Timestamp ?? DateTimeOffset.UtcNow,
            PayloadJson: null);
        _logger.LogWarning("Event row had no populated subtype variant — skipping in snapshot ingestion.");
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1.5: live wiring against <c>GET /v1/orders/</c>. Maps top-level
    /// fields directly; <c>Status</c> is a Polar enum, lowercased for the snapshot's
    /// wire-format string (Polar's wire format itself is lowercase snake_case, which
    /// matches <c>OrderStatus.ToString().ToLowerInvariant()</c>). <c>InvoiceUrl</c> and
    /// <c>FulfilledAt</c> are NOT exposed on Polar's Order list endpoint at this version
    /// — they remain null in the snapshot. Per-line-item ingestion is best-effort:
    /// <c>OrderItemSchema</c> exposes <c>Label</c> + <c>Amount</c> + <c>TaxAmount</c> but
    /// not <c>ProductId</c> or <c>Quantity</c>. The snapshot entity's <c>ProductId</c>
    /// column is NOT NULL (HasMaxLength(64).IsRequired); since Polar doesn't surface it,
    /// we omit line items at this phase and let the per-order aggregate
    /// <c>RefundedAmount</c> drive the drilldown grid. v2.x enrichment can join
    /// <c>OrderItemSchema.ProductPriceId</c> against the prices snapshot to recover
    /// ProductId. Refunds are similarly omitted at this phase — Polar's Order resource
    /// has no nested refunds list; the aggregate <c>RefundedAmount</c> on the order row
    /// is the source of truth.
    /// </remarks>
    public async Task<Result<IReadOnlyList<OrderPayload>, PolarReportingApiError>> FetchOrdersSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.Orders.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<OrderPayload>, PolarReportingApiError>.Success(Array.Empty<OrderPayload>());

            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ascending[i].Id, sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<OrderPayload>(ascending.Count);
            foreach (var o in ascending)
            {
                mapped.Add(new OrderPayload(
                    Id: o.Id ?? string.Empty,
                    Number: o.InvoiceNumber ?? string.Empty,
                    CustomerId: o.CustomerId ?? string.Empty,
                    Status: o.Status?.ToString()?.ToLowerInvariant() ?? "pending",
                    Amount: o.TotalAmount ?? 0,
                    TaxAmount: o.TaxAmount ?? 0,
                    RefundedAmount: o.RefundedAmount ?? 0,
                    Currency: o.Currency ?? string.Empty,
                    InvoiceUrl: null,
                    CreatedAt: o.CreatedAt ?? DateTimeOffset.UtcNow,
                    FulfilledAt: null,
                    LineItems: Array.Empty<OrderLineItemPayload>(),
                    Refunds: Array.Empty<OrderRefundPayload>()));
            }
            return Result<IReadOnlyList<OrderPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<OrderPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "orders"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching orders.");
            return Result<IReadOnlyList<OrderPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1.5: live wiring against <c>GET /v1/subscriptions/</c>. <c>Status</c>
    /// is a <c>SubscriptionStatus</c> enum (incomplete / trialing / active / past_due /
    /// canceled / unpaid / incomplete_expired); <c>.ToString().ToLowerInvariant()</c>
    /// matches Polar's snake_case wire format. <c>StartedAt</c> is a union-wrapped
    /// DateTimeOffset; <c>EndedAt</c> is the canceled-at field (distinct from <c>EndsAt</c>
    /// which is the scheduled end of the current period).
    /// </remarks>
    public async Task<Result<IReadOnlyList<SubscriptionPayload>, PolarReportingApiError>> FetchSubscriptionsSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.Subscriptions.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<SubscriptionPayload>, PolarReportingApiError>.Success(Array.Empty<SubscriptionPayload>());

            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ascending[i].Id, sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<SubscriptionPayload>(ascending.Count);
            foreach (var s in ascending)
            {
                mapped.Add(new SubscriptionPayload(
                    Id: s.Id ?? string.Empty,
                    CustomerId: s.CustomerId ?? string.Empty,
                    ProductId: s.ProductId ?? string.Empty,
                    Status: s.Status?.ToString()?.ToLowerInvariant() ?? "incomplete",
                    StartedAt: s.StartedAt?.DateTimeOffset ?? s.CreatedAt ?? DateTimeOffset.UtcNow,
                    CanceledAt: s.EndedAt?.DateTimeOffset));
            }
            return Result<IReadOnlyList<SubscriptionPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<SubscriptionPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "subscriptions"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching subscriptions.");
            return Result<IReadOnlyList<SubscriptionPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1.5: live wiring against <c>GET /v1/customers/</c>. Polar's
    /// <c>Customer</c> is a discriminated union — <c>CustomerIndividual</c> + <c>CustomerTeam</c>.
    /// We probe each variant for the Id/Email/Name/CreatedAt fields. The team variant
    /// doesn't expose an <c>Email</c> field on the wire (teams aren't a single mailbox);
    /// we surface an empty string in that case. Polar's Customer model has no
    /// <c>Currency</c> field — that lives on associated orders/subscriptions; the
    /// customer ingestion pass surfaces an empty string and the per-customer aggregate
    /// roll-up (LifetimeValue) draws currency from the order-side.
    /// </remarks>
    public async Task<Result<IReadOnlyList<CustomerPayload>, PolarReportingApiError>> FetchCustomersSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.Customers.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<CustomerPayload>, PolarReportingApiError>.Success(Array.Empty<CustomerPayload>());

            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ExtractCustomerId(ascending[i]), sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<CustomerPayload>(ascending.Count);
            foreach (var c in ascending)
            {
                var payload = MapCustomer(c);
                if (payload is not null) mapped.Add(payload);
            }
            return Result<IReadOnlyList<CustomerPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<CustomerPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "customers"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching customers.");
            return Result<IReadOnlyList<CustomerPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static string? ExtractCustomerId(global::PolarSharp.Generated.Models.Customer c) =>
        c.CustomerIndividual?.Id ?? c.CustomerTeam?.Id;

    private CustomerPayload? MapCustomer(global::PolarSharp.Generated.Models.Customer c)
    {
        if (c.CustomerIndividual is { } ind) return new CustomerPayload(
            Id: ind.Id ?? string.Empty,
            Email: ind.Email ?? string.Empty,
            Name: ind.Name?.String,
            Currency: string.Empty,
            CreatedAt: ind.CreatedAt ?? DateTimeOffset.UtcNow);
        if (c.CustomerTeam is { } team) return new CustomerPayload(
            Id: team.Id ?? string.Empty,
            Email: string.Empty,
            Name: null,
            Currency: string.Empty,
            CreatedAt: team.CreatedAt ?? DateTimeOffset.UtcNow);
        _logger.LogWarning("Customer row had no populated subtype variant — skipping in snapshot ingestion.");
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1.5: live wiring against <c>GET /v1/benefit-grants/</c>. Polar's
    /// org-level <c>BenefitGrant</c> (distinct from the customer-portal-side
    /// <c>CustomerBenefitGrant</c>) is a single shape with union-wrapped <c>GrantedAt</c>
    /// / <c>RevokedAt</c> / <c>OrderId</c> fields. Polar does NOT denormalize the parent
    /// benefit's <c>Name</c> or <c>Kind</c> onto the grant payload — the snapshot
    /// surfaces <c>BenefitId</c> as both the BenefitName and BenefitKind placeholder; a
    /// v2.x enrichment can join against the benefits snapshot to populate richer columns.
    /// </remarks>
    public async Task<Result<IReadOnlyList<BenefitGrantPayload>, PolarReportingApiError>> FetchBenefitGrantsSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.BenefitGrants.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<BenefitGrantPayload>, PolarReportingApiError>.Success(Array.Empty<BenefitGrantPayload>());

            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ascending[i].Id, sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<BenefitGrantPayload>(ascending.Count);
            foreach (var g in ascending)
            {
                mapped.Add(new BenefitGrantPayload(
                    Id: g.Id ?? string.Empty,
                    CustomerId: g.CustomerId ?? string.Empty,
                    OrderId: g.OrderId?.String,
                    BenefitId: g.BenefitId ?? string.Empty,
                    BenefitName: g.BenefitId ?? string.Empty,
                    BenefitKind: "unknown",
                    IsGranted: g.IsGranted ?? false,
                    GrantedAt: g.GrantedAt?.DateTimeOffset,
                    RevokedAt: g.RevokedAt?.DateTimeOffset));
            }
            return Result<IReadOnlyList<BenefitGrantPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<BenefitGrantPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "benefit-grants"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching benefit grants.");
            return Result<IReadOnlyList<BenefitGrantPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    // ── V20-005 Phase 1: 7 additional resource impls (live-wired) ──────────────

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1E: live wiring against <c>GET /v1/benefits/</c>. <c>Benefit</c> is a
    /// discriminated union wrapping seven subtypes (BenefitCustom, BenefitDiscord,
    /// BenefitDownloadables, BenefitFeatureFlag, BenefitGitHubRepository, BenefitLicenseKeys,
    /// BenefitMeterCredit) — exactly one subtype property is non-null per row. We probe each
    /// in order and extract the shared fields (Id, Description, IsActive, CreatedAt). The
    /// discriminator becomes the <c>Kind</c> value, mapped to Polar's wire-format string.
    /// </remarks>
    public async Task<Result<IReadOnlyList<BenefitPayload>, PolarReportingApiError>> FetchBenefitsSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.Benefits.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<BenefitPayload>, PolarReportingApiError>.Success(Array.Empty<BenefitPayload>());

            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ExtractBenefitId(ascending[i]), sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<BenefitPayload>(ascending.Count);
            foreach (var b in ascending)
            {
                var payload = MapBenefit(b);
                if (payload is not null) mapped.Add(payload);
            }
            return Result<IReadOnlyList<BenefitPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<BenefitPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "benefits"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching benefits.");
            return Result<IReadOnlyList<BenefitPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <summary>
    /// Extracts the Polar id from whichever subtype variant is populated on the
    /// <see cref="global::PolarSharp.Generated.Models.Benefit"/> wrapper.
    /// </summary>
    private static string? ExtractBenefitId(global::PolarSharp.Generated.Models.Benefit b) =>
        b.BenefitCustom?.Id
        ?? b.BenefitDiscord?.Id
        ?? b.BenefitDownloadables?.Id
        ?? b.BenefitFeatureFlag?.Id
        ?? b.BenefitGitHubRepository?.Id
        ?? b.BenefitLicenseKeys?.Id
        ?? b.BenefitMeterCredit?.Id;

    /// <summary>
    /// Probes each subtype on the Benefit discriminator wrapper, returning a populated
    /// payload for the first non-null variant. Returns null if the wrapper has no populated
    /// variant (Polar returned a row with an unknown <c>type</c> discriminator — we log
    /// + skip rather than fail).
    /// </summary>
    private BenefitPayload? MapBenefit(global::PolarSharp.Generated.Models.Benefit b)
    {
        // Each subtype has the same shared shape (Id, Description, Selectable, CreatedAt,
        // ModifiedAt). The `Name` we surface is the Description truncated to fit the
        // entity's 256-char limit; Polar's Benefit model has no separate display-name
        // field for most subtypes.
        if (b.BenefitCustom is { } c)              return Build(c.Id, c.Description, "custom", c.Selectable, c.CreatedAt, c.ModifiedAt?.DateTimeOffset);
        if (b.BenefitDiscord is { } d)             return Build(d.Id, d.Description, "discord", d.Selectable, d.CreatedAt, d.ModifiedAt?.DateTimeOffset);
        if (b.BenefitDownloadables is { } dl)      return Build(dl.Id, dl.Description, "downloadables", dl.Selectable, dl.CreatedAt, dl.ModifiedAt?.DateTimeOffset);
        if (b.BenefitFeatureFlag is { } ff)        return Build(ff.Id, ff.Description, "feature_flag", ff.Selectable, ff.CreatedAt, ff.ModifiedAt?.DateTimeOffset);
        if (b.BenefitGitHubRepository is { } gh)   return Build(gh.Id, gh.Description, "github_repository", gh.Selectable, gh.CreatedAt, gh.ModifiedAt?.DateTimeOffset);
        if (b.BenefitLicenseKeys is { } lk)        return Build(lk.Id, lk.Description, "license_keys", lk.Selectable, lk.CreatedAt, lk.ModifiedAt?.DateTimeOffset);
        if (b.BenefitMeterCredit is { } mc)        return Build(mc.Id, mc.Description, "meter_credit", mc.Selectable, mc.CreatedAt, mc.ModifiedAt?.DateTimeOffset);
        _logger.LogWarning("Benefit row had no populated subtype variant — skipping in snapshot ingestion.");
        return null;

        static BenefitPayload Build(string? id, string? description, string kind, bool? selectable, DateTimeOffset? createdAt, DateTimeOffset? modifiedAt) =>
            new(
                Id: id ?? string.Empty,
                Name: Truncate(description ?? kind, 256),
                Kind: kind,
                Description: description,
                IsActive: selectable ?? true,
                CreatedAt: createdAt ?? DateTimeOffset.UtcNow,
                ModifiedAt: modifiedAt);

        static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
    }

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1H: live wiring against <c>GET /v1/discounts/</c>. Polar's
    /// <c>Discount</c> is a 4-way discriminated union wrapper:
    /// <c>DiscountFixedOnceForeverDuration</c> + <c>DiscountFixedRepeatDuration</c> +
    /// <c>DiscountPercentageOnceForeverDuration</c> + <c>DiscountPercentageRepeatDuration</c>.
    /// Fixed variants carry <c>Amount</c> + <c>Currency</c>; Percentage variants carry
    /// <c>BasisPoints</c> (percentage × 100). The remaining fields (Id / Name / Code /
    /// MaxRedemptions / RedemptionsCount / StartsAt / EndsAt / CreatedAt) are uniform
    /// across all four. The wrapper probes each variant and extracts both shared and
    /// variant-specific fields.
    /// </remarks>
    public async Task<Result<IReadOnlyList<DiscountPayload>, PolarReportingApiError>> FetchDiscountsSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.Discounts.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<DiscountPayload>, PolarReportingApiError>.Success(Array.Empty<DiscountPayload>());

            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ExtractDiscountId(ascending[i]), sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<DiscountPayload>(ascending.Count);
            foreach (var d in ascending)
            {
                var payload = MapDiscount(d);
                if (payload is not null) mapped.Add(payload);
            }
            return Result<IReadOnlyList<DiscountPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<DiscountPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "discounts"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching discounts.");
            return Result<IReadOnlyList<DiscountPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static string? ExtractDiscountId(global::PolarSharp.Generated.Models.Discount d) =>
        d.DiscountFixedOnceForeverDuration?.Id
        ?? d.DiscountFixedRepeatDuration?.Id
        ?? d.DiscountPercentageOnceForeverDuration?.Id
        ?? d.DiscountPercentageRepeatDuration?.Id;

    /// <summary>
    /// Probes each of the 4 Discount subtype variants and produces a DiscountPayload from
    /// whichever is populated. Returns null if no variant is populated (defensive — Polar
    /// could ship a 5th discount type and we'd skip-with-warning rather than fail).
    /// </summary>
    private DiscountPayload? MapDiscount(global::PolarSharp.Generated.Models.Discount d)
    {
        // Note: Polar deprecated single-currency `Amount` + `Currency` on Fixed discount
        // subtypes (Kiota flagged both [Obsolete]); they're replaced with a multi-currency
        // `Amounts` collection. The v2.0 snapshot surfaces `AmountOff = null` and
        // `Currency = null` for Fixed variants — flagged as v2.x enrichment to ingest the
        // Amounts collection into a per-currency expanded representation. PercentOff /
        // RedemptionsSoFar / MaxRedemptions / dates still map cleanly.
        if (d.DiscountFixedOnceForeverDuration is { } fo)
        {
            return new DiscountPayload(
                Id: fo.Id ?? string.Empty,
                Name: fo.Name ?? "(unnamed discount)",
                Code: fo.Code?.String,
                Type: "fixed",
                AmountOff: null,
                PercentOff: null,
                Currency: null,
                RedemptionsSoFar: fo.RedemptionsCount,
                MaxRedemptions: fo.MaxRedemptions?.Integer,
                StartsAt: fo.StartsAt?.DateTimeOffset,
                EndsAt: fo.EndsAt?.DateTimeOffset,
                CreatedAt: fo.CreatedAt ?? DateTimeOffset.UtcNow);
        }
        if (d.DiscountFixedRepeatDuration is { } fr)
        {
            return new DiscountPayload(
                Id: fr.Id ?? string.Empty,
                Name: fr.Name ?? "(unnamed discount)",
                Code: fr.Code?.String,
                Type: "fixed",
                AmountOff: null,
                PercentOff: null,
                Currency: null,
                RedemptionsSoFar: fr.RedemptionsCount,
                MaxRedemptions: fr.MaxRedemptions?.Integer,
                StartsAt: fr.StartsAt?.DateTimeOffset,
                EndsAt: fr.EndsAt?.DateTimeOffset,
                CreatedAt: fr.CreatedAt ?? DateTimeOffset.UtcNow);
        }
        if (d.DiscountPercentageOnceForeverDuration is { } po)
        {
            return new DiscountPayload(
                Id: po.Id ?? string.Empty,
                Name: po.Name ?? "(unnamed discount)",
                Code: po.Code?.String,
                Type: "percentage",
                AmountOff: null,
                PercentOff: po.BasisPoints is { } bp ? bp / 100m : null,
                Currency: null,
                RedemptionsSoFar: po.RedemptionsCount,
                MaxRedemptions: po.MaxRedemptions?.Integer,
                StartsAt: po.StartsAt?.DateTimeOffset,
                EndsAt: po.EndsAt?.DateTimeOffset,
                CreatedAt: po.CreatedAt ?? DateTimeOffset.UtcNow);
        }
        if (d.DiscountPercentageRepeatDuration is { } pr)
        {
            return new DiscountPayload(
                Id: pr.Id ?? string.Empty,
                Name: pr.Name ?? "(unnamed discount)",
                Code: pr.Code?.String,
                Type: "percentage",
                AmountOff: null,
                PercentOff: pr.BasisPoints is { } bp ? bp / 100m : null,
                Currency: null,
                RedemptionsSoFar: pr.RedemptionsCount,
                MaxRedemptions: pr.MaxRedemptions?.Integer,
                StartsAt: pr.StartsAt?.DateTimeOffset,
                EndsAt: pr.EndsAt?.DateTimeOffset,
                CreatedAt: pr.CreatedAt ?? DateTimeOffset.UtcNow);
        }
        _logger.LogWarning("Discount row had no populated subtype variant — skipping in snapshot ingestion.");
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1G: live wiring against <c>GET /v1/checkout-links/</c>. Polar's
    /// CheckoutLink has multiple string-or-object union fields (<c>Label</c>,
    /// <c>SuccessUrl</c>, <c>ModifiedAt</c>, <c>DiscountId</c>, <c>ReturnUrl</c>); each
    /// extracts its <c>.String</c> / <c>.DateTimeOffset</c> variant. The top-level
    /// <c>Url</c> is a plain <c>string?</c> with private setter — exposed via Polar's
    /// JSON response. <c>Products</c> is a list of <c>CheckoutLinkProduct</c>; we collect
    /// each product's Id into a CSV string for the snapshot row (entity has a 2048-char
    /// ProductIdsCsv field). The sensitive <c>ClientSecret</c> field is deliberately NOT
    /// mapped.
    /// </remarks>
    public async Task<Result<IReadOnlyList<CheckoutLinkPayload>, PolarReportingApiError>> FetchCheckoutLinksSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.CheckoutLinks.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<CheckoutLinkPayload>, PolarReportingApiError>.Success(Array.Empty<CheckoutLinkPayload>());

            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ascending[i].Id, sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<CheckoutLinkPayload>(ascending.Count);
            foreach (var cl in ascending)
            {
                var productIds = cl.Products?
                    .Where(p => !string.IsNullOrEmpty(p.Id))
                    .Select(p => p.Id!)
                    .ToList() ?? [];
                mapped.Add(new CheckoutLinkPayload(
                    Id: cl.Id ?? string.Empty,
                    Label: cl.Label?.String ?? "(unnamed checkout link)",
                    ProductIds: productIds,
                    Url: cl.Url,
                    SuccessUrl: cl.SuccessUrl?.String,
                    AllowDiscountCodes: cl.AllowDiscountCodes ?? true,
                    CreatedAt: cl.CreatedAt ?? DateTimeOffset.UtcNow));
            }
            return Result<IReadOnlyList<CheckoutLinkPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<CheckoutLinkPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "checkout-links"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching checkout links.");
            return Result<IReadOnlyList<CheckoutLinkPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1B: live wiring against <c>GET /v1/products/</c>. Page-1 cursor
    /// semantics — see the class-level remarks. Map: <c>Product</c> -&gt; <c>ProductPayload</c>.
    /// <c>Description</c> is a string-or-object union; extract <c>.String</c>. <c>ModifiedAt</c>
    /// is a DateTime-or-object union; extract <c>.DateTimeOffset</c>. <c>RecurringInterval</c>
    /// is a Polar enum; we stringify it for our payload (wire-format value preserved).
    /// </remarks>
    public async Task<Result<IReadOnlyList<ProductPayload>, PolarReportingApiError>> FetchProductsSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.Products.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<ProductPayload>, PolarReportingApiError>.Success(Array.Empty<ProductPayload>());

            // Polar returns descending by created_at; reverse to ascending so the snapshot
            // service's `cursor = rows[^1].Id` advances to the newest ingested row.
            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ascending[i].Id, sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<ProductPayload>(ascending.Count);
            foreach (var p in ascending)
            {
                mapped.Add(new ProductPayload(
                    Id: p.Id ?? string.Empty,
                    Name: p.Name ?? string.Empty,
                    Description: p.Description?.String,
                    IsRecurring: p.IsRecurring ?? false,
                    RecurringInterval: p.RecurringInterval?.ToString()?.ToLowerInvariant(),
                    IsArchived: p.IsArchived ?? false,
                    CreatedAt: p.CreatedAt ?? DateTimeOffset.UtcNow,
                    ModifiedAt: p.ModifiedAt?.DateTimeOffset));
            }
            return Result<IReadOnlyList<ProductPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<ProductPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "products"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching products.");
            return Result<IReadOnlyList<ProductPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1D: live wiring against <c>GET /v1/license-keys/</c>. Multiple
    /// union-wrapped fields: <c>ExpiresAt</c> + <c>ModifiedAt</c> + <c>LastValidatedAt</c>
    /// (DateTimeOffset-or-object); <c>LimitActivations</c> + <c>LimitUsage</c>
    /// (Integer-or-object). Extract the populated variant on each. The raw <c>Key</c> is
    /// deliberately NOT mapped — we surface <c>DisplayKey</c> (Polar's masked-for-UI form)
    /// so the snapshot never carries the sensitive raw key string. <c>Usage</c> is mapped
    /// to <c>ActivationsUsed</c>; semantically it's "validation count" in Polar's model
    /// (distinct from raw activation count), but it's the only usage-counter Polar
    /// surfaces at the list level — close-enough for the snapshot.
    /// </remarks>
    public async Task<Result<IReadOnlyList<LicenseKeyPayload>, PolarReportingApiError>> FetchLicenseKeysSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.LicenseKeys.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<LicenseKeyPayload>, PolarReportingApiError>.Success(Array.Empty<LicenseKeyPayload>());

            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ascending[i].Id, sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<LicenseKeyPayload>(ascending.Count);
            foreach (var lk in ascending)
            {
                mapped.Add(new LicenseKeyPayload(
                    Id: lk.Id ?? string.Empty,
                    CustomerId: lk.CustomerId ?? string.Empty,
                    BenefitId: lk.BenefitId,
                    DisplayKey: lk.DisplayKey,
                    Status: lk.Status?.ToString()?.ToLowerInvariant() ?? "granted",
                    LimitActivations: lk.LimitActivations?.Integer,
                    ActivationsUsed: lk.Usage,
                    ExpiresAt: lk.ExpiresAt?.DateTimeOffset,
                    CreatedAt: lk.CreatedAt ?? DateTimeOffset.UtcNow));
            }
            return Result<IReadOnlyList<LicenseKeyPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<LicenseKeyPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "license-keys"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching license keys.");
            return Result<IReadOnlyList<LicenseKeyPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1F: live wiring against <c>GET /v1/meters/</c>. Polar's
    /// <c>Meter.Aggregation</c> is a discriminated union on a field (not the top-level
    /// shape) — variants: <c>CountAggregation</c>, <c>PropertyAggregation</c>,
    /// <c>UniqueAggregation</c>. Each carries a <c>Func</c> field naming the actual
    /// aggregation function (sum, max, avg, count, unique). We probe each variant in
    /// order and surface the function name as the snapshot's <c>AggregationKind</c>.
    /// </remarks>
    public async Task<Result<IReadOnlyList<MeterPayload>, PolarReportingApiError>> FetchMetersSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.Meters.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<MeterPayload>, PolarReportingApiError>.Success(Array.Empty<MeterPayload>());

            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ascending[i].Id, sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<MeterPayload>(ascending.Count);
            foreach (var m in ascending)
            {
                mapped.Add(new MeterPayload(
                    Id: m.Id ?? string.Empty,
                    Name: m.Name ?? "(unnamed)",
                    AggregationKind: ExtractAggregationKind(m.Aggregation),
                    CreatedAt: m.CreatedAt ?? DateTimeOffset.UtcNow));
            }
            return Result<IReadOnlyList<MeterPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<MeterPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "meters"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching meters.");
            return Result<IReadOnlyList<MeterPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <summary>
    /// Probes the Meter_aggregation discriminator wrapper and returns the aggregation
    /// function name (sum / count / avg / max / min / unique) as a lowercase wire string.
    /// Defaults to "unknown" when no variant is populated.
    /// </summary>
    private static string ExtractAggregationKind(global::PolarSharp.Generated.Models.Meter.Meter_aggregation? agg)
    {
        if (agg is null) return "unknown";
        if (agg.CountAggregation is { Func: { Length: > 0 } cf }) return cf.ToLowerInvariant();
        if (agg.PropertyAggregation is { Func: { } pf }) return pf.ToString().ToLowerInvariant();
        if (agg.UniqueAggregation is { Func: { Length: > 0 } uf }) return uf.ToLowerInvariant();
        return "unknown";
    }

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1C: live wiring against <c>GET /v1/customer-meters/</c>. Mostly flat
    /// fields; only <c>ModifiedAt</c> is a string-or-DateTimeOffset union (extract
    /// <c>.DateTimeOffset</c>). Polar's <c>ConsumedUnits</c> is <c>double?</c> wire-side; we
    /// surface <c>decimal</c> in <c>CustomerMeterPayload</c> for consistent reporting math.
    /// </remarks>
    public async Task<Result<IReadOnlyList<CustomerMeterPayload>, PolarReportingApiError>> FetchCustomerMetersSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.CustomerMeters.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<CustomerMeterPayload>, PolarReportingApiError>.Success(Array.Empty<CustomerMeterPayload>());

            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ascending[i].Id, sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<CustomerMeterPayload>(ascending.Count);
            foreach (var cm in ascending)
            {
                mapped.Add(new CustomerMeterPayload(
                    Id: cm.Id ?? string.Empty,
                    CustomerId: cm.CustomerId ?? string.Empty,
                    MeterId: cm.MeterId ?? string.Empty,
                    ConsumedUnits: (decimal)(cm.ConsumedUnits ?? 0d),
                    CreditedUnits: (decimal?)cm.CreditedUnits,
                    CreatedAt: cm.CreatedAt ?? DateTimeOffset.UtcNow,
                    ModifiedAt: cm.ModifiedAt?.DateTimeOffset));
            }
            return Result<IReadOnlyList<CustomerMeterPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<CustomerMeterPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "customer-meters"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching customer meters.");
            return Result<IReadOnlyList<CustomerMeterPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static PolarReportingApiError MapApiException(ApiException ex, string resourceName)
    {
        var kind = ex.ResponseStatusCode switch
        {
            401 or 403 => PolarReportingApiErrorKind.AuthorizationFailed,
            429 => PolarReportingApiErrorKind.RateLimited,
            _ => PolarReportingApiErrorKind.UnexpectedFailure,
        };
        return new PolarReportingApiError(kind, $"Polar {resourceName} {ex.ResponseStatusCode}: {ex.Message}");
    }
}
