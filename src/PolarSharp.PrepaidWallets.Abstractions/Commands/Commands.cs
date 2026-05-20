namespace PolarSharp.PrepaidWallets.Abstractions.Commands;

/// <summary>Standard handler response — either a successful append or a typed domain error.</summary>
public sealed record WalletCommandResult(Result<CommandSuccess, CommandError> Outcome);

/// <summary>Open a new wallet.</summary>
/// <param name="WalletId">The wallet identifier.</param>
/// <param name="CustomerId">Customer who owns the wallet.</param>
/// <param name="TenantId">Tenant scope. <see cref="Option{T}.None"/> in single-tenant deployments.</param>
/// <param name="Currency">ISO-4217 currency code (e.g. "USD"). Locked for the wallet's lifetime.</param>
/// <param name="ActorUserId">The operator who issued the command.</param>
/// <param name="IdempotencyKey">Idempotency key for retry safety.</param>
public sealed record OpenWalletCommand(
    WalletId WalletId,
    Guid CustomerId,
    Option<Guid> TenantId,
    string Currency,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey) : IWalletCommand<WalletCommandResult>;

/// <summary>Fund a wallet — credit tokens corresponding to a customer payment.</summary>
/// <param name="WalletId">Target wallet.</param>
/// <param name="Amount">Tokens credited (face value).</param>
/// <param name="BonusTokens">Bonus tokens credited (zero when no bonus applies).</param>
/// <param name="Source">Which processor delivered the payment (processor-level provenance).</param>
/// <param name="SourceKind">Tax-bucket category of the funded tokens (semantic provenance for Phase 22.5 WTR).</param>
/// <param name="CustomerChargedAmountCents">Customer-charged amount, in cents.</param>
/// <param name="ProcessorFeeCents">Processor fee withheld, in cents.</param>
/// <param name="SaaSProfitCents">SaaS share, in cents.</param>
/// <param name="TenantAbsorbedAmountCents">Tenant-absorbed amount, in cents (zero outside <c>AbsorbAndMarkup</c> mode).</param>
/// <param name="TenantNetAmountCents">Tenant net amount, in cents.</param>
/// <param name="FundingTermsSnapshotJson">Serialized funding-terms snapshot.</param>
/// <param name="ActorUserId">The actor on whose behalf the funding happened.</param>
/// <param name="IdempotencyKey">Idempotency key for retry safety.</param>
public sealed record FundWalletCommand(
    WalletId WalletId,
    TokenAmount Amount,
    TokenAmount BonusTokens,
    FundingSource Source,
    FundingSourceKind SourceKind,
    int CustomerChargedAmountCents,
    int ProcessorFeeCents,
    int SaaSProfitCents,
    int TenantAbsorbedAmountCents,
    int TenantNetAmountCents,
    string FundingTermsSnapshotJson,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey) : IWalletCommand<WalletCommandResult>;

/// <summary>Debit tokens from a wallet against an external target.</summary>
/// <param name="WalletId">Target wallet.</param>
/// <param name="Amount">Tokens to debit.</param>
/// <param name="TargetKind">Free-form kind of the debit target (e.g. <c>"order_line"</c>, <c>"subscription_invoice"</c>).</param>
/// <param name="TargetId">Target id.</param>
/// <param name="ActorUserId">The actor on whose behalf the debit happened.</param>
/// <param name="IdempotencyKey">Idempotency key for retry safety.</param>
public sealed record DebitWalletCommand(
    WalletId WalletId,
    TokenAmount Amount,
    string TargetKind,
    string TargetId,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey) : IWalletCommand<WalletCommandResult>;

/// <summary>Credit tokens to a wallet for a reason other than a customer funding payment.</summary>
/// <param name="WalletId">Target wallet.</param>
/// <param name="Amount">Tokens to credit.</param>
/// <param name="Reason">Free-form reason.</param>
/// <param name="SourceKind">Tax-bucket category of the credited tokens. Drives the Phase 22.5 WTR framework's discount-vs-revenue classification.</param>
/// <param name="RelatedPurchaseOrderId">Optional PO id when the credit consumes PO authorization.</param>
/// <param name="ActorUserId">The operator that issued the credit.</param>
/// <param name="IdempotencyKey">Idempotency key for retry safety.</param>
public sealed record CreditWalletCommand(
    WalletId WalletId,
    TokenAmount Amount,
    string Reason,
    FundingSourceKind SourceKind,
    Option<Guid> RelatedPurchaseOrderId,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey) : IWalletCommand<WalletCommandResult>;

/// <summary>Refund tokens from a wallet, recording the cents-breakdown of the refund.</summary>
/// <param name="WalletId">Target wallet.</param>
/// <param name="TokensRefunded">Tokens to debit from the wallet.</param>
/// <param name="OriginalFundingSequenceNo">Sequence number of the <c>WalletFunded</c> event being refunded.</param>
/// <param name="CustomerRefundedAmountCents">Refund amount returned to the customer's instrument, in cents.</param>
/// <param name="SurchargeAmountCents">Surcharge withheld, in cents.</param>
/// <param name="SaaSShareAmountCents">SaaS share withheld, in cents.</param>
/// <param name="ActorUserId">The actor that initiated the refund.</param>
/// <param name="IdempotencyKey">Idempotency key for retry safety.</param>
public sealed record RefundWalletCommand(
    WalletId WalletId,
    TokenAmount TokensRefunded,
    long OriginalFundingSequenceNo,
    int CustomerRefundedAmountCents,
    int SurchargeAmountCents,
    int SaaSShareAmountCents,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey) : IWalletCommand<WalletCommandResult>;

/// <summary>Freeze a wallet — reject funding + debits until unfrozen.</summary>
/// <param name="WalletId">Target wallet.</param>
/// <param name="Reason">Free-form reason for the freeze.</param>
/// <param name="ActorUserId">The operator who froze the wallet.</param>
/// <param name="IdempotencyKey">Idempotency key for retry safety.</param>
public sealed record FreezeWalletCommand(
    WalletId WalletId,
    string Reason,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey) : IWalletCommand<WalletCommandResult>;

/// <summary>Re-activate a previously frozen wallet.</summary>
/// <param name="WalletId">Target wallet.</param>
/// <param name="ActorUserId">The operator who lifted the freeze.</param>
/// <param name="IdempotencyKey">Idempotency key for retry safety.</param>
public sealed record UnfreezeWalletCommand(
    WalletId WalletId,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey) : IWalletCommand<WalletCommandResult>;

/// <summary>Terminally close a wallet.</summary>
/// <param name="WalletId">Target wallet.</param>
/// <param name="Reason">Free-form reason for the closure.</param>
/// <param name="ActorUserId">The operator who closed the wallet.</param>
/// <param name="IdempotencyKey">Idempotency key for retry safety.</param>
public sealed record CloseWalletCommand(
    WalletId WalletId,
    string Reason,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey) : IWalletCommand<WalletCommandResult>;
