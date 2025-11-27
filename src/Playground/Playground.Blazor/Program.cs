using FSH.Framework.Blazor.UI;
using FSH.Playground.Blazor.Components;
using FSH.Playground.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHeroUI();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();
builder.Services.AddTransient<BffAuthDelegatingHandler>();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
                 ?? throw new InvalidOperationException("Api:BaseUrl configuration is missing.");

builder.Services.AddHttpClient("AuthApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Simple health endpoints for ALB/ECS
app.MapGet("/health/ready", () => Results.Ok(new { status = "Healthy" }))
   .AllowAnonymous();

app.MapGet("/health/live", () => Results.Ok(new { status = "Alive" }))
   .AllowAnonymous();

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapBffAuthEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
