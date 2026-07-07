using Jonnxor.Admin.Components;
using Jonnxor.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Loopback-only posture: refuse to boot on any non-loopback bind, before Run().
// Guard the union of every source Kestrel can bind from: explicit app.Urls entries,
// the "urls" configuration key (appsettings "Urls", ASPNETCORE_URLS, a --urls
// override — all flow into it), and Kestrel:Endpoints:*:Url values, which Kestrel
// binds directly without touching the "urls" key at all.
LoopbackGuard.EnsureLoopback(
    app.Urls.Concat(LoopbackGuard.CollectConfiguredUrls(app.Configuration)));

app.Run();
