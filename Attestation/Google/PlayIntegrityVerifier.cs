using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WordSearch.Api.Options;

namespace WordSearch.Api.Attestation.Google;

/// <summary>
/// Verifies an Android client by handing its integrity token to Google and
/// reading the verdicts that come back.
/// </summary>
public sealed class PlayIntegrityVerifier(
    IHttpClientFactory httpClientFactory,
    GoogleServiceAccountTokenProvider tokenProvider,
    IChallengeStore challenges,
    IOptions<PlayIntegrityOptions> options,
    TimeProvider clock,
    ILogger<PlayIntegrityVerifier> logger) : IClientAttestationVerifier
{
    /// <summary>The named <see cref="HttpClient"/> used to call Google.</summary>
    public const string HttpClientName = "play-integrity";

    private const string ApiRoot = "https://playintegrity.googleapis.com/v1/";
    private readonly PlayIntegrityOptions _options = options.Value;

    /// <inheritdoc />
    public ClientPlatform Platform => ClientPlatform.Android;

    /// <inheritdoc />
    public async Task<AttestationResult> VerifyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var token = request.Headers[AttestationHeaders.IntegrityToken].ToString();
        var challenge = request.Headers[AttestationHeaders.Challenge].ToString();

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(challenge))
        {
            return AttestationResult.Fail("An integrity header is missing.");
        }

        if (!challenges.TryConsume(challenge))
        {
            return AttestationResult.Fail("The challenge is unknown, expired, or already used.");
        }

        TokenPayload? payload;
        try
        {
            payload = await DecodeAsync(token, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the Play Integrity API.");
            return AttestationResult.Fail("The Play Integrity API could not be reached.");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Could not decode the integrity token.");
            return AttestationResult.Fail("The integrity token could not be decoded.");
        }

        if (payload is null)
        {
            return AttestationResult.Fail("Google returned no verdicts.");
        }

        return Evaluate(payload, challenge, token);
    }

    private async Task<TokenPayload?> DecodeAsync(string integrityToken, CancellationToken cancellationToken)
    {
        var accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken);

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"{ApiRoot}{Uri.EscapeDataString(_options.PackageName)}:decodeIntegrityToken")
        {
            Content = JsonContent.Create(new { integrity_token = integrityToken }),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"decodeIntegrityToken returned {(int)response.StatusCode}.");
        }

        var decoded = await response.Content
            .ReadFromJsonAsync<DecodeIntegrityTokenResponse>(cancellationToken);
        return decoded?.TokenPayloadExternal;
    }

    private AttestationResult Evaluate(TokenPayload payload, string challenge, string integrityToken)
    {
        var details = payload.RequestDetails;
        if (details is null)
        {
            return AttestationResult.Fail("The token carries no request details.");
        }

        if (!string.Equals(details.RequestPackageName, _options.PackageName, StringComparison.Ordinal))
        {
            return AttestationResult.Fail("The token was minted for a different package.");
        }

        if (!string.Equals(details.Nonce, challenge, StringComparison.Ordinal))
        {
            return AttestationResult.Fail("The nonce does not match the issued challenge.");
        }

        if (!IsFresh(details.TimestampMillis))
        {
            return AttestationResult.Fail("The token is older than the accepted window.");
        }

        var appIntegrity = payload.AppIntegrity;
        if (appIntegrity?.AppRecognitionVerdict is not { } appVerdict ||
            !_options.AllowedAppRecognitionVerdicts.Contains(appVerdict, StringComparer.Ordinal))
        {
            return AttestationResult.Fail("The app recognition verdict is not accepted.");
        }

        if (_options.AllowedCertificateSha256Digests.Length > 0)
        {
            var digests = appIntegrity.CertificateSha256Digest ?? [];
            if (!digests.Intersect(_options.AllowedCertificateSha256Digests, StringComparer.Ordinal).Any())
            {
                return AttestationResult.Fail("The app is signed with an unexpected certificate.");
            }
        }

        var deviceVerdicts = payload.DeviceIntegrity?.DeviceRecognitionVerdict ?? [];
        var missing = _options.RequiredDeviceVerdicts
            .Except(deviceVerdicts, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
        {
            return AttestationResult.Fail($"Missing device verdicts: {string.Join(", ", missing)}.");
        }

        if (_options.RequireLicensedAccount &&
            !string.Equals(payload.AccountDetails?.AppLicensingVerdict, "LICENSED", StringComparison.Ordinal))
        {
            return AttestationResult.Fail("The account does not hold a licence for the app.");
        }

        return AttestationResult.Success(new AttestedClient(ClientPlatform.Android, DeviceIdOf(integrityToken)));
    }

    private bool IsFresh(string? timestampMillis)
    {
        if (!long.TryParse(timestampMillis, out var milliseconds))
        {
            return false;
        }

        var age = clock.GetUtcNow() - DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        return age >= -TimeSpan.FromMinutes(1) && age <= _options.MaxTokenAge;
    }

    // Play Integrity exposes no device identifier, so requests are grouped by a
    // digest of the token purely for logging and rate limiting.
    private static string DeviceIdOf(string integrityToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(integrityToken))).ToLowerInvariant()[..16];
}