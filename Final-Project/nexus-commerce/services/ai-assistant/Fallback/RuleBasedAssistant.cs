using System.Globalization;
using System.Text;
using Nexus.Assistant.Catalog;
using Nexus.Assistant.Domain;

namespace Nexus.Assistant.Fallback;

public sealed record AssistantReply(string Text, IReadOnlyList<ProductFact> Products);

/// <summary>
/// Deterministic shopping assistant used when no LLM is configured (or the provider is unavailable).
/// All product facts come from the live catalog; nothing is invented. It is NOT an AI model and says so in the UI.
/// </summary>
public sealed class RuleBasedAssistant(ICatalogGateway catalog)
{
    public async Task<AssistantReply> RespondAsync(string message, IReadOnlyList<HistoryItem> history, CancellationToken ct)
    {
        var intent = IntentParser.Parse(message);
        var ar = intent.IsArabic;

        switch (intent.Kind)
        {
            case IntentKind.Greeting:
                return new(T(ar,
                    "Hi! I'm the Nexus shopping assistant (basic mode). Tell me what you are looking for, for example \"wireless headphones under 200\", \"compare Aurora X5 and Voltix Air\", or \"is the Haven thermostat in stock?\".",
                    "مرحباً! أنا مساعد التسوق في Nexus (الوضع الأساسي). أخبرني بما تبحث عنه، مثلاً: \"سماعات أقل من 200\" أو \"قارن بين Aurora X5 و Voltix Air\"."), []);

            case IntentKind.Categories:
                var cats = await catalog.GetCategoriesAsync(ct);
                return new(cats.Count == 0
                    ? T(ar, "The catalog is empty right now.", "الكتالوج فارغ حالياً.")
                    : T(ar, "We sell: ", "نبيع: ") + string.Join(", ", cats.Select(c => c.Name)) + T(ar, ". Ask me about any of them.", ". اسألني عن أي فئة."), []);

            case IntentKind.Compare:
                return await CompareAsync(intent, ct);

            default:
                return await SearchAsync(intent, history, ct);
        }
    }

    private async Task<AssistantReply> CompareAsync(ParsedIntent intent, CancellationToken ct)
    {
        var ar = intent.IsArabic;
        var found = new List<ProductFact>();
        foreach (var term in intent.CompareTerms)
        {
            var hit = (await catalog.SearchAsync(new ProductSearch(term, null, null, null, null, 1), ct)).FirstOrDefault();
            if (hit != null && found.All(f => f.Id != hit.Id)) found.Add(hit);
        }
        if (found.Count < 2)
            return new(T(ar,
                "To compare products I need two product names that exist in our catalog, e.g. \"compare Aurora X5 and Nimbus Tab\". I could not match enough products.",
                "للمقارنة أحتاج اسمَي منتجين موجودين في الكتالوج، مثل: \"قارن بين Aurora X5 و Nimbus Tab\". لم أتمكن من العثور على منتجين."), found);

        var sb = new StringBuilder();
        sb.AppendLine(T(ar, "Here is a side-by-side comparison from our catalog:", "هذه مقارنة من الكتالوج:"));
        foreach (var p in found)
            sb.AppendLine($"- {p.Name} ({p.Brand}): {Money(p)} - {Avail(p.Availability, ar)}{RatingText(p, ar)}");
        var sharedKeys = found.Select(p => p.Specs.Keys.ToHashSet()).Aggregate((a, b) => { a.IntersectWith(b); return a; });
        foreach (var key in sharedKeys.Take(5))
            sb.AppendLine($"  {key}: " + string.Join(" | ", found.Select(p => p.Specs[key])));
        var cheapest = found.OrderBy(p => p.Price).First();
        sb.Append(T(ar, $"Lowest price: {cheapest.Name} at {Money(cheapest)}.", $"الأقل سعراً: {cheapest.Name} بسعر {Money(cheapest)}."));
        return new(sb.ToString(), found);
    }

