using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using GrantMatcher.Client;
using GrantMatcher.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure API client
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:7071/api";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

// Register services
builder.Services.AddScoped<IApiClient, ApiClient>();
builder.Services.AddScoped<IAnalyticsClient, AnalyticsClient>();

await builder.Build().RunAsync();
