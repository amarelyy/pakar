using Microsoft.AspNetCore.Components;
using Pakar.Web.Services;

namespace Pakar.Web.Components.Admin;

public abstract class AdminWorkspaceBase : ComponentBase
{
    [Inject] protected ParkingApiClient Api { get; set; } = null!;
    protected bool Busy { get; private set; }
    protected ApiException? Error { get; set; }
    protected string? Success { get; set; }
    protected async Task Run(Func<Task> action, string? success = null)
    {
        if (Busy) return;
        Busy = true; Error = null; Success = null;
        try { await action(); Success = success; }
        catch (ApiException ex) { Error = ex; }
        finally { Busy = false; }
    }
}
