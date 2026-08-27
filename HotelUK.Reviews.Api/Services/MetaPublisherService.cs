using System.Text;
using System.Text.Json;
using HotelUK.Reviews.Api.Models;
using Microsoft.Extensions.Options;

namespace HotelUK.Reviews.Api.Services;

/// <summary>
/// Pushes one review straight to Meta. Nothing is stored: the only thing that
/// touches disk is the generated PNG, and only because Instagram insists on
/// downloading images from a public URL rather than accepting an upload.
/// </summary>
public sealed class MetaPublisherService
{
    private readonly HttpClient _http;
    private readonly MetaOptions _options;
    private readonly ReviewImageGenerator _imageGenerator;
    private readonly ILogger<MetaPublisherService> _logger;
    private readonly string _imageFolder;

    private const string PublicImageRoute = "generated";

    public MetaPublisherService(
        HttpClient http,
        IOptions<MetaOptions> options,
        ReviewImageGenerator imageGenerator,
        IWebHostEnvironment env,
        ILogger<MetaPublisherService> logger)
    {
        _http = http;
        _options = options.Value;
        _imageGenerator = imageGenerator;
        _logger = logger;

        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;

        _imageFolder = Path.Combine(webRoot, PublicImageRoute);

        try
        {
            Directory.CreateDirectory(_imageFolder);
        }
        catch (Exception ex)
        {
            // Heroku runs the container as a non-root user, so a folder made by root
            // at build time is not writable at runtime. The Dockerfile fixes that;
            // log it clearly rather than failing to start.
            _logger.LogError(ex,
                "Could not create {Folder}. Instagram posting will fail until this folder is writable.",
                _imageFolder);
        }
    }

    public async Task<PublishResult> PublishAsync(ReviewSubmission review, CancellationToken ct = default)
    {
        var result = new PublishResult();

        if (review.Rating < _options.MinimumRatingToPublish)
        {
            result.Warnings.Add(
                $"Rating {review.Rating} is below the publishing threshold of {_options.MinimumRatingToPublish}. " +
                "Nothing was posted; the review was sent to the hotel privately instead.");

            await SendPrivateFeedbackAsync(review, ct);
            return result;
        }

        if (_options.PostToFacebook)
        {
            try
            {
                result.FacebookPostId = await PostToFacebookAsync(review, ct);
                result.FacebookPublished = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Facebook publish failed.");
                result.Warnings.Add("Facebook: " + ex.Message);
            }
        }

        if (_options.PostToInstagram)
        {
            try
            {
                result.InstagramMediaId = await PostToInstagramAsync(review, ct);
                result.InstagramPublished = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Instagram publish failed.");
                result.Warnings.Add("Instagram: " + ex.Message);
            }
        }

        return result;
    }

    // ------------------------------------------------- private feedback

    /// <summary>
    /// A review too low to publish still has to reach somebody. It always goes to
    /// the log, and to your webhook as well when one is configured.
    /// </summary>
    private async Task SendPrivateFeedbackAsync(ReviewSubmission review, CancellationToken ct)
    {
        _logger.LogWarning(
            "LOW RATING {Rating}/5 from {Name} ({Country}): {Text}",
            review.Rating,
            review.CustomerName.Trim(),
            string.IsNullOrWhiteSpace(review.Country) ? "not given" : review.Country!.Trim(),
            review.ReviewText.Trim());

        if (string.IsNullOrWhiteSpace(_options.PrivateFeedbackWebhookUrl))
        {
            _logger.LogWarning(
                "Meta:PrivateFeedbackWebhookUrl is not set, so this review is in the log only. " +
                "Point it at a Slack, Google Chat, Discord or Zapier webhook so the manager is told.");
            return;
        }

        try
        {
            var origin = string.IsNullOrWhiteSpace(review.Country) ? "" : $" from {review.Country!.Trim()}";

            // "text" is the field Slack and Google Chat read, "content" is Discord's.
            // Sending both means one payload works with any of them, and Zapier or
            // Make can pick out the separate fields underneath.
            var payload = new
            {
                text = $"{review.Rating}/5 \u2014 {review.CustomerName.Trim()}{origin}\n\n" +
                       $"\u201C{review.ReviewText.Trim()}\u201D\n\n" +
                       "Not published. Please follow this one up.",
                content = $"{review.Rating}/5 from {review.CustomerName.Trim()}: {review.ReviewText.Trim()}",
                rating = review.Rating,
                customerName = review.CustomerName.Trim(),
                country = review.Country?.Trim(),
                reviewText = review.ReviewText.Trim(),
                receivedUtc = DateTime.UtcNow
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _http.PostAsync(_options.PrivateFeedbackWebhookUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Private feedback webhook returned HTTP {Code}: {Body}",
                                 (int)response.StatusCode, Trim(body));
            }
        }
        catch (Exception ex)
        {
            // The guest has already been thanked. Never let this throw.
            _logger.LogError(ex, "Private feedback webhook failed. The review is still in the log above.");
        }
    }