    private async Task<AssistantReply> SearchAsync(ParsedIntent intent, IReadOnlyList<HistoryItem> history, CancellationToken ct)
    {
        var ar = intent.IsArabic;
        var keywords = intent.Keywords.ToList();

        // Follow-ups such as "cheaper ones" / "under 100": reuse the topic of the previous user message.
        if (keywords.Count == 0 && history.LastOrDefault(h => h.Role == "user") is { } prev)
            keywords = IntentParser.Parse(prev.Content).Keywords.ToList();

        string? categorySlug = null;
        var categories = await catalog.GetCategoriesAsync(ct);
        (categorySlug, keywords) = MatchCategory(categories, keywords);

        if (keywords.Count == 0 && categorySlug == null && intent.MaxPrice == null && intent.MinPrice == null && intent.Sort == null)
            return new(T(ar,
                "I can help you find products, compare them, and check price and availability. What are you shopping for?",
                "أستطيع مساعدتك في إيجاد المنتجات ومقارنتها ومعرفة الأسعار والتوفر. ماذا تريد أن تشتري؟"), []);

        var products = await FindAsync(keywords, categorySlug, intent, ct);
        if (products.Count == 0)
        {
            var names = string.Join(", ", categories.Select(c => c.Name));
            return new(T(ar,
                $"I could not find matching products{BudgetText(intent, false)}. Try different words, a higher budget, or ask for our categories ({names}).",
                $"لم أجد منتجات مطابقة{BudgetText(intent, true)}. جرّب كلمات مختلفة أو ميزانية أعلى، أو اسأل عن الفئات."), []);
        }

        var top = products.Take(4).ToList();
        var sb = new StringBuilder();
        sb.AppendLine(intent.AsksAvailability || intent.AsksPrice
            ? T(ar, "Here is the current price and availability:", "هذه الأسعار والتوفر الحالي:")
            : T(ar, $"I found {products.Count} matching product(s){BudgetText(intent, false)}:", $"وجدت {products.Count} منتج(ات) مطابقة{BudgetText(intent, true)}:"));
        var i = 1;
        foreach (var p in top)
            sb.AppendLine($"{i++}. {p.Name} ({p.Brand}) - {Money(p)} - {Avail(p.Availability, ar)}{RatingText(p, ar)}");
        sb.Append(T(ar, "Open a product card below for full details. Ask me to compare any two of them.", "افتح بطاقة المنتج للتفاصيل. يمكنني مقارنة أي منتجين."));
        return new(sb.ToString(), top);
    }

    private async Task<IReadOnlyList<ProductFact>> FindAsync(List<string> keywords, string? category, ParsedIntent intent, CancellationToken ct)
    {
        ProductSearch Make(string? q, int limit) => new(q, category, intent.MinPrice, intent.MaxPrice, intent.Sort, limit);

        if (keywords.Count == 0) return await catalog.SearchAsync(Make(null, 6), ct);

        var all = await catalog.SearchAsync(Make(string.Join(' ', keywords.Take(4)), 6), ct);
        if (all.Count > 0) return all;

        // No product matches every word: rank by how many individual words match.
        var scored = new Dictionary<Guid, (ProductFact P, int Hits)>();
        foreach (var kw in keywords.Take(4))
            foreach (var p in await catalog.SearchAsync(Make(kw, 10), ct))
                scored[p.Id] = (p, scored.TryGetValue(p.Id, out var e) ? e.Hits + 1 : 1);
        var ranked = scored.Values.OrderByDescending(x => x.Hits);
        return (intent.Sort == "price_desc" ? ranked.ThenByDescending(x => x.P.Price)
              : intent.Sort == "price_asc" ? ranked.ThenBy(x => x.P.Price) : ranked.ThenBy(x => x.P.Price))
            .Select(x => x.P).Take(6).ToList();
    }

    /// <summary>A keyword equal to a category word that is unique to one category selects that category.</summary>
    public static (string? Slug, List<string> Remaining) MatchCategory(IReadOnlyList<CategoryFact> categories, List<string> keywords)
    {
        static IEnumerable<string> Words(string name) =>
            name.ToLowerInvariant().Split([' ', '&', '-', ','], StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 4).Select(w => w.Length > 4 && w.EndsWith('s') ? w[..^1] : w);
        foreach (var kw in keywords)
        {
            var matches = categories.Where(c => Words(c.Name).Contains(kw)).ToList();
            if (matches.Count == 1) return (matches[0].Slug, keywords.Where(k => k != kw).ToList());
        }
        return (null, keywords);
    }

    private static string Money(ProductFact p) => p.Price.ToString("0.00", CultureInfo.InvariantCulture) + " " + p.Currency;

    private static string RatingText(ProductFact p, bool ar) =>
        p.Rating is { } r ? (ar ? $" - التقييم {r:0.0}/5" : $" - rated {r:0.0}/5") : "";

    private static string Avail(string status, bool ar) => status switch
    {
        "in_stock" => ar ? "متوفر" : "In stock",
        "low_stock" => ar ? "كمية محدودة" : "Low stock",
        "out_of_stock" => ar ? "غير متوفر" : "Out of stock",
        _ => ar ? "التوفر غير معروف" : "Availability unknown"
    };

    private static string BudgetText(ParsedIntent i, bool ar)
    {
        if (i.MinPrice != null && i.MaxPrice != null) return ar ? $" بين {i.MinPrice:0.##} و {i.MaxPrice:0.##}" : $" between {i.MinPrice:0.##} and {i.MaxPrice:0.##}";
        if (i.MaxPrice != null) return ar ? $" بسعر أقل من {i.MaxPrice:0.##}" : $" under {i.MaxPrice:0.##}";
        if (i.MinPrice != null) return ar ? $" بسعر أعلى من {i.MinPrice:0.##}" : $" over {i.MinPrice:0.##}";
        return "";
    }

    private static string T(bool ar, string en, string arText) => ar ? arText : en;
}
