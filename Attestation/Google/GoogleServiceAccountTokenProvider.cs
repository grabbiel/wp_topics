using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WordSearch.Api.Options;

namespace WordSearch.Api.Attestation.Google;

/// <summary>
/// Exchanges a Google service account key for an access token scoped to the
/// Play Integrity API, and caches it until shortly before it expires.
/// </summary>
/// <remarks>
/// This is the JWT bearer flow from Google's OAuth documentation, written out
/// here so the prototype needs no Google client library.
/// </remarks>
public sealed class GoogleServiceAccountTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<PlayIntegrityOptions> options,
    TimeProvider clock)
{
    /// <summary>The named <see cref="HttpClient"/> used for the token exchange.</summary>
    public const string HttpClientName = "google-oauth";

    private const string Scope = "https://www.googleapis.com/auth/playintegrity";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(55);
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly PlayIntegrityOptions _options = options.Value;
    private ServiceAccountKey? _key;
    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    /// <summary>Returns a valid access token, minting a new one when needed.</summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && clock.GetUtcNow() + RefreshMargin < _expiresAt)
        {
            return _accessToken;
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && clock.GetUtcNow() + RefreshMargin < _expiresAt)
            {
                return _accessToken;
            }

            var key = _key ??= LoadServiceAccountKey();
            var assertion = CreateAssertion(key);

            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion,
            });

            using var response = await client.PostAsync(key.TokenUri, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Google rejected the token request with {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Google returned an empty token response.");

            _accessToken = payload.AccessToken;
            _expiresAt = clock.GetUtcNow().AddSeconds(payload.ExpiresIn);
            return _accessToken;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private string CreateAssertion(ServiceAccountKey key)
    {
        var issuedAt = clock.GetUtcNow();
        var header = new { alg = "RS256", typ = "JWT" };
        var claims = new Dictionary<string, object>
        {
            ["iss"] = key.ClientEmail,
            ["scope"] = Scope,
            ["aud"] = key.TokenUri,
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = (issuedAt + TokenLifetime).ToUnixTimeSeconds(),
        };

        var signingInput =
            $"{Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(header))}." +
            $"{Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(claims))}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(key.PrivateKey);
        var signature = rsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64Url.Encode(signature)}";
    }

    private ServiceAccountKey LoadServiceAccountKey()
    {
        var json = _options.ServiceAccountJson;
        if (string.IsNullOrWhiteSpace(json) && !string.IsNullOrWhiteSpace(_options.ServiceAccountJsonPath))
        {
            json = File.ReadAllText(_options.ServiceAccountJsonPath);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                "Set PlayIntegrity:ServiceAccountJson or PlayIntegrity:ServiceAccountJsonPath.");
        }

        return JsonSerializer.Deserialize<ServiceAccountKey>(json)
            ?? throw new InvalidOperationException("The service account key could not be read.");
    }

    private sealed record ServiceAccountKey
    {
        [JsonPropertyName("client_email")]
        public string ClientEmail { get; init; } = string.Empty;

        [JsonPropertyName("private_key")]
        public string PrivateKey { get; init; } = string.Empty;

        [JsonPropertyName("token_uri")]
        public string TokenUri { get; init; } = "https://oauth2.googleapis.com/token";
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}