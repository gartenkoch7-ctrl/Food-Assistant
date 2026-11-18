using FoodAssistant.UI.Common;
using FoodAssistant.UI.Composition;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

// Thin host: everything the app does lives in FoodAssistant.UI.
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddFoodAssistantUi();

var host = builder.Build();

// The language must be set before the first component renders, or the first frame shows the wrong texts.
var cultures = host.Services.GetRequiredService<ICultureService>();
cultures.Apply(await cultures.ResolveStoredAsync().ConfigureAwait(false));

await host.RunAsync().ConfigureAwait(false);
