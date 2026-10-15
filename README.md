# WebBanHang

Hệ thống quản lý bán hàng của Công ty ABC xây dựng bằng **ASP.NET Core MVC (.NET 8)**: quản lý **sản phẩm**, **danh mục** và **đơn hàng**.
Dự án dùng để thực hành CI/CD: mỗi lần hợp nhất vào `main`, Jenkins tự động biên dịch, kiểm thử, publish và triển khai lên IIS.

Địa chỉ sau khi triển khai: **http://localhost/webbanhang**

## Quy trình CI/CD

```
Developer ─ feature/* ─ Pull Request ─▶ main ─ webhook ─▶ Jenkins (WebBanHang-CICD)
                                                           1. Checkout   lấy mã nguồn nhánh main
                                                           2. Restore    khôi phục NuGet
                                                           3. Build      dotnet build -c Release
                                                           4. Test       dotnet test (xUnit)
                                                           5. Publish    dotnet publish
                                                           6. Deploy     app_offline.htm → robocopy → C:\wwwroot\webbanhang
                                                           7. Verify     kiểm tra website sau triển khai
```

- Lỗi ở bất kỳ stage nào làm pipeline dừng ngay; lỗi Build hoặc Test thì **không Publish, không Deploy**, IIS vẫn chạy bản cũ.
- Chỉ nhánh `main` được deploy. Nhánh khác (chạy bằng *Build with Parameters → BRANCH*) dừng sau Publish.
- Deploy giữ nguyên dữ liệu và cấu hình của môi trường: thư mục `App_Data` (CSDL SQLite), `logs`, `appsettings.Production.json` không bị ghi đè hay xóa.
- Stage Verify kiểm tra: trang chủ trả 200, link CSS/JS nằm dưới `/webbanhang` (PathBase) và tải được, chân trang hiển thị đúng phiên bản vừa build (`1.0.<số build>`), các trang Sản phẩm / Danh mục / Đơn hàng trả 200.

## Cấu trúc thư mục

| Đường dẫn | Nội dung |
|---|---|
| `Jenkinsfile` | Pipeline khai báo 7 stage |
| `src/WebBanHang/` | Ứng dụng MVC: `Models`, `Data` (EF Core + SQLite), `Services/OrderCalculator.cs` (tính tiền đơn hàng), `Controllers`, `Views` |
| `tests/WebBanHang.Tests/` | Unit test xUnit cho `OrderCalculator` |
| `scripts/setup-server.ps1` | Chuẩn bị máy chủ một lần: .NET 8 SDK, IIS, Hosting Bundle, App Pool, ứng dụng `/webbanhang`, phân quyền |
| `scripts/deploy-iis.ps1` | Deploy bản publish vào IIS (dùng `app_offline.htm`) |
| `scripts/verify-deployment.ps1` | Kiểm tra website sau khi deploy |
| `jenkins/WebBanHang-CICD.config.xml` | Cấu hình job Jenkins để import nhanh |
| `docs/HuongDan-CaiDat.md` | Hướng dẫn cài đặt Jenkins, IIS, GitHub webhook trên máy chủ |
| `docs/BaoCao-KiemThu.md` | Báo cáo kiểm thử 3 tình huống |

## Chạy trên máy lập trình viên

Cần [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet test                                   # chạy unit test
dotnet run --project src/WebBanHang           # chạy tại URL in ra, ví dụ http://localhost:5000
dotnet run --project src/WebBanHang -- --PathBase=/webbanhang   # giả lập đường dẫn /webbanhang của IIS
```

CSDL SQLite tự tạo tại `src/WebBanHang/App_Data/webbanhang.db` (có dữ liệu mẫu). Khi mô hình dữ liệu thay đổi:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add TenMigration --project src/WebBanHang --output-dir Migrations
```

Ứng dụng tự áp dụng migration khi khởi động nên dữ liệu cũ được giữ lại sau mỗi lần deploy.

## Quy ước làm việc nhóm

- `main` luôn ở trạng thái build và test được. Không push trực tiếp vào `main`.
- Mỗi thành viên làm trên nhánh riêng: `feature/products`, `feature/categories`, `feature/orders`.
- Hợp nhất vào `main` bằng **Pull Request**; khi `main` đổi, GitHub gọi webhook và Jenkins tự chạy pipeline.
- Nhánh `test/build-error` và `test/test-fail` chứa lỗi cố ý để kiểm thử pipeline (không merge vào `main`).
