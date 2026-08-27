using System.ComponentModel.DataAnnotations;

namespace HotelUK.Reviews.Api.Models;

/// <summary>
/// Exactly what the browser sends. Nothing here is ever written to a database —
/// it lives in memory for the few seconds it takes to push it to Meta.
/// </summary>
public sealed class ReviewSubmission
{
    [Required(ErrorMessage = "Please tell us your name.")]
    [StringLength(60, MinimumLength = 2)]
    public string CustomerName { get; set; } = string.Empty;

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; set; }

    [Required(ErrorMessage = "Please write a few words about your stay.")]
    [StringLength(1200, MinimumLength = 10)]
    public string ReviewText { get; set; } = string.Empty;

    /// <summary>Optional. Printed small on the Instagram graphic, e.g. "United Kingdom".</summary>
    [StringLength(40)]
    public string? Country { get; set; }

    /// <summary>
    /// Honeypot. A real person never sees this field, so it must arrive empty.
    /// If a bot fills it, we pretend everything worked and quietly drop the post.
    /// </summary>
    public string? Website { get; set; }
}
