using FluentValidation;
using MediatR;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Commands;

namespace PolarSharp.PrepaidWallets.Behaviors;

/// <summary>
/// MediatR behavior that runs FluentValidation validators registered for the command and returns
/// a typed <see cref="CommandError.ValidationFailed"/> outcome on failure — without throwing.
/// </summary>
/// <remarks>
/// Per the Case Study 02 pipeline ("Validation behavior — FluentValidation on DTO"), validation
/// failures are an expected domain outcome and surface as a <see cref="WalletCommandResult"/>
/// failure, not an exception. Tests therefore never need to hook around exception flow to assert
/// validation behavior.
/// </remarks>
/// <typeparam name="TRequest">The command type.</typeparam>
public sealed class ValidationBehavior<TRequest>
    : IPipelineBehavior<TRequest, WalletCommandResult>
    where TRequest : IWalletCommand<WalletCommandResult>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>Construct the behavior.</summary>
    /// <param name="validators">Registered validators for <typeparamref name="TRequest"/>.</param>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        _validators = validators;
    }

    /// <inheritdoc/>
    public async Task<WalletCommandResult> Handle(
        TRequest request,
        RequestHandlerDelegate<WalletCommandResult> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var ctx = new ValidationContext<TRequest>(request);
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(ctx, cancellationToken).ConfigureAwait(false);
            if (!result.IsValid)
            {
                var detail = string.Join("; ", result.Errors.Select(e => e.ErrorMessage));
                return new WalletCommandResult(
                    Result<CommandSuccess, CommandError>.Failure(new CommandError.ValidationFailed(detail)));
            }
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
