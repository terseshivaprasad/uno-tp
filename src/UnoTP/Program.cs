using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
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
    // Nobody reaches a page without a session from Home (see SessionAuthenticationFilter).
    options.Filters.Add(new SessionAuthenticationFilter());
    options.Filters.Add(new FeatureGate());
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddSingleton<AppUrls>();
// A wizard step's address carries the application's number, and every link and
// redirect to another step carries it on (see ApplicationUrls).
builder.Services.AddSingleton<Microsoft.AspNetCore.Mvc.Routing.IUrlHelperFactory>(
    new ApplicationUrlHelperFactory(new Microsoft.AspNetCore.Mvc.Routing.UrlHelperFactory()));
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
// The backend's slow-changing answers kept in memory, around whichever answers (see CachedBackend).
builder.Services.AddBackendCaching();
// The console's schedule, read once per request for the gate, the tiles and the bell.
builder.Services.AddScoped<UnoTP.Models.ConsoleState>();
// The backend's lists and rules, kept for a few minutes (see Lookups).
// Each entry counts one; the limit keeps partners' searches from growing it without end.
builder.Services.AddMemoryCache(options => options.SizeLimit = 50_000);
builder.Services.AddScoped<Lookups>();

// Who the partner is - the sign-in, and nothing else (see PartnerSession). The
// application and everything on it are the backend's, for audit; the application a
// page is on is in its address (see ApplicationUrls).
builder.Services.AddDistributedMemoryCache();
// The app's cookies all carry its name - unotp.session, unotp.antiforgery,
// unotp.tempdata and unotp.ff (FeatureSet) - so they are told apart from other
// apps' on the same host. Outside Development every one is Secure whatever the
// request looks like, so none can go over plain HTTP even if the proxy's forwarded
// scheme is lost; Development runs on http://localhost, so there they follow it.
var cookieSecure = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "unotp.session";
    options.Cookie.HttpOnly = true;
    // Lax, not Strict: the portal opens the app from another site, and a Strict
    // cookie is withheld on that redirect chain, so the partner would arrive with
    // no session. Lax still keeps it off cross-site posts; antiforgery guards them too.
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = cookieSecure;
    options.Cookie.IsEssential = true;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "unotp.antiforgery";
    options.Cookie.SecurePolicy = cookieSecure;
});

// TempData rides in a cookie encrypted with the keys below: the search typed on
// Investor Identification until Proceed, and what a post has to say once.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.CookieTempDataProviderOptions>(options =>
{
    options.Cookie.Name = "unotp.tempdata";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = cookieSecure;
    options.Cookie.IsEssential = true;
});

// Behind Render's proxy, which ends TLS: the scheme and client address it forwards
// are taken as the request's own, so the cookie above is Secure in production.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The proxy's address is not fixed, so any is trusted; the app is only ever reached through it.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// The keys that protect the session and antiforgery cookies. Kept at
// DataProtection:KeysPath when it is set (a persistent disk), so a restart or a
// second instance does not sign everyone out; without it they last as long as the process.
var keys = builder.Services.AddDataProtection().SetApplicationName("UnoTP");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    keys.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

// Pages, styles, scripts and JSON go compressed - Brotli where the browser takes
// it, gzip otherwise. Over HTTPS too: the only secret a page carries is the
// antiforgery token, which is issued afresh with every page, so compression gives
// nothing away about it (BREACH); the session itself is in a cookie, never a page.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["image/svg+xml", "application/problem+json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);

// /health, for the host to check the app is up. It asks nothing of the backend.
builder.Services.AddHealthChecks();

// Who the partner is, from the backend (GET me), once a request.
builder.Services.AddScoped<CurrentPartner>();

// Feature switches: defaults from appsettings, per-session override via ?ff= (see FeatureSet).
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<FeatureFlags>(builder.Configuration.GetSection("Features"));
builder.Services.Configure<EntryOptions>(builder.Configuration.GetSection(EntryOptions.Section));
builder.Services.AddScoped(sp =>
{
    var ctx = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    return ctx?.Items[FeatureSet.ItemKey] as FeatureSet
        ?? new FeatureSet(
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FeatureFlags>>().Value);
});

var app = builder.Build();

// First, so everything after it sees the scheme and address the proxy forwarded.
app.UseForwardedHeaders();
// Before anything that writes a response, so all of it is compressed.
app.UseResponseCompression();

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
// Whatever a page lets through is logged and shown as the error page, or as
// Session Expired when the backend has ended the session (see GlobalExceptionMiddleware).
// Development keeps the developer page, which shows the exception itself.
if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();
else
{
    app.UseMiddleware<GlobalExceptionMiddleware>();
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// No UseHttpsRedirection: Render terminates TLS at the edge and forwards plain HTTP to the container.

// Pages and JSON are never kept by the browser or anything between: they carry an
// investor's name, address, PAN and account details, which must not be left in a
// cache on a shared computer. And a page held on to is served as old markup against
// freshly versioned scripts, which then look for elements it does not have.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var type = context.Response.ContentType;
        if (type is not null && (type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) || type.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
        }
        return Task.CompletedTask;
    });
    await next();
});

// A stylesheet or script written with asp-append-version carries ?v= its content
// hash, so it can be kept for a year: a change is a new address. Anything else is
// checked with the server each time.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Query.ContainsKey("v"))
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    },
});

app.UseSession();

// Must run before the pages so a ?ff= override applies to this render.
app.UseFeatureOverrides();

// After the features, so ?agency= is honoured only while the demo data is on.
app.UsePartner();

// A change posted from a page comes back as that page in one round trip, not two
// (see PartialFollow). Before routing, so the page it follows on to is routed afresh.
app.UsePartialFollow();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// The portal may open the app at its root: the way in is Home, with whatever it sent.
app.MapGet("/", (HttpContext ctx) => Results.LocalRedirect("~/Home" + ctx.Request.QueryString));

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
// An address with no application in it has none to open: the way in is a search.
foreach (var bare in new[] { "/Apps/UnoTp/Application/UploadDocuments", "/Apps/UnoTp/Classic/UploadDocuments" })
{
    app.MapGet(bare, () => Results.LocalRedirect("~/Purchase/InvestorIdentification"));
}

app.Run();
