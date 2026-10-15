using System.ComponentModel.DataAnnotations;

namespace WebBanHang.Models;

public enum OrderStatus
{
    [Display(Name = "Chờ xử lý")]
    Pending = 0,

    [Display(Name = "Đã xác nhận")]
    Confirmed = 1,

    [Display(Name = "Đang giao")]
    Shipped = 2,

    [Display(Name = "Hoàn tất")]
    Completed = 3,
}