    // ------------------------------------------------------------- Facebook

    private async Task<string> PostToFacebookAsync(ReviewSubmission review, CancellationToken ct)
    {
        Require(_options.PageId, "Meta:PageId");
        Require(_options.PageAccessToken, "Meta:PageAccessToken");

        var url = $"https://graph.facebook.com/{_options.GraphApiVersion}/{_options.PageId}/feed";

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["message"] = BuildFacebookMessage(review),
            ["access_token"] = _options.PageAccessToken
        });

        using var response = await _http.PostAsync(url, form, ct);
        var json = await ReadJsonOrThrowAsync(response, "Facebook feed", ct);

        return json.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
    }

    // ------------------------------------------------------------ Instagram

    private async Task<string> PostToInstagramAsync(ReviewSubmission review, CancellationToken ct)
    {
        Require(_options.InstagramUserId, "Meta:InstagramUserId");
        Require(_options.EffectiveInstagramToken, "Meta:PageAccessToken");
        Require(_options.PublicBaseUrl, "Meta:PublicBaseUrl");

        if (_options.PublicBaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "PublicBaseUrl points at localhost. Instagram downloads the image from that address, " +
                "so it must be a public https URL (your Heroku app URL, or an ngrok tunnel while testing).");
        }

        CleanUpOldImages();

        var fileName = $"{Guid.NewGuid():N}.png";
        var filePath = Path.Combine(_imageFolder, fileName);
        var imageUrl = $"{_options.PublicBaseUrl.TrimEnd('/')}/{PublicImageRoute}/{fileName}";

        // NOTE ON HEROKU: the dyno filesystem is ephemeral and per-dyno. This works
        // because Instagram fetches the image within a few seconds, on the same app.
        // If you ever scale past ONE web dyno, move this to object storage
        // (Cloudinary / S3 / Azure Blob) and swap imageUrl for the hosted URL.
        // Everything else in this method stays exactly the same.
        await File.WriteAllBytesAsync(filePath, _imageGenerator.Render(review), ct);

        try
        {
            var creationId = await CreateInstagramContainerAsync(imageUrl, BuildInstagramCaption(review), ct);
            await WaitForContainerAsync(creationId, ct);
            return await PublishInstagramContainerAsync(creationId, ct);
        }
        finally
        {
            TryDelete(filePath);
        }
    }

    private async Task<string> CreateInstagramContainerAsync(string imageUrl, string caption, CancellationToken ct)
    {
        var url = $"https://graph.facebook.com/{_options.GraphApiVersion}/{_options.InstagramUserId}/media";

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["image_url"] = imageUrl,
            ["caption"] = caption,
            ["access_token"] = _options.EffectiveInstagramToken
        });

        using var response = await _http.PostAsync(url, form, ct);
        var json = await ReadJsonOrThrowAsync(response, "Instagram media container", ct);

        var id = json.TryGetProperty("id", out var value) ? value.GetString() : null;
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Instagram did not return a container id.");

        return id!;
    }

    /// <summary>Instagram downloads the image asynchronously; publishing before it is FINISHED fails.</summary>
    private async Task WaitForContainerAsync(string creationId, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_options.InstagramContainerTimeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            var url = $"https://graph.facebook.com/{_options.GraphApiVersion}/{creationId}" +
                      $"?fields=status_code,status&access_token={Uri.EscapeDataString(_options.EffectiveInstagramToken)}";

            using var response = await _http.GetAsync(url, ct);
            var json = await ReadJsonOrThrowAsync(response, "Instagram container status", ct);

            var status = json.TryGetProperty("status_code", out var s) ? s.GetString() : null;

            switch (status)
            {
                case "FINISHED":
                    return;
                case "ERROR":
                    var detail = json.TryGetProperty("status", out var d) ? d.GetString() : "no detail given";
                    throw new InvalidOperationException($"Instagram could not process the image: {detail}");
                case "EXPIRED":
                    throw new InvalidOperationException("The Instagram media container expired before publishing.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }

        throw new TimeoutException(
            $"Instagram did not finish processing within {_options.InstagramContainerTimeoutSeconds}s. " +
            "The usual cause is that it could not download the image URL.");
    }

    private async Task<string> PublishInstagramContainerAsync(string creationId, CancellationToken ct)
    {
        var url = $"https://graph.facebook.com/{_options.GraphApiVersion}/{_options.InstagramUserId}/media_publish";

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["creation_id"] = creationId,
            ["access_token"] = _options.EffectiveInstagramToken
        });

        using var response = await _http.PostAsync(url, form, ct);
        var json = await ReadJsonOrThrowAsync(response, "Instagram media_publish", ct);

        return json.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
    }

    // --------------------------------------------------------------- copy

    private static string BuildFacebookMessage(ReviewSubmission review)
    {
        var stars = new string('\u2b50', Math.Clamp(review.Rating, 1, 5));
        var origin = string.IsNullOrWhiteSpace(review.Country) ? "" : $" ({review.Country.Trim()})";

        return $"""
                {stars}  A note from one of our guests

                "{review.ReviewText.Trim()}"

                — {review.CustomerName.Trim()}{origin}

                Thank you for staying with us. Hotel UK Passikudah, on the calm side of the bay.
                #HotelUKPassikudah #Passikudah #SriLanka #EastCoast
                """;
    }

    private static string BuildInstagramCaption(ReviewSubmission review)
    {
        var stars = new string('\u2b50', Math.Clamp(review.Rating, 1, 5));

        return $"""
                {stars} "{review.ReviewText.Trim()}" — {review.CustomerName.Trim()}

                Thank you for staying with us.
                #HotelUKPassikudah #Passikudah #PasikudaBeach #SriLanka #EastCoastSriLanka #GuestReview
                """;
    }

    // ------------------------------------------------------------- plumbing

    private async Task<JsonElement> ReadJsonOrThrowAsync(HttpResponseMessage response, string step, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(body).RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"{step} returned a non-JSON response: {Trim(body)}");
        }

        if (root.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("message", out var m) ? m.GetString() : "unknown error";
            var code = error.TryGetProperty("code", out var c) ? c.ToString() : "?";
            throw new InvalidOperationException($"{step} failed (code {code}): {message}");
        }

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{step} failed with HTTP {(int)response.StatusCode}: {Trim(body)}");

        return root;
    }

    /// <summary>Deletes stray PNGs older than ten minutes, in case a publish crashed mid-way.</summary>
    private void CleanUpOldImages()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-10);
            foreach (var file in Directory.EnumerateFiles(_imageFolder, "*.png"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff) TryDelete(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Temp image cleanup skipped.");
        }
    }

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogDebug(ex, "Could not delete {Path}.", path); }
    }

    private static void Require(string value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Configuration '{settingName}' is missing.");
    }

    private static string Trim(string value) =>
        value.Length <= 400 ? value : value[..400] + "…";
}
