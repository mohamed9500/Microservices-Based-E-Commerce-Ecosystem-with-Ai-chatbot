namespace Nexus.Contracts;

/// <summary>Deterministic ids so independent service databases can seed consistent development data.</summary>
public static class SeedIds
{
    public static Guid Product(int n) => new($"00000000-0000-4000-8000-{n:D12}");
    public static Guid Category(int n) => new($"00000000-0000-4000-9000-{n:D12}");
    public static Guid User(int n) => new($"00000000-0000-4000-a000-{n:D12}");
}

public static class PaymentMethods
{
    public const string MockSuccess = "mock_success";
    public const string MockDecline = "mock_decline";
    public static readonly string[] All = [MockSuccess, MockDecline];
}
