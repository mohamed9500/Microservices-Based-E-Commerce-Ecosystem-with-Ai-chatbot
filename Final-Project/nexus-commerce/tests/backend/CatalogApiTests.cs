using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nexus.Catalog;
using Nexus.Tests.Support;

namespace Nexus.Tests;

public class CatalogApiTests(ApiFactory<CatalogApiMarker> factory) : IClassFixture<ApiFactory<CatalogApiMarker>>
{
    
    private sealed record Product(Guid Id, string Name, decimal Price, string CategorySlug, string Brand);
    private sealed record Category(Guid Id, string Name, string Slug);

    private HttpClient Anonymous() => factory.CreateClient();
    private HttpClient As(string token) => factory.CreateClient().WithBearer(token);

    private static async Task<(List<Product> Items, int Total)> List(HttpClient c, string query)
    {
        var res = await c.GetAsync("/api/products" + query);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var doc = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var items = doc.GetProperty("items").Deserialize<List<Product>>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        return (items, doc.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Seed_data_has_at_least_30_products_and_categories()
    {
        var (_, total) = await List(Anonymous(), "?pageSize=5");
        Assert.True(total >= 30);
        var cats = await Anonymous().GetFromJsonAsync<List<Category>>("/api/categories");
        Assert.True(cats!.Count >= 8);
    }

    [Fact]
    public async Task Pagination_is_enforced_and_capped()
    {
        var (items, _) = await List(Anonymous(), "?pageSize=1000");
        Assert.True(items.Count <= 50);
        var (page2, _) = await List(Anonymous(), "?pageSize=5&page=2");
        Assert.Equal(5, page2.Count);
    }

    [Fact]
    public async Task Search_filter_and_sort_work()
    {
        var (headphones, _) = await List(Anonymous(), "?q=Aurora%20Wireless");
        Assert.NotEmpty(headphones);
        Assert.All(headphones, p => Assert.Contains("Aurora Wireless", p.Name));

        var (cheap, _) = await List(Anonymous(), "?maxPrice=50&sort=price_desc&pageSize=50");
        Assert.NotEmpty(cheap);
        Assert.All(cheap, p => Assert.True(p.Price <= 50));
        Assert.Equal(cheap.OrderByDescending(p => p.Price).Select(p => p.Id), cheap.Select(p => p.Id));

        var (audio, _) = await List(Anonymous(), "?category=audio&pageSize=50");
        Assert.NotEmpty(audio);
        Assert.All(audio, p => Assert.Equal("audio", p.CategorySlug));
    }

    [Fact]
    public async Task Writes_require_the_admin_role()
    {
        var body = new { sku = "TST-001", name = "Test Product", description = "A product created by an automated test.", price = 9.99, categoryId = Nexus.Contracts.SeedIds.Category(1), brand = "Test", imageUrl = "/images/products/p01.svg" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous().PostAsJsonAsync("/api/products", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(TestTokens.Create("Customer")).PostAsJsonAsync("/api/products", body)).StatusCode);

        var created = await As(TestTokens.Create("Admin")).PostAsJsonAsync("/api/products", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        // Duplicate SKU is a validation error.
        Assert.Equal(HttpStatusCode.BadRequest, (await As(TestTokens.Create("Admin")).PostAsJsonAsync("/api/products", body)).StatusCode);
    }

    [Fact]
    public async Task Unsafe_image_urls_are_rejected()
    {
        var body = new { sku = "TST-XSS", name = "Bad Image", description = "A product with a hostile image url.", price = 9.99, categoryId = Nexus.Contracts.SeedIds.Category(1), brand = "Test", imageUrl = "javascript:alert(1)" };
        Assert.Equal(HttpStatusCode.BadRequest, (await As(TestTokens.Create("Admin")).PostAsJsonAsync("/api/products", body)).StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_is_public() =>
        Assert.Equal(HttpStatusCode.OK, (await Anonymous().GetAsync("/health")).StatusCode);
}
