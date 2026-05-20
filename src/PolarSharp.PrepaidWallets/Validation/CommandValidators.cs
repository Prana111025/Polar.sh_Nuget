using FluentValidation;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Commands;

namespace PolarSharp.PrepaidWallets.Validation;

/// <summary>Validator for <see cref="OpenWalletCommand"/>.</summary>
public sealed class OpenWalletCommandValidator : AbstractValidator<OpenWalletCommand>
{
    /// <summary>Configure the validation rules.</summary>
    public OpenWalletCommandValidator()
    {
        RuleFor(x => x.WalletId.Value).NotEqual(Guid.Empty).WithMessage("WalletId must not be empty.");
        RuleFor(x => x.CustomerId).NotEqual(Guid.Empty).WithMessage("CustomerId must not be empty.");
        RuleFor(x => x.ActorUserId).NotEqual(Guid.Empty).WithMessage("ActorUserId must not be empty.");
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .WithMessage("Currency must be an ISO-4217 3-letter code.");
    }
}

/// <summary>Validator for <see cref="FundWalletCommand"/>.</summary>
public sealed class FundWalletCommandValidator : AbstractValidator<FundWalletCommand>
{
    /// <summary>Configure the validation rules.</summary>
    public FundWalletCommandValidator()
    {
        RuleFor(x => x.WalletId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount.Value).GreaterThan(0).WithMessage("Funding amount must be positive.");
        RuleFor(x => x.BonusTokens.Value).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CustomerChargedAmountCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ProcessorFeeCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SaaSProfitCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TenantAbsorbedAmountCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TenantNetAmountCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FundingTermsSnapshotJson).NotEmpty();
        RuleFor(x => x.Source).NotNull();
    }
}

/// <summary>Validator for <see cref="DebitWalletCommand"/>.</summary>
public sealed class DebitWalletCommandValidator : AbstractValidator<DebitWalletCommand>
{
    /// <summary>Configure the validation rules.</summary>
    public DebitWalletCommandValidator()
    {
        RuleFor(x => x.WalletId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount.Value).GreaterThan(0).WithMessage("Debit amount must be positive.");
        RuleFor(x => x.TargetKind).NotEmpty().MaximumLength(64);
        RuleFor(x => x.TargetId).NotEmpty().MaximumLength(128);
    }
}

/// <summary>Validator for <see cref="CreditWalletCommand"/>.</summary>
public sealed class CreditWalletCommandValidator : AbstractValidator<CreditWalletCommand>
{
    /// <summary>Configure the validation rules.</summary>
    public CreditWalletCommandValidator()
    {
        RuleFor(x => x.WalletId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount.Value).GreaterThan(0).WithMessage("Credit amount must be positive.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(512);
    }
}

/// <summary>Validator for <see cref="RefundWalletCommand"/>.</summary>
public sealed class RefundWalletCommandValidator : AbstractValidator<RefundWalletCommand>
{
    /// <summary>Configure the validation rules.</summary>
    public RefundWalletCommandValidator()
    {
        RuleFor(x => x.WalletId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.TokensRefunded.Value).GreaterThan(0).WithMessage("Refund token amount must be positive.");
        RuleFor(x => x.OriginalFundingSequenceNo).GreaterThan(0);
        RuleFor(x => x.CustomerRefundedAmountCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SurchargeAmountCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SaaSShareAmountCents).GreaterThanOrEqualTo(0);
    }
}

/// <summary>Validator for <see cref="FreezeWalletCommand"/>.</summary>
public sealed class FreezeWalletCommandValidator : AbstractValidator<FreezeWalletCommand>
{
    /// <summary>Configure the validation rules.</summary>
    public FreezeWalletCommandValidator()
    {
        RuleFor(x => x.WalletId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(512);
    }
}

/// <summary>Validator for <see cref="UnfreezeWalletCommand"/>.</summary>
public sealed class UnfreezeWalletCommandValidator : AbstractValidator<UnfreezeWalletCommand>
{
    /// <summary>Configure the validation rules.</summary>
    public UnfreezeWalletCommandValidator()
    {
        RuleFor(x => x.WalletId.Value).NotEqual(Guid.Empty);
    }
}

/// <summary>Validator for <see cref="CloseWalletCommand"/>.</summary>
public sealed class CloseWalletCommandValidator : AbstractValidator<CloseWalletCommand>
{
    /// <summary>Configure the validation rules.</summary>
    public CloseWalletCommandValidator()
    {
        RuleFor(x => x.WalletId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(512);
    }
}
