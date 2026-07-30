using System.ComponentModel.DataAnnotations;

namespace WordSearch.Api.Options;

/// <summary>Settings for the Azure OpenAI deployment that writes the word lists.</summary>
public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>The resource endpoint, for example <c>https://my-aoai.openai.azure.com</c>.</summary>
    [Required]
    [Url]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>The chat deployment name.</summary>
    [Required]
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// An API key. Leave it empty to use the container app's managed identity,
    /// which is the intended setup.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>How many words to ask for.</summary>
    [Range(4, 20)]
    public int WordCount { get; set; } = 12;

    /// <summary>How long to wait on the model before giving up.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);
}
