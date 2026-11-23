using System.Text.Json;
using Microsoft.JSInterop;

namespace Pakar.Web.Services;

/// <summary>
/// Holds the signed-in user for one browser tab. The session is kept in the browser's session storage,
/// so it survives page reloads and is cleared when the tab closes or the user logs out.
/// The token is readable by scripts of this origin; that is the trade-off of running without a server.
/// </summary>
public sealed class UserSession(IJSRuntime js, TimeProvider clock)
{
    private const string SessionKey = "pakar-auth-session";

    private SignedInUser? user;
    private bool restored;
    private readonly SemaphoreSlim restoreGate = new(1, 1);

    /// <summary>Raised after sign-in and sign-out so menus can re-render.</summary>
    public event Action? Changed;

    public async Task SignInAsync(SignedInUser signedIn)
    {
        user = signedIn;
        restored = true;
        await js.InvokeVoidAsync("sessionStorage.setItem", SessionKey, JsonSerializer.Serialize(signedIn));
        Changed?.Invoke();
    }

    public async Task SignOutAsync()
    {
        user = null;
        restored = true;
        await js.InvokeVoidAsync("sessionStorage.removeItem", SessionKey);
        Changed?.Invoke();
    }

    /// <summary>Returns the signed-in user, or null when signed out or the token expired.</summary>
    public async ValueTask<SignedInUser?> GetUserAsync()
    {
        if (!restored)
        {
            // Components that start together must all wait for the same restore, not read a half-empty session.
            await restoreGate.WaitAsync();
            try
            {
                if (!restored)
                {
                    user = await ReadStoredUserAsync();
                    restored = true;
                }
            }
            finally
            {
                restoreGate.Release();
            }
        }

        if (user is not null && user.IsExpired(clock.GetUtcNow().UtcDateTime))
            await SignOutAsync();

        return user;
    }

    /// <summary>Bearer token for API calls, or null when signed out.</summary>
    public async ValueTask<string?> GetTokenAsync() => (await GetUserAsync())?.Token;

    private async Task<SignedInUser?> ReadStoredUserAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("sessionStorage.getItem", SessionKey);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<SignedInUser>(json);
        }
        catch (JsonException)
        {
            // A damaged entry is treated as signed out.
            await js.InvokeVoidAsync("sessionStorage.removeItem", SessionKey);
            return null;
        }
    }
}
