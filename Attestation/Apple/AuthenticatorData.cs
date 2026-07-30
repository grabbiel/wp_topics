using System.Buffers.Binary;

namespace WordSearch.Api.Attestation.Apple;

/// <summary>
/// The authenticator data block that Apple puts in both attestations and
/// assertions. The layout is the WebAuthn one.
/// </summary>
/// <param name="RelyingPartyIdHash">SHA-256 of the App ID.</param>
/// <param name="Flags">WebAuthn flag byte.</param>
/// <param name="SignCount">The counter that must increase on every assertion.</param>
/// <param name="CredentialId">The attested key identifier, present only in attestations.</param>
public sealed record AuthenticatorData(
    byte[] RelyingPartyIdHash,
    byte Flags,
    uint SignCount,
    byte[]? CredentialId)
{
    private const int RelyingPartyIdHashLength = 32;
    private const int FlagsOffset = 32;
    private const int SignCountOffset = 33;
    private const int AttestedCredentialDataOffset = 37;
    private const int AaguidLength = 16;

    /// <summary>Parses <paramref name="data"/>, returning <c>null</c> when it is malformed.</summary>
    /// <param name="expectAttestedCredentialData">
    /// True for an attestation, where the aaguid and credential identifier follow the counter.
    /// </param>
    public static AuthenticatorData? Parse(ReadOnlySpan<byte> data, bool expectAttestedCredentialData)
    {
        if (data.Length < AttestedCredentialDataOffset)
        {
            return null;
        }

        var relyingPartyIdHash = data[..RelyingPartyIdHashLength].ToArray();
        var flags = data[FlagsOffset];
        var signCount = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(SignCountOffset, sizeof(uint)));

        if (!expectAttestedCredentialData)
        {
            return new AuthenticatorData(relyingPartyIdHash, flags, signCount, null);
        }

        var credentialLengthOffset = AttestedCredentialDataOffset + AaguidLength;
        if (data.Length < credentialLengthOffset + sizeof(ushort))
        {
            return null;
        }

        var credentialIdLength = BinaryPrimitives.ReadUInt16BigEndian(
            data.Slice(credentialLengthOffset, sizeof(ushort)));
        var credentialIdOffset = credentialLengthOffset + sizeof(ushort);
        if (data.Length < credentialIdOffset + credentialIdLength)
        {
            return null;
        }

        var credentialId = data.Slice(credentialIdOffset, credentialIdLength).ToArray();
        return new AuthenticatorData(relyingPartyIdHash, flags, signCount, credentialId);
    }

    /// <summary>The environment marker Apple writes into the aaguid field.</summary>
    public static ReadOnlySpan<byte> ProductionAaguid => "appattest\0\0\0\0\0\0\0"u8;

    /// <summary>The aaguid used by the App Attest sandbox.</summary>
    public static ReadOnlySpan<byte> DevelopmentAaguid => "appattestdevelop"u8;

    /// <summary>Reads the aaguid out of a raw attestation authenticator data block.</summary>
    public static ReadOnlySpan<byte> ReadAaguid(ReadOnlySpan<byte> data) =>
        data.Length < AttestedCredentialDataOffset + AaguidLength
            ? default
            : data.Slice(AttestedCredentialDataOffset, AaguidLength);
}