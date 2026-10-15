using System.ComponentModel.DataAnnotations;

namespace WebBanHang.Models;

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
    [StringLength(200)]
    [Display(Name = "Tên sản phẩm")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Range(0, 1_000_000_000, ErrorMessage = "Giá phải từ 0 đến 1.000.000.000")]
    [Display(Name = "Giá (VND)")]
    public decimal Price { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Số lượng tồn kho không hợp lệ")]
    [Display(Name = "Tồn kho")]
    public int Stock { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục")]
    [Display(Name = "Danh mục")]
    public int CategoryId { get; set; }

    public Category? Category { get; set; }
}
