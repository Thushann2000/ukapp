namespace HotelUK.Reviews.Api.Models;

public sealed class PublishResult
{
    public bool Accepted { get; set; } = true;
    public bool FacebookPublished { get; set; }
    public bool InstagramPublished { get; set; }
    public string? FacebookPostId { get; set; }
    public string? InstagramMediaId { get; set; }
    public List<string> Warnings { get; } = new();
}
