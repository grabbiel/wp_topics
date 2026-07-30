using System.ComponentModel.DataAnnotations;

namespace WordSearch.Api.Options;

/// <summary>Settings shared by both attestation platforms.</summary>
public sealed class AttestationOptions
{
    public const string SectionName = "Attestation";

    /// <summary>How long an issued challenge stays usable.</summary>
    [Range(typeof(TimeSpan), "00:00:30", "00:30:00")]
    public TimeSpan ChallengeLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Skips attestation entirely. Only ever true for local development: the
    /// startup check refuses to let it through outside the Development
    /// environment.
    /// </summary>
    public bool BypassForDevelopment { get; set; }
}
