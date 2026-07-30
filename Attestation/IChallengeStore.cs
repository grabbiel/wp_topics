namespace WordSearch.Api.Attestation;

/// <summary>
/// Issues and redeems the one-time challenges that stop a captured
/// attestation token from being replayed.
/// </summary>
public interface IChallengeStore
{
    /// <summary>Issues a fresh challenge as a base64url string.</summary>
    string Issue();

    /// <summary>
    /// Redeems <paramref name="challenge"/>. Returns <c>false</c> when it is
    /// unknown, expired, or already spent.
    /// </summary>
    bool TryConsume(string challenge);
}