using HotelUK.Reviews.Api.Models;
using HotelUK.Reviews.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace HotelUK.Reviews.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ReviewsController : ControllerBase
{
    private readonly MetaPublisherService _publisher;
    private readonly ReviewImageGenerator _imageGenerator;
    private readonly MetaOptions _options;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(
        MetaPublisherService publisher,
        ReviewImageGenerator imageGenerator,
        IOptions<MetaOptions> options,
        ILogger<ReviewsController> logger)
    {
        _publisher = publisher;
        _imageGenerator = imageGenerator;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Receives a review and forwards it to Facebook and Instagram. Nothing is saved.
    /// A rating below Meta:MinimumRatingToPublish goes to the hotel privately instead.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("reviews")]
    public async Task<IActionResult> Submit([FromBody] ReviewSubmission review, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        // Honeypot: a bot filled the hidden field. Look successful, do nothing.
        if (!string.IsNullOrWhiteSpace(review.Website))
        {
            _logger.LogInformation("Honeypot triggered; submission dropped.");
            return Accepted(new PublishResult());
        }

        _logger.LogInformation("Review received from {Name}, {Rating} stars.",
                               review.CustomerName, review.Rating);

        var result = await _publisher.PublishAsync(review, ct);
        return Accepted(result);
    }

    /// <summary>
    /// Renders the Instagram graphic and returns it as a PNG, without posting anything.
    /// Handy while you tune the design:
    /// /api/reviews/preview?name=Amara%20Silva&amp;rating=5&amp;text=Perfect%20stay
    ///
    /// Anyone who knows this address can put any words they like on the hotel's
    /// branding, so set Meta:PreviewEnabled to false once the design is settled.
    /// </summary>
    [HttpGet("preview")]
    [EnableRateLimiting("reviews")]
    public IActionResult Preview(
        [FromQuery] string name = "Amara Silva",
        [FromQuery] int rating = 5,
        [FromQuery] string text = "We woke up to the calmest water we have ever seen. The staff remembered our names by the second morning, and breakfast on the terrace was the best part of every day.",
        [FromQuery] string? country = "United Kingdom")
    {
        if (!_options.PreviewEnabled)
        {
            return NotFound();
        }

        var png = _imageGenerator.Render(new ReviewSubmission
        {
            CustomerName = Truncate(name, 60),
            Rating = Math.Clamp(rating, 1, 5),
            ReviewText = Truncate(text, 1200),
            Country = country is null ? null : Truncate(country, 40)
        });

        return File(png, "image/png");
    }

    /// <summary>Quick check that the deployed app has its configuration. Never returns tokens.</summary>
    [HttpGet("health")]
    public IActionResult Health() => Ok(new
    {
        status = "ok",
        graphApiVersion = _options.GraphApiVersion,
        facebookConfigured = !string.IsNullOrWhiteSpace(_options.PageId)
                             && !string.IsNullOrWhiteSpace(_options.PageAccessToken),
        instagramConfigured = !string.IsNullOrWhiteSpace(_options.InstagramUserId)
                              && !string.IsNullOrWhiteSpace(_options.EffectiveInstagramToken),
        privateFeedbackConfigured = !string.IsNullOrWhiteSpace(_options.PrivateFeedbackWebhookUrl),
        publicBaseUrl = _options.PublicBaseUrl,
        minimumRatingToPublish = _options.MinimumRatingToPublish,
        previewEnabled = _options.PreviewEnabled
    });

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
