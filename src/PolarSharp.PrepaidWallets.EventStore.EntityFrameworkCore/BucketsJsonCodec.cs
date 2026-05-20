using System.Text.Json;
using PolarSharp.PrepaidWallets.Abstractions.Stores;
using PolarSharp.PrepaidWallets.Serialization;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore;

/// <summary>
/// AOT-safe codec for the <c>buckets_json</c> column on <see cref="WalletSnapshotRecord"/>. Uses
/// the wallet's source-generated <see cref="WalletJsonContext"/> so trim + AOT publishes stay
/// happy.
/// </summary>
public static class BucketsJsonCodec
{
    /// <summary>The JSON representation of an empty bucket list.</summary>
    public const string Empty = "[]";

    /// <summary>Serialize a bucket list to its JSON form.</summary>
    /// <param name="buckets">The buckets to serialize. Pass an empty list for "no buckets".</param>
    /// <returns>The JSON payload.</returns>
    public static string Serialize(IReadOnlyList<FundingBucketState> buckets)
    {
        ArgumentNullException.ThrowIfNull(buckets);
        if (buckets.Count == 0)
        {
            return Empty;
        }

        return JsonSerializer.Serialize(buckets, WalletJsonContext.Default.IReadOnlyListFundingBucketState);
    }

    /// <summary>Deserialize a bucket list from its JSON form.</summary>
    /// <param name="json">The JSON payload.</param>
    /// <returns>The parsed bucket list (empty for null/empty input).</returns>
    public static IReadOnlyList<FundingBucketState> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == Empty)
        {
            return Array.Empty<FundingBucketState>();
        }

        var result = JsonSerializer.Deserialize(json, WalletJsonContext.Default.IReadOnlyListFundingBucketState);
        return result ?? Array.Empty<FundingBucketState>();
    }
}
