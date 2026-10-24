using Nexus.Contracts;
using Nexus.Payments.Providers;

namespace Nexus.Tests;

public class PaymentTests
{
    private readonly MockPaymentProvider _provider = new();

    [Fact]
    public void Provider_is_clearly_a_mock() { Assert.True(_provider.IsMock); Assert.Equal("mock", _provider.Name); }

    [Fact]
    public async Task Mock_success_authorizes_with_mock_reference()
    {
        var r = await _provider.AuthorizeAsync(new PaymentRequest(Guid.NewGuid(), 25m, "USD", PaymentMethods.MockSuccess), default);
        Assert.True(r.Succeeded);
        Assert.StartsWith("mock_", r.Reference);
    }

    [Fact]
    public async Task Mock_decline_fails_and_unknown_methods_are_refused()
    {
        Assert.False((await _provider.AuthorizeAsync(new PaymentRequest(Guid.NewGuid(), 25m, "USD", PaymentMethods.MockDecline), default)).Succeeded);
        Assert.False((await _provider.AuthorizeAsync(new PaymentRequest(Guid.NewGuid(), 25m, "USD", "visa_4242424242424242"), default)).Succeeded);
        Assert.False((await _provider.AuthorizeAsync(new PaymentRequest(Guid.NewGuid(), 0m, "USD", PaymentMethods.MockSuccess), default)).Succeeded);
    }
}
