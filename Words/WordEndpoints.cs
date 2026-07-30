using System.Text;
using Microsoft.AspNetCore.Http.Features;
using WordSearch.Api.Attestation;

namespace WordSearch.Api.Words;

/// <summary>The endpoint the game calls to fill a grid.</summary>
public static class WordEndpoints
{
    /// <summary>Maps <c>POST /v1/words</c>.</summary>
    public static IEndpointRouteBuilder MapWordEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/words", HandleAsync)
            .AddEndpointFilter<AttestationEndpointFilter>()
            .RequireRateLimiting(RateLimitPolicies.PerDevice)
            .Accepts<string>("text/plain")
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .WithName("GenerateWordList")
            .WithSummary("Generates a word list for a topic sent as plain text.");

        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext http,
        IWordListGenerator generator,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(WordEndpoints));

        var body = await ReadTopicBodyAsync(http, cancellationToken);
        if (body is null)
        {
            return Results.Problem(
                title: "Topic too large",
                detail: $"The topic body must be at most {TopicValidator.MaxBodyBytes} bytes.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        if (!TopicValidator.TryNormalize(body, out var topic, out var error))
        {
            return Results.Problem(
                title: "Invalid topic",
                detail: error,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var client = AttestationEndpointFilter.GetClient(http);
        logger.LogInformation(
            "Generating a word list for a {Length} character topic from {Platform} device {DeviceId}.",
            TopicValidator.VisibleLength(topic),
            client?.Platform,
            client?.DeviceId);

        var result = await generator.GenerateAsync(topic, cancellationToken);
        switch (result.Status)
        {
            case WordListStatus.Success:
                // Straight through: the client gets exactly what the model wrote.
                return Results.Text(result.Json!, "application/json", Encoding.UTF8);

            case WordListStatus.InvalidResponse:
                logger.LogWarning("Unusable model response: {Diagnostic}", result.Diagnostic);
                return Results.Problem(
                    title: "Word list unavailable",
                    detail: "The word list could not be generated. Try a different topic.",
                    statusCode: StatusCodes.Status502BadGateway);

            default:
                logger.LogError("Word list generation failed: {Diagnostic}", result.Diagnostic);
                return Results.Problem(
                    title: "Word list unavailable",
                    detail: "The word list service is not responding. Try again shortly.",
                    statusCode: StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>
    /// Reads the plain text body, or returns <c>null</c> when the client sent
    /// more than the endpoint accepts.
    /// </summary>
    private static async Task<string?> ReadTopicBodyAsync(
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var sizeLimit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeLimit is { IsReadOnly: false })
        {
            sizeLimit.MaxRequestBodySize = TopicValidator.MaxBodyBytes;
        }

        if (http.Request.ContentLength > TopicValidator.MaxBodyBytes)
        {
            return null;
        }

        try
        {
            using var reader = new StreamReader(
                http.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (BadHttpRequestException)
        {
            return null;
        }
    }
}