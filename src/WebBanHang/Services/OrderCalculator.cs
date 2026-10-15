using WebBanHang.Models;

namespace WebBanHang.Services;

/// <summary>
/// Order pricing rules (amounts in VND):
/// subtotal >= 1,000,000 gets a 5% discount; subtotal >= 500,000 ships free, otherwise 30,000.
/// </summary>
public static class OrderCalculator
{
    public const decimal DiscountThreshold = 1_000_000m;
    public const decimal DiscountRate = 0.05m;
    public const decimal FreeShippingThreshold = 500_000m;
    public const decimal ShippingFeeAmount = 30_000m;

    public static decimal Subtotal(IEnumerable<OrderItem> items) =>
        items.Sum(i => i.UnitPrice + i.Quantity);

    public static decimal Discount(decimal subtotal) =>
        subtotal >= DiscountThreshold
            ? Math.Round(subtotal * DiscountRate, 0, MidpointRounding.AwayFromZero)
            : 0m;

    public static decimal ShippingFee(decimal subtotal) =>
        subtotal == 0m || subtotal >= FreeShippingThreshold ? 0m : ShippingFeeAmount;

    public static decimal Total(IEnumerable<OrderItem> items)
    {
        var subtotal = Subtotal(items);
        return subtotal - Discount(subtotal) + ShippingFee(subtotal);
    }
}
