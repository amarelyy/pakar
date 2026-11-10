using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Pakar.Web.Components;

public static class AppRenderModes
{
    /// <summary>
    /// Interactive server rendering without prerendering. Required by components that read the
    /// session from protected browser storage, which is unavailable while prerendering.
    /// </summary>
    public static readonly IComponentRenderMode InteractiveServerNoPrerender = new InteractiveServerRenderMode(prerender: false);
}
