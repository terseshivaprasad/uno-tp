using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;
using UnoTP.Data;
using UnoTP.Infrastructure;
using UnoTP.Models;
using UnoTP.Services;
using UnoTP.Services.Auth;
using UnoTP.Services.Ckyc;
using UnoTP.Services.Idfy;
using UnoTP.Services.NameMatch;
using UnoTP.Services.NameScreening;
using UnoTP.Services.Pan;
using UnoTP.Services.Shortener;
using UnoTP.Services.UidMasking;
using UnoTP.ViewModels;

var builder = WebApplication.CreateBuilder(args);

// Render (and most container platforms) assign the listen port via $PORT. Run
// locally it takes 5102, clear of the other eSarathi apps' ports.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5102";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
// No Server header: what the app runs on is nobody's business (see SecurityHeaders).
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

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
builder.Services.AddSingleton<IUrlHelperFactory>(new ApplicationUrlHelperFactory(new UrlHelperFactory()));
// The app's data is in SQL Server (ConnectionStrings:UnoTP), read in process through
// the interfaces the pages read (UnoTP.Data). The backend APIs are behind one
// gateway (Backend:BaseUrl), unless an API's own section gives it an address of its
// own (BaseUrl); each has a settings section with that, its base path and the path
// of its calls: the way in (AuthApi), the PAN check (PanApi),
// Aadhaar masking (UidMasking), the CKYC search (Ckyc), the document checks (Idfy),
// name screening, name match and the link shortener.
builder.Services.Configure<BackendOptions>(builder.Configuration.GetSection(BackendOptions.Section));
builder.Services.Configure<AuthApiOptions>(builder.Configuration.GetSection(AuthApiOptions.Section));
builder.Services.Configure<PanApiOptions>(builder.Configuration.GetSection(PanApiOptions.Section));
builder.Services.Configure<UidMaskingOptions>(builder.Configuration.GetSection(UidMaskingOptions.Section));
builder.Services.Configure<CkycOptions>(builder.Configuration.GetSection(CkycOptions.Section));
builder.Services.Configure<NameScreeningOptions>(builder.Configuration.GetSection(NameScreeningOptions.Section));
builder.Services.Configure<NameMatchOptions>(builder.Configuration.GetSection(NameMatchOptions.Section));
builder.Services.Configure<IdfyOptions>(builder.Configuration.GetSection(IdfyOptions.Section));
builder.Services.Configure<ShortenerOptions>(builder.Configuration.GetSection(ShortenerOptions.Section));
builder.Services.Configure<PaymentLinkOptions>(builder.Configuration.GetSection(PaymentLinkOptions.Section));
// A submitted application's payment link: made, shortened and put on record once the application is saved.
builder.Services.AddScoped<PaymentLinkSender>();
builder.Services.Configure<PortalOptions>(builder.Configuration.GetSection(PortalOptions.Section));
builder.Services.AddScoped<IPartner, SessionPartner>();
// Investor Identification's steps, for the primary holder and each joint holder alike.
builder.Services.AddScoped<HolderSearch>();
// Nothing stands in for the database or an outside service: each answers for real.
if (!SqlDataServiceCollectionExtensions.Configured(builder.Configuration))
    throw new InvalidOperationException("ConnectionStrings:UnoTP is not set. The app runs only on its database.");
builder.Services.AddSqlData();
// Every API the app calls needs an address: its own, or the gateway's.
var apiSections = new List<string> { AuthApiOptions.Section, UidMaskingOptions.Section, IdfyOptions.Section, NameScreeningOptions.Section };
var outsideSwitches = new OutsideSwitches(builder.Configuration);
if (outsideSwitches.IsOn(OutsideSwitches.PanCheck)) apiSections.Add(PanApiOptions.Section);
if (outsideSwitches.IsOn(OutsideSwitches.FetchCkyc)) apiSections.Add(CkycOptions.Section);
if (outsideSwitches.IsOn(OutsideSwitches.NameMatch)) apiSections.Add(NameMatchOptions.Section);
// The payment link is shortened when the short link is switched on (Backend:Switches:ShortLink)
// and the shortener has a path; otherwise it is kept and sent in full.
var shortensLinks = outsideSwitches.IsOn(OutsideSwitches.ShortLink) && ShortenerOptions.Configured(builder.Configuration);
if (shortensLinks) apiSections.Add(ShortenerOptions.Section);
var unaddressed = BackendHttpClients.Unaddressed(builder.Configuration, [.. apiSections]);
if (unaddressed.Count > 0)
    throw new InvalidOperationException($"No address is set for {string.Join(", ", unaddressed)}. Set Backend:BaseUrl to the gateway they are behind (appsettings.json carries it with a <gateway-host> placeholder), or BaseUrl in each one's own section.");
