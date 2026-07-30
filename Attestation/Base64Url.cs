namespace WordSearch.Api.Attestation;

/// <summary>
/// Base64url with the padding stripped, which is the encoding both Google and
/// Apple use for the values exchanged here.
/// </summary>
public static class Base64Url
{
    /// <summary>Encodes <paramref name="bytes"/>.</summary>
    public static string Encode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Decodes <paramref name="value"/>, or returns <c>null</c> when it is malformed.</summary>
    public static byte[]? TryDecode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            0 => padded,
            _ => padded,
        };

        try
        {
            return Convert.FromBase64String(padded);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}