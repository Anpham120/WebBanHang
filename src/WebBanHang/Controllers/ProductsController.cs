using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Data;
using WebBanHang.Models;

namespace WebBanHang.Controllers;

public class ProductsController : Controller
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q, int? categoryId)
    {
        var query = _db.Products.Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Name, pattern));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        ViewBag.Query = q;
        await LoadCategoriesAsync(categoryId);
        return View(await query.OrderBy(p => p.Name).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        await LoadCategoriesAsync();
        return View(new Product());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Description,Price,Stock,CategoryId")] Product product)
    {
        await ValidateCategoryAsync(product.CategoryId);
        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync(product.CategoryId);
            return View(product);
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Đã thêm sản phẩm \"{product.Name}\".";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        await LoadCategoriesAsync(product.CategoryId);
        return View(product);
    }

    [HttpPost, ActionName("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPost(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        var updated = await TryUpdateModelAsync(product, "",
            p => p.Name, p => p.Description, p => p.Price, p => p.Stock, p => p.CategoryId);
        await ValidateCategoryAsync(product.CategoryId);
        if (!updated || !ModelState.IsValid)
        {
            await LoadCategoriesAsync(product.CategoryId);
            return View(product);
        }

        await _db.SaveChangesAsync();
        TempData["Message"] = "Đã cập nhật sản phẩm.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        if (await _db.OrderItems.AnyAsync(i => i.ProductId == id))
        {
            TempData["Error"] = $"Không thể xóa sản phẩm \"{product.Name}\" vì đã có trong đơn hàng.";
            return RedirectToAction(nameof(Index));
        }

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Đã xóa sản phẩm \"{product.Name}\".";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadCategoriesAsync(int? selectedId = null)
    {
        var categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.Categories = new SelectList(categories, nameof(Category.Id), nameof(Category.Name), selectedId);
    }

    private async Task ValidateCategoryAsync(int categoryId)
    {
        if (categoryId > 0 && !await _db.Categories.AnyAsync(c => c.Id == categoryId))
        {
            ModelState.AddModelError(nameof(Product.CategoryId), "Danh mục không tồn tại.");
        }
    }
}