builder.Services.AddAuthApi();
// Who the partner is: what the auth API said when their session started (see PartnerSession).
builder.Services.AddScoped<IPartnerApi, SessionPartnerApi>();
// A holder's PAN is checked with NSDL unless the PAN check is switched off
// (Backend:Switches:PanCheck), and an Aadhaar is always masked.
builder.Services.AddPanApi();
builder.Services.AddUidMasking();
// CERSAI is searched when the partner chooses Fetch from CKYC, unless it is
// switched off (Backend:Switches:FetchCkyc): its button is then disabled.
builder.Services.AddCkyc();
builder.Services.AddDocumentChecks(builder.Configuration);
if (shortensLinks) builder.Services.AddShortener();
else builder.Services.AddUnshortenedLinks();
// The backend's slow-changing answers kept in memory, around whichever answers (see CachedBackend).
builder.Services.AddBackendCaching();
// The console's schedule, read once per request for the gate, the tiles and the bell.
builder.Services.AddScoped<ConsoleState>();
// The backend's lists and rules, kept for a few minutes (see Lookups).
// Each entry counts one; the limit keeps partners' searches from growing it without end.
builder.Services.AddMemoryCache(options => options.SizeLimit = 50_000);
builder.Services.AddScoped<Lookups>();

// Who the partner is - the sign-in, and nothing else (see PartnerSession). The
// application and everything on it are the backend's, for audit; the application a
// page is on is in its address (see ApplicationUrls).
builder.Services.AddDistributedMemoryCache();
// The app's cookies all carry its name - unotp.session, unotp.antiforgery and
// unotp.tempdata - so they are told apart from other apps' on the same host. Outside Development every one is Secure whatever the
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
builder.Services.Configure<CookieTempDataProviderOptions>(options =>
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
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

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

// A cap on how often one caller may come in, and on tries at one document (see RateLimits).
builder.Services.AddRateLimits(builder.Configuration);

// What an upload is doing now, for the wait on the screen to say (see UploadProgress).
builder.Services.AddSingleton<UploadProgress>();

// /health, for the host to check the app is up. It asks nothing of the backend.
builder.Services.AddHealthChecks();

// Who the partner is, from the backend (GET me), once a request.
builder.Services.AddScoped<CurrentPartner>();

// Feature switches: the "Features" section of appsettings, and nothing else.
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<FeatureFlags>(builder.Configuration.GetSection("Features"));
builder.Services.AddSingleton(sp => new FeatureSet(sp.GetRequiredService<IOptions<FeatureFlags>>().Value));

var app = builder.Build();

// First, so everything after it sees the scheme and address the proxy forwarded.
app.UseForwardedHeaders();
// On every response, the error page and static files included (see SecurityHeaders).
app.UseSecurityHeaders(app.Configuration);
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

// Whatever a page lets through is logged and shown as the error page (see
// GlobalExceptionMiddleware).
// Development keeps the developer page, which shows the exception itself.
if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();
else
{
    app.UseMiddleware<GlobalExceptionMiddleware>();
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

// Who is signed in, for the error log (t_Unotp_Logs, SqlErrorLog): set once the
// session is read, so an error logged later in the request is put down to them.
app.Use((context, next) =>
{
    context.Items[SqlErrorLog.UserItem] = context.Session.SignedInUser();
    return next();
});

// A change posted from a page comes back as that page in one round trip, not two
// (see PartialFollow). Before routing, so the page it follows on to is routed afresh.
app.UsePartialFollow();

// Input no page of this app would send - a parameter given twice, a character no
// field takes - is refused before a page sees it.
app.UseMiddleware<InputScreening>();

app.UseRouting();

// After routing, so the limit meets only the action marked with it: the way in.
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");

// The portal may open the app at its root: the way in is its entry, with whatever it sent.
app.MapGet("/", (HttpContext ctx) => Results.LocalRedirect("~/Home/Index" + ctx.Request.QueryString));

// The pages keep the old app's addresses, so the portal's links and saved links
// still work. The IIS virtual directory (e.g. /WA_FD_UNOTP) is the path base and is
// never part of a route. Addresses the old app had that name no page here land on
// the page that took their place, with whatever query they carried.
var moved = new (string From, string To)[]
{
    ("/Dashboard/Index", "/Dashboard"),
    // A step's address with no application in it has none to open: the way in is a search.
    ("/UploadInvestorDocuments", "/SearchInvestor"),
    ("/InvestorInformation", "/SearchInvestor"),
    ("/BankDetails", "/SearchInvestor"),
    ("/FDConfiguration", "/SearchInvestor"),
    ("/ReviewSummary", "/SearchInvestor"),
};
foreach (var (from, to) in moved)
    app.MapGet(from, (HttpContext ctx) => Results.LocalRedirect("~" + to + ctx.Request.QueryString));

app.Run();
