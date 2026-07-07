using Jonnxor.Admin.Components;
using Jonnxor.Admin.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.AddSingleton(static sp => sp.GetRequiredService<IOptions<AdminOptions>>().Value);
builder.Services.AddSingleton<RepoPaths>();
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();

// Health services (stateless singletons). Explicit factories so the optional
// test-only ctor parameters (HttpMessageHandler, env lookup) stay at their
// real defaults instead of being container-resolved.
builder.Services.AddSingleton(static sp =>
    new DirectusHealthService(sp.GetRequiredService<RepoPaths>()));
builder.Services.AddSingleton(static sp =>
    new SnapshotHealthService(sp.GetRequiredService<RepoPaths>(), sp.GetRequiredService<IProcessRunner>()));
builder.Services.AddSingleton(static sp =>
    new ForgejoCiService(sp.GetRequiredService<AdminOptions>()));

// Pipeline orchestration (stateful singleton: global run lock + output buffer).
// Explicit factory so the test-only liveVerify seam stays at its CliRunner default.
builder.Services.AddSingleton(static sp =>
    new ContentPipelineService(
        sp.GetRequiredService<RepoPaths>(),
        sp.GetRequiredService<IProcessRunner>(),
        sp.GetRequiredService<SnapshotHealthService>(),
        liveVerify: null,
        sp.GetRequiredService<ILogger<ContentPipelineService>>()));

// Coverage report (stateless apart from the drill-down cache of the last build).
builder.Services.AddSingleton(static sp =>
    new CoverageService(sp.GetRequiredService<RepoPaths>()));

// Panel preferences (operator state under the OS app-data dir, never the repo) and
// the Config page's effective-config view. Explicit factories keep the test-only
// seams (base directory, env lookup) at their real defaults.
builder.Services.AddSingleton(static _ => new PanelPreferencesService());
builder.Services.AddSingleton(static sp => new ConfigInspectionService(
    sp.GetRequiredService<AdminOptions>(), sp.GetRequiredService<RepoPaths>()));

var app = builder.Build();

// Panel-owned assets (wwwroot: admin.css).
app.UseStaticFiles();

// The site's design tokens, served read-only straight from the client workspace at
// /site-assets so the panel inherits dawn/rune/neon and can never drift from the
// site's token vocabulary (design doc §7). Static-file middleware serves GET/HEAD
// only — no write path.
var repoPaths = app.Services.GetRequiredService<RepoPaths>();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(repoPaths.ClientDir, "public", "assets")),
    RequestPath = "/site-assets",
});

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
