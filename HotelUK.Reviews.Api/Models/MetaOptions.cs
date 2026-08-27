namespace HotelUK.Reviews.Api.Models;

/// <summary>
/// Bound from the "Meta" section of appsettings.json.
/// On Heroku the same values come from Config Vars named Meta__PageId,
/// Meta__PageAccessToken, and so on (double underscore = the ":" separator).
/// </summary>
public sealed class MetaOptions
{
    /// <summary>Graph API version. Current as of writing: v25.0 (released Feb 2026).</summary>
    public string GraphApiVersion { get; set; } = "v25.0";

    /// <summary>Numeric ID of your Facebook Page.</summary>
    public string PageId { get; set; } = string.Empty;

    /// <summary>Long-lived Page Access Token (never expires while the app stays live).</summary>
    public string PageAccessToken { get; set; } = string.Empty;

    /// <summary>Numeric ID of the Instagram Business account linked to the Page.</summary>
    public string InstagramUserId { get; set; } = string.Empty;

    /// <summary>
    /// Usually identical to PageAccessToken. Kept separate so you can rotate one
    /// without touching the other.
    /// </summary>
    public string InstagramAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// The public https address of THIS app, with no trailing slash,
    /// e.g. https://hotel-uk-reviews.herokuapp.com
    /// Instagram downloads the generated graphic from this address, so it must be
    /// reachable from the open internet. localhost will not work.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>Reviews below this score are accepted and thanked, but not published.</summary>
    public int MinimumRatingToPublish { get; set; } = 4;

    /// <summary>
    /// Where a review that is too low to publish is sent instead. Any address that
    /// accepts a JSON POST works: a Slack or Google Chat incoming webhook, a Discord
    /// webhook, Zapier, Make, n8n. Leave it empty and the review is written to the
    /// application log only — which means you have to go and read the log to find it.
    ///
    /// The review page tells the guest their words go to the hotel, so set this up.
    /// </summary>
    public string PrivateFeedbackWebhookUrl { get; set; } = string.Empty;

    public bool PostToFacebook { get; set; } = true;
    public bool PostToInstagram { get; set; } = true;

    /// <summary>Seconds to wait for Instagram to finish downloading the image.</summary>
    public int InstagramContainerTimeoutSeconds { get; set; } = 45;

    /// <summary>
    /// Whether /api/reviews/preview will draw a graphic. It is open to anyone who
    /// knows the address, and it puts arbitrary text on the hotel's branding, so
    /// turn it off once you are happy with the design.
    /// </summary>
    public bool PreviewEnabled { get; set; } = true;

    /// <summary>How many submissions one internet address may send in an hour.</summary>
    public int MaxSubmissionsPerHourPerIp { get; set; } = 6;

    public string EffectiveInstagramToken =>
        string.IsNullOrWhiteSpace(InstagramAccessToken) ? PageAccessToken : InstagramAccessToken;
}
