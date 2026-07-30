using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Options;
using WordSearch.Api.Options;

namespace WordSearch.Api.Attestation.Apple;

/// <summary>
/// Validates the attestation object a device produces on first launch and
/// stores the public key that later assertions are checked against.
/// </summary>
/// <remarks>
/// The steps follow Apple's "Validating Apps That Connect to Your Server".
/// </remarks>
public sealed class AppAttestRegistrationService(
    IAttestedKeyStore keyStore,
    AppleRootCertificateProvider rootCertificates,
    IOptions<AppAttestOptions> options,
    TimeProvider clock)
{
    /// <summary>The certificate extension carrying the attestation nonce.</summary>
    private const string NonceExtensionOid = "1.2.840.113635.100.8.2";

    private readonly AppAttestOptions _options = options.Value;

    /// <summary>
    /// Verifies <paramref name="attestationObject"/> and registers the key.
    /// </summary>
    /// <param name="keyId">The base64 key identifier from the device.</param>
    /// <param name="attestationObject">The raw CBOR attestation object.</param>
    /// <param name="challenge">The challenge the device attested against.</param>
    /// <param name="cancellationToken">Cancels the store write.</param>
    public async Task<AttestationResult> RegisterAsync(
        string keyId,
        byte[] attestationObject,
        string challenge,
        CancellationToken cancellationToken)
    {
        var keyIdBytes = Base64Url.TryDecode(keyId) ?? TryDecodeStandardBase64(keyId);
        if (keyIdBytes is not { Length: 32 })
        {
            return AttestationResult.Fail("The key identifier is not a 32 byte value.");
        }

        var attestation = AppAttestCbor.ReadAttestationObject(attestationObject);
        if (attestation is null || attestation.Format != "apple-appattest")
        {
            return AttestationResult.Fail("The attestation object could not be read.");
        }

        using var credentialCertificate = X509CertificateLoader.LoadCertificate(attestation.CertificateChain[0]);
        if (!IsChainTrusted(attestation.CertificateChain, credentialCertificate))
        {
            return AttestationResult.Fail("The certificate chain does not lead to the Apple root.");
        }

        var clientDataHash = AppAttestClientData.Hash(challenge);
        byte[] noncePreimage = [.. attestation.AuthenticatorData, .. clientDataHash];
        var expectedNonce = SHA256.HashData(noncePreimage);
        var certificateNonce = ReadNonceExtension(credentialCertificate);
        if (certificateNonce is null || !CryptographicOperations.FixedTimeEquals(certificateNonce, expectedNonce))
        {
            return AttestationResult.Fail("The attestation nonce does not match the challenge.");
        }

        using var publicKey = credentialCertificate.GetECDsaPublicKey();
        if (publicKey is null)
        {
            return AttestationResult.Fail("The credential certificate carries no EC public key.");
        }

        if (!CryptographicOperations.FixedTimeEquals(KeyIdentifierOf(publicKey), keyIdBytes))
        {
            return AttestationResult.Fail("The key identifier does not match the attested public key.");
        }

        var authenticatorData = AuthenticatorData.Parse(attestation.AuthenticatorData, expectAttestedCredentialData: true);
        if (authenticatorData is null)
        {
            return AttestationResult.Fail("The authenticator data could not be read.");
        }

        var expectedAppIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(_options.AppId));
        if (!CryptographicOperations.FixedTimeEquals(authenticatorData.RelyingPartyIdHash, expectedAppIdHash))
        {
            return AttestationResult.Fail("The attestation is bound to a different App ID.");
        }

        if (authenticatorData.SignCount != 0)
        {
            return AttestationResult.Fail("A fresh attestation must have a zero counter.");
        }

        if (!IsAaguidAccepted(attestation.AuthenticatorData))
        {
            return AttestationResult.Fail("The attestation came from an environment that is not accepted.");
        }

        if (authenticatorData.CredentialId is null ||
            !CryptographicOperations.FixedTimeEquals(authenticatorData.CredentialId, keyIdBytes))
        {
            return AttestationResult.Fail("The credential identifier does not match the key identifier.");
        }

        await keyStore.SaveAsync(
            new AttestedKey(
                KeyId: keyId,
                PublicKey: publicKey.ExportSubjectPublicKeyInfo(),
                SignCount: 0,
                RegisteredAt: clock.GetUtcNow()),
            cancellationToken);

        return AttestationResult.Success(new AttestedClient(ClientPlatform.Ios, keyId));
    }

    private bool IsChainTrusted(IReadOnlyList<byte[]> chain, X509Certificate2 leaf)
    {
        using var validator = new X509Chain();
        validator.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        validator.ChainPolicy.CustomTrustStore.Add(rootCertificates.Certificate);
        validator.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        validator.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

        var intermediates = new List<X509Certificate2>();
        try
        {
            foreach (var encoded in chain.Skip(1))
            {
                var certificate = X509CertificateLoader.LoadCertificate(encoded);
                intermediates.Add(certificate);
                validator.ChainPolicy.ExtraStore.Add(certificate);
            }

            return validator.Build(leaf);
        }
        finally
        {
            foreach (var certificate in intermediates)
            {
                certificate.Dispose();
            }
        }
    }

    private bool IsAaguidAccepted(byte[] rawAuthenticatorData)
    {
        var aaguid = AuthenticatorData.ReadAaguid(rawAuthenticatorData);
        if (aaguid.SequenceEqual(AuthenticatorData.ProductionAaguid))
        {
            return true;
        }

        return _options.AllowDevelopmentEnvironment &&
               aaguid.SequenceEqual(AuthenticatorData.DevelopmentAaguid);
    }

    private static byte[]? ReadNonceExtension(X509Certificate2 certificate)
    {
        var extension = certificate.Extensions
            .FirstOrDefault(candidate => candidate.Oid?.Value == NonceExtensionOid);
        if (extension is null)
        {
            return null;
        }

        try
        {
            // SEQUENCE { [1] { OCTET STRING nonce } }
            var outer = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
            var tagged = outer.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 1));
            return tagged.ReadOctetString();
        }
        catch (AsnContentException)
        {
            return null;
        }
    }

    private static byte[] KeyIdentifierOf(ECDsa publicKey)
    {
        var parameters = publicKey.ExportParameters(includePrivateParameters: false);
        var x = parameters.Q.X ?? [];
        var y = parameters.Q.Y ?? [];

        // Apple hashes the uncompressed point: 0x04 || X || Y.
        var uncompressed = new byte[1 + x.Length + y.Length];
        uncompressed[0] = 0x04;
        x.CopyTo(uncompressed, 1);
        y.CopyTo(uncompressed, 1 + x.Length);
        return SHA256.HashData(uncompressed);
    }

    private static byte[]? TryDecodeStandardBase64(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}