using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Pakar.Web.Services;

/// <summary>
/// Holds the signed-in user's API token for one browser tab. The token is kept encrypted in session
/// storage, so it survives page reloads and is cleared when the tab closes.
/// </summary>
public sealed class UserSession(ProtectedSessionStorage storage)
{
    private const string TokenKey = "auth-token";

    private string? token;
    private bool restored;

    public async Task SignInAsync(string newToken)
    {
        token = newToken;
        restored = true;
        await storage.SetAsync(TokenKey, newToken);
    }

    /// <summary>Returns the stored token, or null when the user is not signed in.</summary>
    public async ValueTask<string?> GetTokenAsync()
    {
        if (restored)
            return token;

        restored = true;
        try
        {
            var result = await storage.GetAsync<string>(TokenKey);
            token = result.Success ? result.Value : null;
        }
        catch (CryptographicException)
        {
            // Encryption keys changed (for example after a redeploy); treat as signed out.
            await storage.DeleteAsync(TokenKey);
            token = null;
        }

        return token;
    }
}
