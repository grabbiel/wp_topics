using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using WordSearch.Api.Options;

namespace WordSearch.Api.Attestation.Apple;

/// <summary>
/// Loads the Apple App Attest root certificate once at startup.
/// </summary>
/// <remarks>
/// The certificate is not bundled with this repository. Download it from
/// https://www.apple.com/certificateauthority/private/ and supply it as
/// <c>AppAttest:RootCertificatePem</c> or point
/// <c>AppAttest:RootCertificatePath</c> at the PEM file.
/// </remarks>
public sealed class AppleRootCertificateProvider
{
    private readonly Lazy<X509Certificate2> _certificate;

    /// <summary>Creates the provider.</summary>
    public AppleRootCertificateProvider(IOptions<AppAttestOptions> options)
    {
        var settings = options.Value;
        _certificate = new Lazy<X509Certificate2>(() => Load(settings));
    }

    /// <summary>The root certificate every attestation chain must lead back to.</summary>
    public X509Certificate2 Certificate => _certificate.Value;

    private static X509Certificate2 Load(AppAttestOptions settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.RootCertificatePem))
        {
            return X509Certificate2.CreateFromPem(settings.RootCertificatePem);
        }

        if (!string.IsNullOrWhiteSpace(settings.RootCertificatePath))
        {
            var pem = File.ReadAllText(settings.RootCertificatePath);
            return X509Certificate2.CreateFromPem(pem);
        }

        throw new InvalidOperationException(
            "Set AppAttest:RootCertificatePem or AppAttest:RootCertificatePath to the " +
            "Apple App Attest root certificate.");
    }
}