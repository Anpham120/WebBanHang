using System.ComponentModel.DataAnnotations;
using WebBanHang.Services;

namespace WebBanHang.Models;

public class Order
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên khách hàng")]
    [StringLength(100)]
    [Display(Name = "Khách hàng")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng")]
    [StringLength(300)]
    [Display(Name = "Địa chỉ giao hàng")]
    public string Address { get; set; } = string.Empty;

    [Display(Name = "Ngày đặt")]
    public DateTime OrderDate { get; set; }

    [Display(Name = "Trạng thái")]
    public OrderStatus Status { get; set; }

    public List<OrderItem> Items { get; set; } = new();

    // Derived from the items, never stored: money logic lives only in OrderCalculator.
    [Display(Name = "Tạm tính")]
    public decimal Subtotal => OrderCalculator.Subtotal(Items);

    [Display(Name = "Giảm giá")]
    public decimal Discount => OrderCalculator.Discount(Subtotal);

    [Display(Name = "Phí vận chuyển")]
    public decimal ShippingFee => OrderCalculator.ShippingFee(Subtotal);

    [Display(Name = "Tổng tiền")]
    public decimal Total => OrderCalculator.Total(Items);
}
