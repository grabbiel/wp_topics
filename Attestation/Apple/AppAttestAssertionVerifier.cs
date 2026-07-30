using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WordSearch.Api.Options;

namespace WordSearch.Api.Attestation.Apple;

/// <summary>
/// Verifies the per-request assertion an iOS client signs with its registered
/// App Attest key.
/// </summary>
public sealed class AppAttestAssertionVerifier(
    IAttestedKeyStore keyStore,
    IChallengeStore challenges,
    IOptions<AppAttestOptions> options) : IClientAttestationVerifier
{
    private readonly AppAttestOptions _options = options.Value;

    /// <inheritdoc />
    public ClientPlatform Platform => ClientPlatform.Ios;

    /// <inheritdoc />
    public async Task<AttestationResult> VerifyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var keyId = request.Headers[AttestationHeaders.KeyId].ToString();
        var challenge = request.Headers[AttestationHeaders.Challenge].ToString();
        var assertionHeader = request.Headers[AttestationHeaders.Assertion].ToString();

        if (string.IsNullOrWhiteSpace(keyId) ||
            string.IsNullOrWhiteSpace(challenge) ||
            string.IsNullOrWhiteSpace(assertionHeader))
        {
            return AttestationResult.Fail("An App Attest header is missing.");
        }

        var assertionBytes = Base64Url.TryDecode(assertionHeader);
        if (assertionBytes is null)
        {
            return AttestationResult.Fail("The assertion is not valid base64.");
        }

        if (!challenges.TryConsume(challenge))
        {
            return AttestationResult.Fail("The challenge is unknown, expired, or already used.");
        }

        var storedKey = await keyStore.FindAsync(keyId, cancellationToken);
        if (storedKey is null)
        {
            return AttestationResult.Fail("The key was never registered.");
        }

        var assertion = AppAttestCbor.ReadAssertion(assertionBytes);
        if (assertion is null)
        {
            return AttestationResult.Fail("The assertion could not be read.");
        }

        var authenticatorData = AuthenticatorData.Parse(
            assertion.AuthenticatorData,
            expectAttestedCredentialData: false);
        if (authenticatorData is null)
        {
            return AttestationResult.Fail("The authenticator data could not be read.");
        }

        var expectedAppIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(_options.AppId));
        if (!CryptographicOperations.FixedTimeEquals(authenticatorData.RelyingPartyIdHash, expectedAppIdHash))
        {
            return AttestationResult.Fail("The assertion is bound to a different App ID.");
        }

        // A counter that does not move forward means the assertion is a replay.
        if (authenticatorData.SignCount <= storedKey.SignCount)
        {
            return AttestationResult.Fail("The assertion counter did not advance.");
        }

        var clientDataHash = AppAttestClientData.Hash(challenge);
        byte[] signedData = [.. assertion.AuthenticatorData, .. clientDataHash];
        var nonce = SHA256.HashData(signedData);

        using var publicKey = ECDsa.Create();
        publicKey.ImportSubjectPublicKeyInfo(storedKey.PublicKey, out _);
        var isSignatureValid = publicKey.VerifyData(
            nonce,
            assertion.Signature,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
        if (!isSignatureValid)
        {
            return AttestationResult.Fail("The assertion signature is invalid.");
        }

        await keyStore.SaveAsync(
            storedKey with { SignCount = authenticatorData.SignCount },
            cancellationToken);

        return AttestationResult.Success(new AttestedClient(ClientPlatform.Ios, keyId));
    }
}