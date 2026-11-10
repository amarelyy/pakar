using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Pakar.Web.Services;

/// <summary>
/// Holds the signed-in user for one browser tab. The session is kept encrypted in session storage,
/// so it survives page reloads and is cleared when the tab closes or the user logs out.
/// Scoped per circuit, so every interactive component on the page sees the same user.
/// </summary>
public sealed class UserSession(ProtectedSessionStorage storage, TimeProvider clock)
{
    private const string SessionKey = "auth-session";

    private SignedInUser? user;
    private bool restored;

    /// <summary>Raised after sign-in and sign-out so menus can re-render.</summary>
    public event Action? Changed;

    public async Task SignInAsync(SignedInUser signedIn)
    {
        user = signedIn;
        restored = true;
        await storage.SetAsync(SessionKey, signedIn);
        Changed?.Invoke();
    }

    public async Task SignOutAsync()
    {
        user = null;
        restored = true;
        await storage.DeleteAsync(SessionKey);
        Changed?.Invoke();
    }

    /// <summary>
    /// Returns the signed-in user, or null when signed out or the token expired.
    /// Needs an interactive circuit: call it from components rendered without prerendering.
    /// </summary>
    public async ValueTask<SignedInUser?> GetUserAsync()
    {
        if (!restored)
        {
            restored = true;
            try
            {
                var result = await storage.GetAsync<SignedInUser>(SessionKey);
                user = result.Success ? result.Value : null;
            }
            catch (CryptographicException)
            {
                // Encryption keys changed (for example after a redeploy); treat as signed out.
                await storage.DeleteAsync(SessionKey);
                user = null;
            }
        }

        if (user is not null && user.IsExpired(clock.GetUtcNow().UtcDateTime))
            await SignOutAsync();

        return user;
    }

    /// <summary>Bearer token for API calls, or null when signed out.</summary>
    public async ValueTask<string?> GetTokenAsync() => (await GetUserAsync())?.Token;
}
