namespace WordSearch.Api.Attestation;

/// <summary>Verifies that a request came from a genuine build of the app.</summary>
public interface IClientAttestationVerifier
{
    /// <summary>The platform this verifier handles.</summary>
    ClientPlatform Platform { get; }

    /// <summary>Checks the attestation headers on <paramref name="request"/>.</summary>
    Task<AttestationResult> VerifyAsync(HttpRequest request, CancellationToken cancellationToken);
}