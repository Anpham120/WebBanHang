using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Data;
using WebBanHang.Models;

namespace WebBanHang.Controllers;

public class OrdersController : Controller
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var orders = await _db.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Id)
            .ToListAsync();
        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        return order is null ? NotFound() : View(order);
    }

    public async Task<IActionResult> Create()
    {
        var model = new OrderCreateViewModel();
        ShowProducts(model, await LoadAvailableProductsAsync());
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OrderCreateViewModel model)
    {
        var products = await LoadAvailableProductsAsync();
        var byId = products.ToDictionary(p => p.Id);
        var chosen = model.Lines.Where(l => l.Quantity > 0).ToList();

        if (chosen.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng chọn ít nhất một sản phẩm.");
        }

        foreach (var line in chosen)
        {
            if (!byId.TryGetValue(line.ProductId, out var product))
            {
                ModelState.AddModelError(string.Empty, "Có sản phẩm không còn tồn tại hoặc đã hết hàng.");
            }
            else if (line.Quantity > product.Stock)
            {
                ModelState.AddModelError(string.Empty, $"\"{product.Name}\" chỉ còn {product.Stock} sản phẩm.");
            }
        }

        if (!ModelState.IsValid)
        {
            ShowProducts(model, products);
            return View(model);
        }

        var order = new Order
        {
            CustomerName = model.CustomerName,
            Phone = model.Phone,
            Address = model.Address,
            OrderDate = DateTime.Now,
            Status = OrderStatus.Pending,
        };

        foreach (var line in chosen)
        {
            var product = byId[line.ProductId];
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = line.Quantity,
            });
            product.Stock -= line.Quantity;
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Đã tạo đơn hàng #{order.Id}.";
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null)
        {
            return NotFound();
        }

        if (!Enum.IsDefined(status))
        {
            return BadRequest();
        }

        order.Status = status;
        await _db.SaveChangesAsync();
        TempData["Message"] = "Đã cập nhật trạng thái đơn hàng.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private Task<List<Product>> LoadAvailableProductsAsync() =>
        _db.Products.Where(p => p.Stock > 0).OrderBy(p => p.Name).ToListAsync();

    // One form row per available product, keeping any quantities the user already typed.
    private static void ShowProducts(OrderCreateViewModel model, List<Product> products)
    {
        var typed = model.Lines
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.First().Quantity);

        model.Lines = products
            .Select(p => new OrderLineInput
            {
                ProductId = p.Id,
                Product = p,
                Quantity = typed.GetValueOrDefault(p.Id),
            })
            .ToList();
    }
}
