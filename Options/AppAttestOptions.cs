using System.ComponentModel.DataAnnotations;

namespace WordSearch.Api.Options;

/// <summary>Settings for verifying iOS clients with App Attest.</summary>
public sealed class AppAttestOptions
{
    public const string SectionName = "AppAttest";

    /// <summary>The Apple Developer team identifier, for example <c>A1B2C3D4E5</c>.</summary>
    [Required]
    public string TeamId { get; set; } = string.Empty;

    /// <summary>The app's bundle identifier.</summary>
    [Required]
    public string BundleId { get; set; } = string.Empty;

    /// <summary>The App ID that the attested key is bound to.</summary>
    public string AppId => $"{TeamId}.{BundleId}";

    /// <summary>The Apple App Attest root certificate, inline as PEM.</summary>
    public string? RootCertificatePem { get; set; }

    /// <summary>Path to the Apple App Attest root certificate in PEM form.</summary>
    public string? RootCertificatePath { get; set; }

    /// <summary>
    /// Accepts keys minted by the App Attest sandbox. Turn this off for
    /// builds shipped through the App Store.
    /// </summary>
    public bool AllowDevelopmentEnvironment { get; set; } = true;
}
