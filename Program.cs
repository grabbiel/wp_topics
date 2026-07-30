using System.ClientModel;
using System.Threading.RateLimiting;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using WordSearch.Api;
using WordSearch.Api.Attestation;
using WordSearch.Api.Attestation.Apple;
using WordSearch.Api.Attestation.Google;
using WordSearch.Api.Options;
using WordSearch.Api.Words;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<AttestationOptions>()
    .BindConfiguration(AttestationOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<PlayIntegrityOptions>()
    .BindConfiguration(PlayIntegrityOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AppAttestOptions>()
    .BindConfiguration(AppAttestOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AzureOpenAIOptions>()
    .BindConfiguration(AzureOpenAIOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Both stores are per replica. Move them to Redis or a database before the
// container app scales past one instance.
builder.Services.AddSingleton<IChallengeStore, InMemoryChallengeStore>();
builder.Services.AddSingleton<IAttestedKeyStore, InMemoryAttestedKeyStore>();

builder.Services.AddHttpClient(GoogleServiceAccountTokenProvider.HttpClientName);
builder.Services.AddHttpClient(PlayIntegrityVerifier.HttpClientName);
builder.Services.AddSingleton<GoogleServiceAccountTokenProvider>();
builder.Services.AddSingleton<AppleRootCertificateProvider>();

builder.Services.AddScoped<IClientAttestationVerifier, PlayIntegrityVerifier>();
builder.Services.AddScoped<IClientAttestationVerifier, AppAttestAssertionVerifier>();
builder.Services.AddScoped<AppAttestRegistrationService>();

builder.Services.AddSingleton(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
    var endpoint = new Uri(settings.Endpoint);

    // Managed identity is the intended path on Container Apps; the key is a
    // convenience for local runs.
    var client = string.IsNullOrWhiteSpace(settings.ApiKey)
        ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
        : new AzureOpenAIClient(endpoint, new ApiKeyCredential(settings.ApiKey));

    return client.GetChatClient(settings.DeploymentName);
});

builder.Services.AddScoped<IWordListGenerator, AzureOpenAIWordListGenerator>();

builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddPolicy(RateLimitPolicies.PerDevice, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: PartitionKeyFor(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

// A bypass switch that survives into production would quietly turn the whole
// attestation layer off, so refuse to start instead.
var attestationOptions = app.Services.GetRequiredService<IOptions<AttestationOptions>>().Value;
if (attestationOptions.BypassForDevelopment && !app.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Attestation:BypassForDevelopment is only allowed in the Development environment.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }))
    .WithName("Liveness")
    .AllowAnonymous();

app.MapGet("/readyz", () => Results.Ok(new { status = "ready" }))
    .WithName("Readiness")
    .AllowAnonymous();

app.MapAttestationEndpoints();
app.MapWordEndpoints();

app.Run();

static string PartitionKeyFor(HttpContext context)
{
    var keyId = context.Request.Headers[AttestationHeaders.KeyId].ToString();
    if (!string.IsNullOrWhiteSpace(keyId))
    {
        return $"key:{keyId}";
    }

    return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}

/// <summary>Exposed so an integration test project can boot the API.</summary>
public partial class Program;