using System.Globalization;
using System.Text;

namespace WordSearch.Api.Words;

/// <summary>Checks and normalises the topic a client sends.</summary>
public static class TopicValidator
{
    /// <summary>The longest topic the API accepts.</summary>
    public const int MaxLength = 120;

    /// <summary>The largest request body accepted for a topic, in bytes.</summary>
    /// <remarks>
    /// Generous next to <see cref="MaxLength"/> so that a topic made of
    /// multi-byte characters still fits, but small enough that nobody can
    /// stream megabytes at the endpoint.
    /// </remarks>
    public const int MaxBodyBytes = 1024;

    /// <summary>
    /// Trims and normalises <paramref name="raw"/>.
    /// </summary>
    /// <param name="raw">The body the client sent.</param>
    /// <param name="topic">The normalised topic, when the input is usable.</param>
    /// <param name="error">A message safe to return to the client, otherwise.</param>
    public static bool TryNormalize(string? raw, out string topic, out string error)
    {
        topic = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "The topic is empty.";
            return false;
        }

        // Normalising first means an accented topic is measured the same way
        // however the client happened to encode it.
        var candidate = raw.Normalize(NormalizationForm.FormC).Trim();

        if (candidate.Length > MaxLength)
        {
            error = $"The topic is longer than {MaxLength} characters.";
            return false;
        }

        foreach (var character in candidate)
        {
            if (char.IsControl(character))
            {
                error = "The topic contains control characters.";
                return false;
            }
        }

        if (candidate.All(character => !char.IsLetterOrDigit(character)))
        {
            error = "The topic contains no letters or digits.";
            return false;
        }

        topic = candidate;
        return true;
    }

    /// <summary>The number of text elements, which is what a reader would call the length.</summary>
    public static int VisibleLength(string topic) => new StringInfo(topic).LengthInTextElements;
}