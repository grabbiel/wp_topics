namespace WordSearch.Api.Attestation;

/// <summary>The caller once it has proved which app and device it is.</summary>
/// <param name="Platform">The platform that vouched for the caller.</param>
/// <param name="DeviceId">
/// A stable identifier for the device or key, used for logging and rate
/// limiting. It is not a user identity.
/// </param>
public sealed record AttestedClient(ClientPlatform Platform, string DeviceId);

/// <summary>The outcome of one attestation check.</summary>
public sealed record AttestationResult
{
    private AttestationResult(AttestedClient? client, string? failureReason)
    {
        Client = client;
        FailureReason = failureReason;
    }

    /// <summary>The verified caller, when the check passed.</summary>
    public AttestedClient? Client { get; }

    /// <summary>
    /// Why the check failed. This is written to the log and never returned to
    /// the caller: telling an attacker which check tripped helps them.
    /// </summary>
    public string? FailureReason { get; }

    /// <summary>Whether the caller is trusted.</summary>
    public bool Succeeded => Client is not null;

    /// <summary>Creates a passing result.</summary>
    public static AttestationResult Success(AttestedClient client) => new(client, null);

    /// <summary>Creates a failing result carrying <paramref name="reason"/>.</summary>
    public static AttestationResult Fail(string reason) => new(null, reason);
}