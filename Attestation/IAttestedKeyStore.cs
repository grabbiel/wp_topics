namespace WordSearch.Api.Attestation;

/// <summary>An App Attest key that a device registered with the API.</summary>
/// <param name="KeyId">The base64 key identifier Apple issued.</param>
/// <param name="PublicKey">The public key in SubjectPublicKeyInfo (DER) form.</param>
/// <param name="SignCount">The highest counter value seen in an assertion.</param>
/// <param name="RegisteredAt">When the key was attested.</param>
public sealed record AttestedKey(
    string KeyId,
    byte[] PublicKey,
    uint SignCount,
    DateTimeOffset RegisteredAt);

/// <summary>Stores the public keys devices registered through App Attest.</summary>
public interface IAttestedKeyStore
{
    /// <summary>Looks up a key, or returns <c>null</c> when it was never registered.</summary>
    ValueTask<AttestedKey?> FindAsync(string keyId, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces a key.</summary>
    ValueTask SaveAsync(AttestedKey key, CancellationToken cancellationToken);
}