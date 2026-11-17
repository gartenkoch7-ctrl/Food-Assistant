using FoodAssistant.Application.Composition;
using FoodAssistant.Infrastructure.Composition;
using FoodAssistant.Server.Api;
using FoodAssistant.Server.Components;
using FoodAssistant.Server.Composition;

var builder = WebApplication.CreateBuilder(args);

builder.AddServerTelemetry();
builder.Services.AddApplication();
builder.AddInfrastructure();
builder.AddApi();
builder.AddAuth();
builder.AddUi();
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// No UseHttpsRedirection: TLS ends at the reverse proxy (ADR-0007).
app.UseApi();
app.UseAuth();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapApiV1();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(FoodAssistant.Web.Client.Routes).Assembly);

await app.RunAsync().ConfigureAwait(false);

/// <summary>Entry point; public so integration tests can use WebApplicationFactory.</summary>
public partial class Program;
