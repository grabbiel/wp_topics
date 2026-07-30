using System.Text.Json.Serialization;

namespace WordSearch.Api.Attestation.Google;

/// <summary>The response body of <c>decodeIntegrityToken</c>.</summary>
public sealed record DecodeIntegrityTokenResponse
{
    /// <summary>The decoded verdicts.</summary>
    [JsonPropertyName("tokenPayloadExternal")]
    public TokenPayload? TokenPayloadExternal { get; init; }
}

/// <summary>The verdicts Play returns about one request.</summary>
public sealed record TokenPayload
{
    /// <summary>What the client asked for.</summary>
    [JsonPropertyName("requestDetails")]
    public RequestDetails? RequestDetails { get; init; }

    /// <summary>Whether the app binary is the one published on Play.</summary>
    [JsonPropertyName("appIntegrity")]
    public AppIntegrity? AppIntegrity { get; init; }

    /// <summary>Whether the device looks genuine.</summary>
    [JsonPropertyName("deviceIntegrity")]
    public DeviceIntegrity? DeviceIntegrity { get; init; }

    /// <summary>Whether the account owns the app.</summary>
    [JsonPropertyName("accountDetails")]
    public AccountDetails? AccountDetails { get; init; }
}

/// <summary>The request the token was minted for.</summary>
public sealed record RequestDetails
{
    /// <summary>The package the token was requested by.</summary>
    [JsonPropertyName("requestPackageName")]
    public string? RequestPackageName { get; init; }

    /// <summary>The nonce the client supplied.</summary>
    [JsonPropertyName("nonce")]
    public string? Nonce { get; init; }

    /// <summary>When the token was minted, in milliseconds since the epoch.</summary>
    [JsonPropertyName("timestampMillis")]
    public string? TimestampMillis { get; init; }
}

/// <summary>Verdicts about the app binary.</summary>
public sealed record AppIntegrity
{
    /// <summary><c>PLAY_RECOGNIZED</c>, <c>UNRECOGNIZED_VERSION</c> or <c>UNEVALUATED</c>.</summary>
    [JsonPropertyName("appRecognitionVerdict")]
    public string? AppRecognitionVerdict { get; init; }

    /// <summary>The package the verdict is about.</summary>
    [JsonPropertyName("packageName")]
    public string? PackageName { get; init; }

    /// <summary>Base64url digests of the signing certificates.</summary>
    [JsonPropertyName("certificateSha256Digest")]
    public string[]? CertificateSha256Digest { get; init; }
}

/// <summary>Verdicts about the device.</summary>
public sealed record DeviceIntegrity
{
    /// <summary>Labels such as <c>MEETS_DEVICE_INTEGRITY</c>.</summary>
    [JsonPropertyName("deviceRecognitionVerdict")]
    public string[]? DeviceRecognitionVerdict { get; init; }
}

/// <summary>Verdicts about the Play account.</summary>
public sealed record AccountDetails
{
    /// <summary><c>LICENSED</c>, <c>UNLICENSED</c> or <c>UNEVALUATED</c>.</summary>
    [JsonPropertyName("appLicensingVerdict")]
    public string? AppLicensingVerdict { get; init; }
}