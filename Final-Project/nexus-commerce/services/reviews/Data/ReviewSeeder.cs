using Microsoft.EntityFrameworkCore;
using Nexus.Contracts;
using Nexus.Reviews.Domain;

namespace Nexus.Reviews.Data;

public static class ReviewSeeder
{
    private static readonly string[] Names = ["Sara M.", "Omar K.", "Layla H.", "Daniel R.", "Mina A.", "Yusuf T.", "Hannah B.", "Karim S."];
    private static readonly (int Rating, string Title, string Body)[] Templates =
    [
        (5, "Excellent quality", "Exactly as described and better than I expected. Would happily buy again."),
        (4, "Very good value", "Does everything I needed. Small quibbles, but great for the price."),
        (5, "Highly recommended", "Setup was easy and it has been reliable since the first day."),
        (3, "Decent, not perfect", "Works fine overall, though a couple of details could be improved."),
        (4, "Solid purchase", "Well built and comfortable to use every day. Delivery was quick too.")
    ];

    public static async Task SeedAsync(ReviewDb db)
    {
        if (await db.Reviews.AnyAsync()) return;
        for (var n = 1; n <= 32; n++)
        {
            var count = 1 + n % 3; // 1..3 sample reviews per product
            for (var k = 0; k < count; k++)
            {
                var t = Templates[(n + k * 2) % Templates.Length];
                db.Reviews.Add(new Review
                {
                    ProductId = SeedIds.Product(n), UserId = SeedIds.User(1 + (n + k * 3) % Names.Length),
                    UserName = Names[(n + k * 3) % Names.Length], Rating = t.Rating, Title = t.Title, Body = t.Body,
                    VerifiedPurchase = true, CreatedAtUtc = DateTime.UtcNow.AddDays(-(n + k * 5))
                });
            }
        }
        await db.SaveChangesAsync();
    }
}
