using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WordSearch.Api.Attestation.Apple;
using WordSearch.Api.Options;

namespace WordSearch.Api.Attestation;

/// <summary>What a client sends to register an App Attest key.</summary>
/// <param name="KeyId">The base64 key identifier from <c>generateKey</c>.</param>
/// <param name="Attestation">The base64 CBOR attestation object.</param>
/// <param name="Challenge">The challenge the attestation was built against.</param>
public sealed record RegisterAppleKeyRequest(
    [property: JsonPropertyName("keyId")] string KeyId,
    [property: JsonPropertyName("attestation")] string Attestation,
    [property: JsonPropertyName("challenge")] string Challenge);

/// <summary>The challenge handed to a client before it attests.</summary>
/// <param name="Challenge">A single-use random value.</param>
/// <param name="ExpiresInSeconds">How long it stays valid.</param>
public sealed record ChallengeResponse(
    [property: JsonPropertyName("challenge")] string Challenge,
    [property: JsonPropertyName("expiresInSeconds")] int ExpiresInSeconds);

/// <summary>The endpoints a client uses before it can call the API proper.</summary>
public static class AttestationEndpoints
{
    /// <summary>Maps the challenge and key registration endpoints.</summary>
    public static IEndpointRouteBuilder MapAttestationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/attestation")
            .RequireRateLimiting(RateLimitPolicies.PerDevice);

        group.MapPost("/challenge", (
                IChallengeStore challenges,
                IOptions<AttestationOptions> options) =>
            {
                var challenge = challenges.Issue();
                return Results.Ok(new ChallengeResponse(
                    challenge,
                    (int)options.Value.ChallengeLifetime.TotalSeconds));
            })
            .WithName("IssueChallenge")
            .WithSummary("Issues the single-use challenge that an attestation must cover.");

        group.MapPost("/apple/keys", async (
                RegisterAppleKeyRequest request,
                AppAttestRegistrationService registration,
                IChallengeStore challenges,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                var logger = loggerFactory.CreateLogger(typeof(AttestationEndpoints));

                var attestation = Base64Url.TryDecode(request.Attestation);
                if (attestation is null)
                {
                    return Results.Problem(
                        title: "Invalid attestation",
                        detail: "The attestation object is not valid base64.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (!challenges.TryConsume(request.Challenge))
                {
                    logger.LogInformation("Key registration rejected: the challenge was not valid.");
                    return Results.Problem(
                        title: "Key registration failed",
                        detail: "The attestation could not be verified.",
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                var result = await registration.RegisterAsync(
                    request.KeyId,
                    attestation,
                    request.Challenge,
                    cancellationToken);

                if (!result.Succeeded)
                {
                    logger.LogInformation("Key registration rejected: {Reason}", result.FailureReason);
                    return Results.Problem(
                        title: "Key registration failed",
                        detail: "The attestation could not be verified.",
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                logger.LogInformation("Registered App Attest key {KeyId}.", result.Client!.DeviceId);
                return Results.NoContent();
            })
            .WithName("RegisterAppleKey")
            .WithSummary("Registers the App Attest key an iOS device generated on first launch.");

        return app;
    }
}