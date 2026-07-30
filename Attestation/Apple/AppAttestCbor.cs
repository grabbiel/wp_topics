using System.Formats.Cbor;

namespace WordSearch.Api.Attestation.Apple;

/// <summary>The pieces of an App Attest attestation object.</summary>
/// <param name="Format">Always <c>apple-appattest</c>.</param>
/// <param name="CertificateChain">The credential certificate followed by the intermediate.</param>
/// <param name="AuthenticatorData">The raw authenticator data block.</param>
public sealed record AttestationObject(
    string Format,
    IReadOnlyList<byte[]> CertificateChain,
    byte[] AuthenticatorData);

/// <summary>The pieces of an App Attest assertion.</summary>
/// <param name="Signature">The ECDSA signature, DER encoded.</param>
/// <param name="AuthenticatorData">The raw authenticator data block.</param>
public sealed record AssertionObject(byte[] Signature, byte[] AuthenticatorData);

/// <summary>Reads the CBOR structures Apple hands back on the device.</summary>
public static class AppAttestCbor
{
    /// <summary>Reads an attestation object, or returns <c>null</c> when it is malformed.</summary>
    public static AttestationObject? ReadAttestationObject(byte[] cbor)
    {
        try
        {
            var reader = new CborReader(cbor, CborConformanceMode.Lax);
            string? format = null;
            byte[]? authenticatorData = null;
            List<byte[]> chain = [];

            reader.ReadStartMap();
            while (reader.PeekState() != CborReaderState.EndMap)
            {
                switch (reader.ReadTextString())
                {
                    case "fmt":
                        format = reader.ReadTextString();
                        break;
                    case "authData":
                        authenticatorData = reader.ReadByteString();
                        break;
                    case "attStmt":
                        chain = ReadStatement(reader);
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }

            reader.ReadEndMap();

            return format is null || authenticatorData is null || chain.Count == 0
                ? null
                : new AttestationObject(format, chain, authenticatorData);
        }
        catch (CborContentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Reads an assertion, or returns <c>null</c> when it is malformed.</summary>
    public static AssertionObject? ReadAssertion(byte[] cbor)
    {
        try
        {
            var reader = new CborReader(cbor, CborConformanceMode.Lax);
            byte[]? signature = null;
            byte[]? authenticatorData = null;

            reader.ReadStartMap();
            while (reader.PeekState() != CborReaderState.EndMap)
            {
                switch (reader.ReadTextString())
                {
                    case "signature":
                        signature = reader.ReadByteString();
                        break;
                    case "authenticatorData":
                        authenticatorData = reader.ReadByteString();
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }

            reader.ReadEndMap();

            return signature is null || authenticatorData is null
                ? null
                : new AssertionObject(signature, authenticatorData);
        }
        catch (CborContentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static List<byte[]> ReadStatement(CborReader reader)
    {
        List<byte[]> chain = [];
        reader.ReadStartMap();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            if (reader.ReadTextString() != "x5c")
            {
                reader.SkipValue();
                continue;
            }

            reader.ReadStartArray();
            while (reader.PeekState() != CborReaderState.EndArray)
            {
                chain.Add(reader.ReadByteString());
            }

            reader.ReadEndArray();
        }

        reader.ReadEndMap();
        return chain;
    }
}