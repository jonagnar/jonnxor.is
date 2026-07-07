using Jonnxor.Admin.Components;
using Jonnxor.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Loopback-only posture: refuse to boot on any non-loopback bind. Kestrel resolves
// its addresses from explicit app.Urls entries if present, otherwise from the "urls"
// configuration key (appsettings "Urls", ASPNETCORE_URLS, or a --urls override — all
// of which flow into configuration). Guard whichever set is effective, before Run().
IEnumerable<string> effectiveUrls = app.Urls.Count > 0
    ? app.Urls
    : app.Configuration[WebHostDefaults.ServerUrlsKey]
        ?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? [];
LoopbackGuard.EnsureLoopback(effectiveUrls);

app.Run();
