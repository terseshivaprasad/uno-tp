using UnoTP.Api;
using UnoTP.Api.Data;
using UnoTP.Api.Dms;
using UnoTP.Api.Routes;
using UnoTP.Backend;
using UnoTP.Backend.Mock;

var builder = WebApplication.CreateBuilder(args);

// Run locally it takes 5110, clear of the eSarathi apps' ports; a container host gives $PORT.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5110";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Who is asking: the web app sends the partner and their session with every request.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPartner, RequestPartner>();

// Everything answers from the mock, then what the database holds is taken over by
// it: the last registration wins. Still on the mock: the deposits Renew FD lists,
// and the outside services (NSDL, OCR, identification, verification, masking, the
// PAN-Aadhaar link, face match, the portal's decryption).
builder.Services.AddMockBackend();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<SqlReference>();
builder.Services.AddSingleton<IReferenceApi>(sp => sp.GetRequiredService<SqlReference>());
builder.Services.AddScoped<SqlPartners>();
builder.Services.AddScoped<IPartnerApi>(sp => sp.GetRequiredService<SqlPartners>());
builder.Services.AddScoped<ISessionApi>(sp => sp.GetRequiredService<SqlPartners>());
builder.Services.AddSingleton<SqlMasters>();
builder.Services.AddSingleton<IInvestorApi>(sp => sp.GetRequiredService<SqlMasters>());
builder.Services.AddSingleton<ISourcingApi>(sp => sp.GetRequiredService<SqlMasters>());
builder.Services.AddSingleton<IDepositApi>(sp => sp.GetRequiredService<SqlMasters>());
builder.Services.AddSingleton<IPlaceApi>(sp => sp.GetRequiredService<SqlMasters>());
builder.Services.AddScoped<IConsoleApi, SqlConsole>();
builder.Services.AddScoped<IPayInSlipApi, SqlPayInSlips>();
builder.Services.AddScoped<ILinkApi, SqlLinks>();
builder.Services.AddScoped<SqlApplications>();
builder.Services.AddScoped<IApplicationApi>(sp => sp.GetRequiredService<SqlApplications>());
builder.Services.AddScoped<IRenewalOpener>(sp => sp.GetRequiredService<SqlApplications>());
builder.Services.AddScoped<IDocumentApi, FileDocuments>();

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
// A document asked for on an application that is not the partner's, or not there.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (KeyNotFoundException) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
    }
    // A request the rules or the masters cannot answer: a rate the card does not hold.
    catch (ArgumentException e) when (!context.Response.HasStarted)
    {
        context.RequestServices.GetRequiredService<ILogger<Program>>().LogWarning(e, "Refused {Path}", context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

// Every call is made for a partner in an open session (t_User_Session), bar the
// way in - decrypting the portal's values and starting the session - and the
// lists and rules, which are nobody's in particular.
string[] open = ["/health", "/sessions", "/external/decrypt/", "/reference", "/config"];
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (!open.Any(p => path.Equals(p.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) || path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
    {
        var user = context.Request.Headers[ApiClient.PartnerHeader].ToString();
        var session = context.Request.Headers[ApiClient.SessionHeader].ToString();
        if (user.Length == 0 || session.Length == 0
            || !await context.RequestServices.GetRequiredService<SqlPartners>().SessionOpenAsync(user, session, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }
    await next();
});

app.MapHealthChecks("/health");
app.MapApplications();
app.MapData();
app.MapExternal();

app.Run();

public partial class Program;
