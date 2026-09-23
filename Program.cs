using UnoTp.Features;

var builder = WebApplication.CreateBuilder(args);

// Render (and most container platforms) assign the listen port via $PORT.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.
builder.Services.AddRazorPages();

// Feature switches: defaults from appsettings, per-session override via ?ff= (see FeatureSet).
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<UnoTp.Features.FeatureFlags>(builder.Configuration.GetSection("Features"));
builder.Services.AddScoped(sp =>
{
    var ctx = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    return ctx?.Items[UnoTp.Features.FeatureSet.ItemKey] as UnoTp.Features.FeatureSet
        ?? new UnoTp.Features.FeatureSet(
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UnoTp.Features.FeatureFlags>>().Value);
});
builder.Services.AddScoped<UnoTp.Features.ConsentPlan>();

var app = builder.Build();

var staticFileTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
staticFileTypeProvider.Mappings[".drawio"] = "application/octet-stream";

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// No UseHttpsRedirection: Render terminates TLS at the edge and forwards plain HTTP to the container.

// The pages carry no cache headers of their own, so Safari holds on to a copy and
// serves old markup against freshly versioned scripts - the script then looks for
// elements the cached page does not have and the screen stops responding. The
// markup is rendered from mock data on every request anyway, so none of it is
// worth caching.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        if (context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
        }
        return Task.CompletedTask;
    });
    await next();
});

app.UseRouting();

// Must run before the pages so a ?ff= override applies to this render.
app.UseFeatureOverrides();

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = staticFileTypeProvider
});

app.UseAuthorization();

app.MapRazorPages();

// Phones get the same page as laptops, with the mobile board as its phone view; the
// old mobile-only addresses forward there so saved links keep working.
app.MapGet("/Mobile", () => Results.Redirect("/Classic"));
app.MapGet("/Apps/UnoTp/Dashboard/Mobile", () => Results.Redirect("/Apps/UnoTp/Dashboard"));

app.Run();
