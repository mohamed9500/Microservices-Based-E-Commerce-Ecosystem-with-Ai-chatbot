using System.Globalization;
using System.Text.RegularExpressions;
using Nexus.Assistant.Catalog;

namespace Nexus.Assistant.Fallback;

public enum IntentKind { Greeting, Categories, Compare, Search }

public sealed record ParsedIntent(IntentKind Kind, bool IsArabic, decimal? MinPrice, decimal? MaxPrice, string? Sort,
    IReadOnlyList<string> Keywords, IReadOnlyList<string> CompareTerms, bool AsksPrice, bool AsksAvailability);

/// <summary>Deterministic English/Arabic message understanding used by the rule-based assistant (no AI model involved).</summary>
public static partial class IntentParser
{
    private static readonly string[] ArabicToEnglishKeys =
    [
        "لوحة مفاتيح", "فرشاة اسنان", "فرشاة أسنان", "منظم حرارة", "مكبر صوت", "طقم طهي", "كاميرا مراقبة", "قلاية هوائية",
        "لابتوب", "حاسوب", "هاتف", "جوال", "موبايل", "سماعات", "سماعة", "ساعة", "شاشة", "حقيبة", "شنطة", "حذاء", "مكنسة",
        "قهوة", "سبيكر", "ترموستات", "روتر", "مصباح", "كريم", "يوغا", "تابلت", "شاشه", "شاحن", "نظارة", "نظارات", "سيروم", "مجفف"
    ];
    private static readonly string[] ArabicToEnglishValues =
    [
        "keyboard", "toothbrush", "toothbrush", "thermostat", "speaker", "cookware", "camera", "air fryer",
        "laptop", "laptop", "phone", "phone", "phone", "headphones", "headphones", "watch", "monitor", "backpack", "backpack", "shoes", "vacuum",
        "espresso", "speaker", "thermostat", "router", "bulb", "cream", "yoga", "tablet", "monitor", "charger", "sunglasses", "sunglasses", "serum", "hair dryer"
    ];

    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "a","an","the","i","me","my","we","you","it","is","are","am","was","be","to","of","for","in","on","at","with","and","or","but","not",
        "show","find","get","give","recommend","suggest","want","need","looking","look","search","please","can","could","would","should",
        "some","any","good","best","great","nice","one","ones","something","anything","what","which","where","how","much","does","do","have","has",
        "there","that","this","these","those","buy","purchase","like","about","tell","price","cost","stock","available","availability","cheap",
        "cheapest","expensive","under","below","over","above","than","less","more","most","least","max","maximum","budget","dollars","dollar","usd",
        "compare","comparison","versus","vs","between","difference","better","new","newest","latest","your","store","shop","products","product","items","item","thanks","thank",
        "من","في","على","الى","إلى","عن","مع","هل","ما","ماذا","كم","أريد","اريد","ابحث","أبحث","عندكم","لدي","لدى","اي","أي","افضل","أفضل","سعر","متوفر","متاح","دولار","اقل","أقل","اكثر","أكثر","و","او","أو","لو","سمحت","ممكن","اعطني","أعطني","اقترح"
    };

    [GeneratedRegex(@"(?:under|below|less than|cheaper than|up to|max(?:imum)?(?: of)?|within|budget(?: of| is)?|not more than|at most|أقل من|اقل من|تحت|حتى|لا تزيد عن|ميزانيتي|بحدود)\s*(?:\$|usd)?\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex MaxRegex();
    [GeneratedRegex(@"(?:over|above|more than|at least|starting at|أكثر من|اكثر من|فوق|أعلى من)\s*(?:\$|usd)?\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex MinRegex();
    [GeneratedRegex(@"(?:between|من)\s*\$?(\d+(?:\.\d+)?)\s*(?:and|to|-|الى|إلى)\s*\$?(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex BetweenRegex();
    [GeneratedRegex(@"(?:\$\s*(\d+(?:\.\d+)?))|(?:(\d+(?:\.\d+)?)\s*(?:dollars?|usd|دولار))", RegexOptions.IgnoreCase)]
    private static partial Regex BareMoneyRegex();
    [GeneratedRegex(@"\b(?:compare|comparison|versus|vs\.?|difference between|which is better)\b|قارن|مقارنة|الفرق بين|ايهما افضل|أيهما أفضل", RegexOptions.IgnoreCase)]
    private static partial Regex CompareRegex();
    [GeneratedRegex(@"\s+(?:and|vs\.?|versus|with|or|to)\s+|\s+و\s+|\s+او\s+|\s+أو\s+|[,،]", RegexOptions.IgnoreCase)]
    private static partial Regex CompareSplitRegex();
    [GeneratedRegex(@"^\s*(?:hi|hello|hey|good (?:morning|evening|afternoon)|مرحبا|اهلا|أهلا|السلام عليكم|هلا|help|مساعدة)\b", RegexOptions.IgnoreCase)]
    private static partial Regex GreetingRegex();
    [GeneratedRegex(@"\b(?:categories|category list|what do you sell|what do you have|browse|departments)\b|الفئات|الأقسام|ماذا تبيع|ايش عندكم", RegexOptions.IgnoreCase)]
    private static partial Regex CategoriesRegex();
    [GeneratedRegex(@"\b(?:in stock|available|availability|stock)\b|متوفر|متاح|مخزون|موجود", RegexOptions.IgnoreCase)]
    private static partial Regex AvailabilityRegex();
    [GeneratedRegex(@"\b(?:price|cost|how much)\b|كم سعر|سعر|بكم|كم ثمن", RegexOptions.IgnoreCase)]
    private static partial Regex PriceRegex();
    [GeneratedRegex(@"[\p{L}\p{N}]+")] private static partial Regex WordRegex();

    public static bool ContainsArabic(string s) => s.Any(c => c is >= '\u0600' and <= '\u06FF');

    public static string Normalize(string input)
    {
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (c is >= '\u0660' and <= '\u0669') sb.Append((char)('0' + (c - '\u0660')));       // Arabic-Indic digits
            else if (c is >= '\u06F0' and <= '\u06F9') sb.Append((char)('0' + (c - '\u06F0')));  // Persian digits
            else if (c == '\u066B') sb.Append('.');
            else sb.Append(c);
        }
        return sb.ToString().ToLowerInvariant().Trim();
    }

    public static ParsedIntent Parse(string message)
    {
        var arabic = ContainsArabic(message);
        var text = Normalize(message);
        for (var i = 0; i < ArabicToEnglishKeys.Length; i++)
            text = text.Replace(ArabicToEnglishKeys[i], " " + ArabicToEnglishValues[i] + " ");

        decimal? min = null, max = null;
        var between = BetweenRegex().Match(text);
        if (between.Success)
        {
            min = Dec(between.Groups[1].Value); max = Dec(between.Groups[2].Value);
            if (min > max) (min, max) = (max, min);
        }
        else
        {
            var mx = MaxRegex().Match(text); if (mx.Success) max = Dec(mx.Groups[1].Value);
            var mn = MinRegex().Match(text); if (mn.Success) min = Dec(mn.Groups[1].Value);
            if (max == null && min == null)
            {
                var bare = BareMoneyRegex().Match(text);
                if (bare.Success) max = Dec(bare.Groups[1].Success ? bare.Groups[1].Value : bare.Groups[2].Value); // "$200" implies a budget
            }
        }

        string? sort = null;
        if (Regex.IsMatch(text, @"\b(cheapest|lowest price|least expensive)\b|الأرخص|ارخص|الاقل سعرا")) sort = "price_asc";
        else if (Regex.IsMatch(text, @"\b(most expensive|premium|highest price)\b|الأغلى|اغلى")) sort = "price_desc";
        else if (Regex.IsMatch(text, @"\b(newest|latest|new arrivals)\b|الأحدث|احدث")) sort = "newest";

        var asksPrice = PriceRegex().IsMatch(text);
        var asksAvail = AvailabilityRegex().IsMatch(text);

        // Strip money phrases before extracting keywords.
        var stripped = MaxRegex().Replace(MinRegex().Replace(BetweenRegex().Replace(BareMoneyRegex().Replace(text, " "), " "), " "), " ");
        var keywords = ExtractKeywords(stripped);

        if (CompareRegex().IsMatch(text))
        {
            var terms = CompareSplitRegex().Split(CompareRegex().Replace(text, " "))
                .Select(t => string.Join(' ', ExtractKeywords(t))).Where(t => t.Length > 1).Distinct().Take(4).ToList();
            return new ParsedIntent(IntentKind.Compare, arabic, min, max, sort, keywords, terms, asksPrice, asksAvail);
        }
        if (CategoriesRegex().IsMatch(text))
            return new ParsedIntent(IntentKind.Categories, arabic, min, max, sort, keywords, [], false, false);
        if (GreetingRegex().IsMatch(text) && keywords.Count == 0 && min == null && max == null)
            return new ParsedIntent(IntentKind.Greeting, arabic, null, null, null, [], [], false, false);
        return new ParsedIntent(IntentKind.Search, arabic, min, max, sort, keywords, [], asksPrice, asksAvail);
    }

    public static List<string> ExtractKeywords(string text) =>
        WordRegex().Matches(text).Select(m => m.Value.ToLowerInvariant())
            .Where(w => w.Length > 1 && !Stop.Contains(w) && !w.All(char.IsDigit) && !ContainsArabic(w))
            .Select(Stem).Distinct().Take(6).ToList();

    private static string Stem(string w) => w.Length > 4 && w.EndsWith('s') && !w.EndsWith("ss") ? w[..^1] : w;
    private static decimal? Dec(string s) => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
}
