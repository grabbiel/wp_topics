namespace WordSearch.Api.Attestation;

/// <summary>The headers a client uses to prove it is the real mobile app.</summary>
public static class AttestationHeaders
{
    /// <summary><c>android</c> or <c>ios</c>.</summary>
    public const string Platform = "X-Client-Platform";

    /// <summary>The challenge this call was signed against.</summary>
    public const string Challenge = "X-Attest-Challenge";

    /// <summary>Android: the Play Integrity token.</summary>
    public const string IntegrityToken = "X-Integrity-Token";

    /// <summary>iOS: the base64 key identifier returned by App Attest.</summary>
    public const string KeyId = "X-Attest-Key-Id";

    /// <summary>iOS: the base64 CBOR assertion.</summary>
    public const string Assertion = "X-Attest-Assertion";
}