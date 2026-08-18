using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using IntelligenceKit.Dashboard;
using IntelligenceKit.Dashboard.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The dashboard talks to the IntelligenceKit server API. The base URL comes
// from wwwroot/appsettings.json ("ApiBaseUrl"), falling back to the default
// self-hosted port so a fresh clone just works.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:7099";

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<ApiClient>();

// Single shared live connection for real-time event push.
builder.Services.AddSingleton<LiveEventsClient>();

await builder.Build().RunAsync();
