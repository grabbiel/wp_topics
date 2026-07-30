using System.Collections.Concurrent;

namespace WordSearch.Api.Attestation;

/// <summary>
/// Keeps registered keys in memory.
/// </summary>
/// <remarks>
/// Every device has to register again after a restart or a scale event, so
/// this needs to become a real store (Cosmos DB, PostgreSQL, Redis) before the
/// API leaves the prototype stage.
/// </remarks>
public sealed class InMemoryAttestedKeyStore : IAttestedKeyStore
{
    private readonly ConcurrentDictionary<string, AttestedKey> _keys = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<AttestedKey?> FindAsync(string keyId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_keys.GetValueOrDefault(keyId));

    /// <inheritdoc />
    public ValueTask SaveAsync(AttestedKey key, CancellationToken cancellationToken)
    {
        _keys[key.KeyId] = key;
        return ValueTask.CompletedTask;
    }
}