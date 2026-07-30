using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WordSearch.Api.Options;

namespace WordSearch.Api.Attestation;

/// <summary>
/// Keeps challenges in the replica's own memory.
/// </summary>
/// <remarks>
/// This only works while the app runs as a single replica: a client that gets
/// its challenge from replica A and calls replica B is rejected. Back it with
/// Redis before scaling out on Container Apps.
/// </remarks>
public sealed class InMemoryChallengeStore(
    IMemoryCache cache,
    IOptions<AttestationOptions> options) : IChallengeStore
{
    private const int ChallengeByteLength = 32;
    private readonly AttestationOptions _options = options.Value;

    /// <inheritdoc />
    public string Issue()
    {
        var challenge = Base64Url.Encode(RandomNumberGenerator.GetBytes(ChallengeByteLength));
        cache.Set(CacheKey(challenge), true, _options.ChallengeLifetime);
        return challenge;
    }

    /// <inheritdoc />
    public bool TryConsume(string challenge)
    {
        if (string.IsNullOrWhiteSpace(challenge))
        {
            return false;
        }

        var key = CacheKey(challenge);
        if (!cache.TryGetValue(key, out _))
        {
            return false;
        }

        cache.Remove(key);
        return true;
    }

    private static string CacheKey(string challenge) => $"attestation-challenge:{challenge}";
}