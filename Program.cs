var builder = WebApplication.CreateBuilder(args);

// Render (and most container platforms) assign the listen port via $PORT.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.
builder.Services.AddRazorPages();

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

app.UseRouting();

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = staticFileTypeProvider
});

app.UseAuthorization();

app.MapRazorPages();

app.Run();
