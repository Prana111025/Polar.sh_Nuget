using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;

namespace PolarSharp.EcommerceStorefronts.Tests.TestSupport;

/// <summary>Test stage that marks the order Completed; used to drive ProcessCheckout tests.</summary>
internal sealed class CompletingStage : IOrderProcessingStage
{
    public int Order => 0;
    public string Name => "TestCompleting";

    public async IAsyncEnumerable<OrderInProcess> ProcessAsync(
        IAsyncEnumerable<OrderInProcess> input,
        PipelineStageContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in input.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return item with
            {
                Status = CheckoutStatus.Completed,
                Outcome = PipelineOutcome.Halt,
            };
        }
    }
}

/// <summary>Test stage that marks the order Failed.</summary>
internal sealed class FailingStage : IOrderProcessingStage
{
    public int Order => 0;
    public string Name => "TestFailing";

    public async IAsyncEnumerable<OrderInProcess> ProcessAsync(
        IAsyncEnumerable<OrderInProcess> input,
        PipelineStageContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in input.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return item with
            {
                Status = CheckoutStatus.PaymentCaptured,
                Outcome = PipelineOutcome.Failed,
                FailureReasonKey = PolarSharp.EcommerceStorefronts.Abstractions.StorefrontOption<string>.Some("Test.Failure"),
            };
        }
    }
}
