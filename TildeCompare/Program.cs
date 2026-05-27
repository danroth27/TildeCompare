using TildeCompare.Components;

var builder = WebApplication.CreateBuilder(args);

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// MVC + Razor Pages + View Components
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

// Needed by App.razor to read PathBase for <base href>.
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Mount the whole app under a non-trivial PathBase so we can observe the
// PathBase resolution behavior of '~/' in MVC vs '@Assets[]' in Blazor.
app.UsePathBase("/myapp");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(TildeCompare.Client._Imports).Assembly);

app.Run();
