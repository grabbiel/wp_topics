namespace WordSearch.Api.Words;

/// <summary>How a word list request turned out.</summary>
public enum WordListStatus
{
    /// <summary>The model answered with usable JSON.</summary>
    Success,

    /// <summary>The model could not be reached or refused the request.</summary>
    Unavailable,

    /// <summary>The model answered with something this API will not forward.</summary>
    InvalidResponse,
}

/// <summary>The outcome of one word list request.</summary>
/// <param name="Status">Whether the call worked.</param>
/// <param name="Json">The model's JSON, byte for byte, when it worked.</param>
/// <param name="Diagnostic">A log-only note about what went wrong.</param>
public sealed record WordListResult(WordListStatus Status, string? Json, string? Diagnostic)
{
    /// <summary>Wraps a usable response.</summary>
    public static WordListResult Success(string json) => new(WordListStatus.Success, json, null);

    /// <summary>Reports that the model could not be used.</summary>
    public static WordListResult Unavailable(string diagnostic) =>
        new(WordListStatus.Unavailable, null, diagnostic);

    /// <summary>Reports that the model's answer was unusable.</summary>
    public static WordListResult Invalid(string diagnostic) =>
        new(WordListStatus.InvalidResponse, null, diagnostic);
}

/// <summary>Turns a topic into a list of words for the game.</summary>
public interface IWordListGenerator
{
    /// <summary>Generates words for <paramref name="topic"/>.</summary>
    Task<WordListResult> GenerateAsync(string topic, CancellationToken cancellationToken);
}
