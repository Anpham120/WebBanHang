using WebBanHang.Models;
using WebBanHang.Services;

namespace WebBanHang.Tests;

public class OrderCalculatorTests
{
    private static OrderItem Item(decimal unitPrice, int quantity) =>
        new() { UnitPrice = unitPrice, Quantity = quantity };

    [Fact]
    public void Subtotal_SumsUnitPriceTimesQuantity()
    {
        var items = new[] { Item(100_000m, 2), Item(50_000m, 3) };

        Assert.Equal(350_000m, OrderCalculator.Subtotal(items));
    }

    [Fact]
    public void Subtotal_NoItems_IsZero()
    {
        Assert.Equal(0m, OrderCalculator.Subtotal(Array.Empty<OrderItem>()));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(999_999, 0)]
    [InlineData(1_000_000, 50_000)]
    [InlineData(2_000_000, 100_000)]
    [InlineData(1_000_010, 50_001)] // 50,000.5 rounds away from zero
    public void Discount_Is5PercentFromOneMillion(int subtotal, int expected)
    {
        Assert.Equal((decimal)expected, OrderCalculator.Discount(subtotal));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 30_000)]
    [InlineData(499_999, 30_000)]
    [InlineData(500_000, 0)]
    [InlineData(2_000_000, 0)]
    public void ShippingFee_IsFreeFromFiveHundredThousand(int subtotal, int expected)
    {
        Assert.Equal((decimal)expected, OrderCalculator.ShippingFee(subtotal));
    }

    [Fact]
    public void Total_SmallOrder_AddsShippingFee()
    {
        var items = new[] { Item(200_000m, 1) };

        Assert.Equal(230_000m, OrderCalculator.Total(items));
    }

    [Fact]
    public void Total_MediumOrder_ShipsFreeWithoutDiscount()
    {
        var items = new[] { Item(300_000m, 2) };

        Assert.Equal(600_000m, OrderCalculator.Total(items));
    }

    [Fact]
    public void Total_LargeOrder_AppliesDiscountAndFreeShipping()
    {
        var items = new[] { Item(1_000_000m, 2) };

        Assert.Equal(1_900_000m, OrderCalculator.Total(items));
    }

    [Fact]
    public void Total_NoItems_IsZero()
    {
        Assert.Equal(0m, OrderCalculator.Total(Array.Empty<OrderItem>()));
    }

    [Fact]
    public void Order_ExposesCalculatedAmounts()
    {
        var order = new Order
        {
            Items = { Item(1_500_000m, 1), Item(500_000m, 2) }, // subtotal 2,500,000
        };

        Assert.Equal(2_500_000m, order.Subtotal);
        Assert.Equal(125_000m, order.Discount);
        Assert.Equal(0m, order.ShippingFee);
        Assert.Equal(2_375_000m, order.Total);
    }
}
