using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Pakar.Web.Components;
using Pakar.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The API lives on a separate host. Api:BaseUrl comes from wwwroot/appsettings*.json, which the
// Vercel publish script overwrites with the real address (see infra/vercel/README.md).
var apiBaseUrl = builder.Configuration["Api:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
    throw new InvalidOperationException("Api:BaseUrl is not configured in wwwroot/appsettings.json.");
if (!apiBaseUrl.EndsWith('/'))
    apiBaseUrl += "/";

builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Browser-side AI settings: where the ONNX model and the demo videos are served from.
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));

// Account registration and login (FR 7, FR 8). The whole app runs in the browser, so one instance per tab.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AuthApiClient>();
builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<AdminApiClient>();
builder.Services.AddScoped<RoiApiClient>();
builder.Services.AddScoped<FeederApiClient>();
builder.Services.AddScoped<BrowserDetector>();

await builder.Build().RunAsync();
