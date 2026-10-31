using System.Reflection;
using Nexus.BuildingBlocks.Messaging;
using Nexus.BuildingBlocks.Web;
using Nexus.Contracts;
using Nexus.Contracts.Events;
using Nexus.Orders.Domain;

namespace Nexus.Tests;

public class ContractTests
{
    [Fact]
    public void Every_event_has_a_unique_versioned_routing_key()
    {
        var events = typeof(OrderCreated).Assembly.GetTypes().Where(t => t.GetCustomAttribute<EventContractAttribute>() != null).ToList();
        Assert.True(events.Count >= 7);
        var keys = events.Select(t => EventContractAttribute.For(t).RoutingKey).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(keys, k => Assert.Matches(@"^[a-z.\-]+\.v\d+$", k));
    }

    [Fact]
    public void Required_events_exist()
    {
        Assert.Equal("order.created.v1", EventContractAttribute.RoutingKeyFor<OrderCreated>());
        Assert.Equal("inventory.reserved.v1", EventContractAttribute.RoutingKeyFor<InventoryReserved>());
        Assert.Equal("inventory.reservation-failed.v1", EventContractAttribute.RoutingKeyFor<InventoryReservationFailed>());
        Assert.Equal("payment.authorized.v1", EventContractAttribute.RoutingKeyFor<PaymentAuthorized>());
        Assert.Equal("payment.failed.v1", EventContractAttribute.RoutingKeyFor<PaymentFailed>());
        Assert.Equal("order.confirmed.v1", EventContractAttribute.RoutingKeyFor<OrderConfirmed>());
        Assert.Equal("order.cancelled.v1", EventContractAttribute.RoutingKeyFor<OrderCancelled>());
    }

    [Fact]
    public void Seed_ids_are_stable_and_distinct()
    {
        Assert.Equal(SeedIds.Product(7), SeedIds.Product(7));
        Assert.NotEqual(SeedIds.Product(1), SeedIds.Product(2));
        Assert.NotEqual(SeedIds.Product(1), SeedIds.Category(1));
    }

    [Fact]
    public void Paging_normalizes_out_of_range_values()
    {
        Assert.Equal((1, 12), Paging.Normalize(0, null));
        Assert.Equal((3, 50), Paging.Normalize(3, 5000));
        Assert.Equal((1, 1), Paging.Normalize(-4, -9));
    }
}

public class OrderRulesTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.AwaitingPayment, true)]
    [InlineData(OrderStatus.AwaitingPayment, OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed, false)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled, false)]
    public void State_machine_allows_only_valid_transitions(OrderStatus from, OrderStatus to, bool expected) =>
        Assert.Equal(expected, OrderStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(40, 5, 100, 5)]
    [InlineData(100, 5, 100, 0)]
    [InlineData(250, 5, 100, 0)]
    [InlineData(0, 5, 100, 0)]
    public void Shipping_fee_is_free_above_threshold(decimal subtotal, decimal flat, decimal threshold, decimal expected) =>
        Assert.Equal(expected, Pricing.ShippingFor(subtotal, flat, threshold));

    [Fact]
    public void Customers_cannot_cancel_shipped_orders()
    {
        Assert.True(OrderStateMachine.CustomerCanCancel(OrderStatus.Confirmed));
        Assert.False(OrderStateMachine.CustomerCanCancel(OrderStatus.Shipped));
        Assert.False(OrderStateMachine.CustomerCanCancel(OrderStatus.Delivered));
    }
}
