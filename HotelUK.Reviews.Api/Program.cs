using System.IO.Compression;
using System.Threading.RateLimiting;
using HotelUK.Reviews.Api.Models;
using HotelUK.Reviews.Api.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// --- Heroku gives the app a random port in the PORT variable ---------------
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

// --- Configuration ---------------------------------------------------------
// Values come from appsettings.json locally, and from Heroku Config Vars in
// production (Meta__PageAccessToken, Meta__PublicBaseUrl, ...).
builder.Services.Configure<MetaOptions>(builder.Configuration.GetSection("Meta"));

builder.Services.AddControllers();
builder.Services.AddSingleton<ReviewImageGenerator>();
builder.Services.AddHttpClient<MetaPublisherService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
});

// --- Rate limit ------------------------------------------------------------
// Without this, one person with a script can push hundreds of posts onto the
// hotel's Facebook Page. Six submissions per hour from one address is plenty
// for a real guest and useless to a spammer.
var perHour = builder.Configuration.GetValue<int?>("Meta:MaxSubmissionsPerHourPerIp") ?? 6;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy<string>("reviews", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = perHour,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            }));
});

// --- Compression -----------------------------------------------------------
// The review page is one 55 KB file. Compressed it is roughly 12 KB, which is
// the difference between instant and sluggish on a hotel's guest wifi.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes
        .Concat(new[] { "image/svg+xml", "application/json" });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// --- CORS ------------------------------------------------------------------
// Only needed if you host the form somewhere other than this app. With no
// origins listed we add no policy at all, so the browser's own same-origin
// rule is the only thing that can reach the API from a page.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? Array.Empty<string>();

if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
    });
}

var app = builder.Build();

// --- Behind Heroku's router ------------------------------------------------
// Heroku terminates TLS, so the app sees plain http. ForwardedHeaders only
// trusts loopback addresses by default, and the Heroku router is not one, so
// without clearing the lists below the X-Forwarded-* headers are ignored and
// Request.Scheme stays "http".
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    await next();
});

app.UseResponseCompression();

if (allowedOrigins.Length > 0)
{
    app.UseCors();
}

app.UseRateLimiter();

// Serves wwwroot: the review form at "/" and the temporary PNGs at /generated/*.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var headers = ctx.Context.Response.Headers;
        var path = ctx.Context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/generated/", StringComparison.OrdinalIgnoreCase))
        {
            // Instagram reads these once and then they are deleted.
            headers[HeaderNames.CacheControl] = "no-store";
        }
        else if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            // Always fetch the latest form, so edits go live immediately.
            headers[HeaderNames.CacheControl] = "no-cache";
        }
        else
        {
            // Photographs and the clip: cache hard, they rarely change.
            headers[HeaderNames.CacheControl] = "public,max-age=604800";
        }
    }
});

app.MapControllers();

app.Run();
