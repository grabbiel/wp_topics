using System.ComponentModel.DataAnnotations;

namespace WordSearch.Api.Options;

/// <summary>Settings for verifying Android clients with the Play Integrity API.</summary>
public sealed class PlayIntegrityOptions
{
    public const string SectionName = "PlayIntegrity";

    /// <summary>The package name the token must have been minted for.</summary>
    [Required]
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// The Google service account JSON, inline. Set it from a secret; prefer
    /// this over <see cref="ServiceAccountJsonPath"/> on Container Apps.
    /// </summary>
    public string? ServiceAccountJson { get; set; }

    /// <summary>Path to the service account JSON on disk.</summary>
    public string? ServiceAccountJsonPath { get; set; }

    /// <summary>App recognition verdicts that are treated as genuine.</summary>
    public string[] AllowedAppRecognitionVerdicts { get; set; } = ["PLAY_RECOGNIZED"];

    /// <summary>Device verdicts that must all be present.</summary>
    public string[] RequiredDeviceVerdicts { get; set; } = ["MEETS_DEVICE_INTEGRITY"];

    /// <summary>
    /// Signing certificate digests (base64url, no padding) the app may be
    /// signed with. Empty disables the check, which is fine while prototyping
    /// and not fine in production.
    /// </summary>
    public string[] AllowedCertificateSha256Digests { get; set; } = [];

    /// <summary>Requires the caller to hold a Play licence for the app.</summary>
    public bool RequireLicensedAccount { get; set; }

    /// <summary>How stale a token's timestamp may be.</summary>
    public TimeSpan MaxTokenAge { get; set; } = TimeSpan.FromMinutes(5);
}
