namespace WordSearch.Api.Attestation;

/// <summary>The mobile platform a request claims to come from.</summary>
public enum ClientPlatform
{
    /// <summary>Verified with the Play Integrity API.</summary>
    Android,

    /// <summary>Verified with App Attest.</summary>
    Ios,
}

/// <summary>Reads a <see cref="ClientPlatform"/> from the platform header.</summary>
public static class ClientPlatformParser
{
    /// <summary>Parses <paramref name="value"/>, ignoring case.</summary>
    public static bool TryParse(string? value, out ClientPlatform platform)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "android":
                platform = ClientPlatform.Android;
                return true;
            case "ios":
                platform = ClientPlatform.Ios;
                return true;
            default:
                platform = default;
                return false;
        }
    }
}