using System.ComponentModel.DataAnnotations;

namespace Nexus.Reviews.Domain;

public sealed class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public int Rating { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsVisible { get; set; } = true;
    public bool VerifiedPurchase { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed record CreateReviewRequest
{
    public Guid ProductId { get; init; }
    [Range(1, 5)] public int Rating { get; init; }
    [Required, StringLength(120, MinimumLength = 2)] public string Title { get; init; } = "";
    [Required, StringLength(2000, MinimumLength = 5)] public string Body { get; init; } = "";
}

public sealed record VisibilityRequest { public bool IsVisible { get; init; } }

public sealed record ReviewDto(Guid Id, Guid ProductId, string UserName, int Rating, string Title, string Body,
    bool VerifiedPurchase, DateTime CreatedAtUtc, bool? IsVisible = null);

public sealed record RatingSummary(Guid ProductId, double Average, int Count);

public static class ReviewMapping
{
    public static ReviewDto ToDto(this Review r, bool admin = false) =>
        new(r.Id, r.ProductId, r.UserName, r.Rating, r.Title, r.Body, r.VerifiedPurchase, r.CreatedAtUtc, admin ? r.IsVisible : null);
}
