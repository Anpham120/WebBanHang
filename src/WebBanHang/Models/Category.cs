using System.ComponentModel.DataAnnotations;

namespace WebBanHang.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên danh mục")]
    [StringLength(100)]
    [Display(Name = "Tên danh mục")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    public List<Product> Products { get; set; } = new();
}
