using System.ComponentModel.DataAnnotations;

namespace WebBanHang.Models;

public class OrderCreateViewModel
{
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

    public List<OrderLineInput> Lines { get; set; } = new();
}

public class OrderLineInput
{
    public int ProductId { get; set; }

    [Range(0, 1000, ErrorMessage = "Số lượng không hợp lệ")]
    public int Quantity { get; set; }

    // Display only: filled by the controller, never read from the posted form.
    public Product? Product { get; set; }
}
