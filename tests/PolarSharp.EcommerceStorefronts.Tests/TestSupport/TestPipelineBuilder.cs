using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;
using PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing;

namespace PolarSharp.EcommerceStorefronts.Tests.TestSupport;

internal static class TestPipelineBuilder
{
    public static OrderProcessingPipeline Build(params IOrderProcessingStage[] stages) =>
        new(stages, NullLogger<OrderProcessingPipeline>.Instance);
}
