using System.Security.Cryptography;
using System.Text;

namespace WordSearch.Api.Attestation.Apple;

/// <summary>
/// Builds the client data hash the device signs over.
/// </summary>
/// <remarks>
/// The prototype hashes the challenge on its own. A production client should
/// hash the challenge together with the request it is about to send, so an
/// assertion cannot be lifted off one call and replayed on another.
/// </remarks>
public static class AppAttestClientData
{
    /// <summary>Hashes <paramref name="challenge"/> the way the app does.</summary>
    public static byte[] Hash(string challenge) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(challenge));
}