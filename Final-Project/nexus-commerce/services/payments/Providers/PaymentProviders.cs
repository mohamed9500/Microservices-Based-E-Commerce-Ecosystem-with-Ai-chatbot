using Nexus.Contracts;

namespace Nexus.Payments.Providers;

public sealed record PaymentRequest(Guid OrderId, decimal Amount, string Currency, string PaymentMethod);
public sealed record PaymentResult(bool Succeeded, string? Reference, string? FailureReason);

/// <summary>Abstraction over a payment gateway. Only the mock implementation ships with this project.</summary>
public interface IPaymentProvider
{
    string Name { get; }
    bool IsMock { get; }
    Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct);
    Task VoidAsync(string reference, CancellationToken ct);
}

/// <summary>
/// DEVELOPMENT ONLY. Simulates authorisation: "mock_success" always succeeds, "mock_decline" always fails.
/// It never contacts a payment network, never charges anyone and never sees card data.
/// </summary>
public sealed class MockPaymentProvider : IPaymentProvider
{
    public string Name => "mock";
    public bool IsMock => true;

    public Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0) return Task.FromResult(new PaymentResult(false, null, "Invalid amount."));
        return Task.FromResult(request.PaymentMethod switch
        {
            PaymentMethods.MockSuccess => new PaymentResult(true, $"mock_{Guid.NewGuid():N}", null),
            PaymentMethods.MockDecline => new PaymentResult(false, null, "Simulated decline (mock provider)."),
            _ => new PaymentResult(false, null, "Unsupported payment method.")
        });
    }

    public Task VoidAsync(string reference, CancellationToken ct) => Task.CompletedTask;
}
