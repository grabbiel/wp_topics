using WordSearch.Api.Options;
using Microsoft.Extensions.Options;

namespace WordSearch.Api.Attestation;

/// <summary>
/// Rejects any request that cannot prove it came from the mobile app.
/// </summary>
/// <remarks>
/// Services are resolved per request rather than injected, because an endpoint
/// filter instance is built once when routes are mapped and would otherwise
/// capture scoped services.
/// </remarks>
public sealed class AttestationEndpointFilter : IEndpointFilter
{
    /// <summary>The key the verified caller is stashed under on the request.</summary>
    public const string ClientItemKey = "attested-client";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var services = http.RequestServices;
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger<AttestationEndpointFilter>();

        var attestation = services.GetRequiredService<IOptions<AttestationOptions>>().Value;
        var environment = services.GetRequiredService<IHostEnvironment>();
        if (attestation.BypassForDevelopment && environment.IsDevelopment())
        {
            logger.LogWarning("Attestation bypassed: development environment.");
            http.Items[ClientItemKey] = new AttestedClient(ClientPlatform.Android, "development");
            return await next(context);
        }

        if (!ClientPlatformParser.TryParse(http.Request.Headers[AttestationHeaders.Platform], out var platform))
        {
            logger.LogInformation("Rejected: missing or unknown {Header}.", AttestationHeaders.Platform);
            return Rejected();
        }

        var verifier = services.GetServices<IClientAttestationVerifier>()
            .FirstOrDefault(candidate => candidate.Platform == platform);
        if (verifier is null)
        {
            logger.LogError("No verifier is registered for {Platform}.", platform);
            return Rejected();
        }

        var result = await verifier.VerifyAsync(http.Request, http.RequestAborted);
        if (!result.Succeeded)
        {
            logger.LogInformation(
                "Rejected {Platform} client: {Reason}",
                platform,
                result.FailureReason);
            return Rejected();
        }

        http.Items[ClientItemKey] = result.Client;
        return await next(context);
    }

    /// <summary>Reads the caller a filter attached to this request.</summary>
    public static AttestedClient? GetClient(HttpContext context) =>
        context.Items.TryGetValue(ClientItemKey, out var value) ? value as AttestedClient : null;

    // One shape for every failure: which check tripped is log-only detail.
    private static IResult Rejected() => Results.Problem(
        title: "Client attestation failed",
        detail: "The request could not be verified as coming from the app.",
        statusCode: StatusCodes.Status401Unauthorized);
}