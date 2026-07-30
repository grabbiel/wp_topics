namespace WordSearch.Api;

/// <summary>Names of the rate limiting policies the endpoints use.</summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// A window per device key, falling back to the caller's address.
    /// </summary>
    /// <remarks>
    /// Rate limiting runs before attestation, so the partition key comes from
    /// an unverified header. That is fine for shedding load; it is not an
    /// authorisation control.
    /// </remarks>
    public const string PerDevice = "per-device";
}
