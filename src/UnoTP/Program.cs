using Microsoft.AspNetCore.Mvc;
using UnoTP;
using UnoTP.Backend;
using UnoTP.Backend.Idfy;
using UnoTP.Backend.Mock;
using UnoTP.Features;

var builder = WebApplication.CreateBuilder(args);

// Render (and most container platforms) assign the listen port via $PORT. Run
// locally it takes 5102, clear of the other eSarathi apps' ports.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5102";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.
// MVC: controllers and their views. FeatureGate closes the pages of a console
// feature that is switched off, and every post has to carry the antiforgery token
// the form tag helper writes.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new FeatureGate());
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddSingleton<AppUrls>();
// All data comes from the backend API, and every outside check - NSDL, document
// identification, masking, OCR, verification, the PAN-Aadhaar link - from a
// service of its own. With no Backend:BaseUrl set, the mock answers for all of
// them from memory. With Idfy:BaseUrl set, IDfy answers the checks it has an
// endpoint for, and whatever answered before keeps the rest.
builder.Services.Configure<BackendOptions>(builder.Configuration.GetSection(BackendOptions.Section));
builder.Services.Configure<IdfyOptions>(builder.Configuration.GetSection(IdfyOptions.Section));
builder.Services.AddScoped<IPartner, SessionPartner>();
// Investor Identification's steps, for the primary holder and each joint holder alike.
builder.Services.AddScoped<UnoTP.ViewModels.HolderSearch>();
if (BackendOptions.Configured(builder.Configuration)) builder.Services.AddBackendApi();
else builder.Services.AddMockBackend();
if (IdfyOptions.Configured(builder.Configuration)) builder.Services.AddIdfy();
// The console's schedule, read once per request for the gate, the tiles and the bell.
builder.Services.AddScoped<UnoTP.Models.ConsoleState>();
// The backend's lists and rules, kept for a few minutes (see Lookups).
builder.Services.AddMemoryCache();
builder.Services.AddScoped<Lookups>();

// Who the partner is and the application they are on (see PartnerSession).
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.IsEssential = true;
});

// Who the partner is, from the backend (GET me), once a request.
builder.Services.AddScoped<CurrentPartner>();

// Feature switches: defaults from appsettings, per-session override via ?ff= (see FeatureSet).
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<FeatureFlags>(builder.Configuration.GetSection("Features"));
builder.Services.AddScoped(sp =>
{
    var ctx = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    return ctx?.Items[FeatureSet.ItemKey] as FeatureSet
        ?? new FeatureSet(
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FeatureFlags>>().Value);
});

var app = builder.Build();

// Deployed under a virtual directory (https://server/<dir>/Dashboard), the app is
// told the directory here and every address it writes carries it. IIS hands the
// directory over by itself; any other host sets PathBase in appsettings or as an
// environment variable. Links in the pages all go through ~/ or asp-action, and
// the redirects below through ~/, so none of them skips it.
var pathBase = app.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

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

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

// Must run before the pages so a ?ff= override applies to this render.
app.UseFeatureOverrides();

// After the features, so ?agency= is honoured only while the demo data is on.
app.UsePartner();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.LocalRedirect("~/Dashboard"));

// Old addresses, kept for saved links and the other apps' tiles. A search
// bookmarked under the old address keeps its query, so it lands on its result.
app.MapGet("/Apps/UnoTp/Classic", () => Results.LocalRedirect("~/Dashboard"));
app.MapGet("/Apps/UnoTp/Classic/SearchInvestor", (HttpContext ctx) =>
    Results.LocalRedirect("~/Purchase/InvestorIdentification" + ctx.Request.QueryString));

// The new design's dashboard and consent tracker are kept in archive/new-design.
// Their addresses - saved links, and the other apps' tiles - land on the classic
// dashboard instead.
foreach (var archived in new[] { "/Apps/UnoTp/Dashboard/{**rest}", "/Apps/UnoTp/ConsentTracker" })
{
    app.MapGet(archived, () => Results.LocalRedirect("~/Dashboard"));
}

// Its later wizard steps are served again (ApplicationController); its first two are
// the classic Investor Identification and Upload Documents.
app.MapGet("/Apps/UnoTp/Application/HolderIdentification", () => Results.LocalRedirect("~/Purchase/InvestorIdentification"));
app.MapGet("/Apps/UnoTp/Application/UploadDocuments", () => Results.LocalRedirect("~/Apps/UnoTp/Classic/UploadDocuments"));

app.Run();
