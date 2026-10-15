using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Data;

// Same number parsing/formatting whatever the server's regional settings are.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// The SQLite file lives in App_Data, which the deploy script never overwrites.
// Override with ConnectionStrings:Default (e.g. in appsettings.Production.json on the server).
var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDir);
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? $"Data Source={Path.Combine(dataDir, "webbanhang.db")}";

builder.Services.AddControllersWithViews();
// Emit Vietnamese letters as-is instead of &#x1EE5; entities.
builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

var app = builder.Build();

// Create the database / apply new migrations; existing data is kept.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

// IIS sets the PathBase (/webbanhang) by itself. Set "PathBase" only to simulate it
// when running outside IIS, e.g.: dotnet run --PathBase=/webbanhang
var pathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrEmpty(pathBase))
{
    app.UsePathBase(pathBase);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
