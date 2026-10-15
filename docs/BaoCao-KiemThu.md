# Báo cáo kiểm thử quy trình CI/CD – WebBanHang

| | |
|---|---|
| Nhóm | ……… (3 sinh viên: ………, ………, ………) |
| Repository | https://github.com/Anpham120/WebBanHang |
| Jenkins | https://<domain-jenkins>, job **WebBanHang-CICD**, agent Windows `windows-iis` |
| Website | http://localhost/webbanhang |

## 1. Quy trình

GitHub (Pull Request → `main`) → webhook → Jenkins → **Checkout → Restore → Build → Test → Publish → Deploy → Verify** → IIS (`C:\wwwroot\webbanhang`).

Quy tắc dừng: stage nào lỗi thì các stage sau không chạy. Lỗi Build hoặc Test nghĩa là không Publish, không Deploy nên IIS giữ nguyên phiên bản đang chạy. Deploy chỉ chạy khi build nhánh `main`.

Mỗi lần build, chân trang website hiển thị `phiên bản 1.0.<số build Jenkins>`; stage Verify so khớp số này với bản vừa deploy.

## 2. Tình huống 1 – Triển khai thành công

**Mục tiêu:** thay đổi giao diện trang chủ, merge vào `main`, Jenkins tự deploy, website hiển thị nội dung mới.

**Thực hiện**

```powershell
git checkout main; git pull
git checkout -b feature/home-banner
# sửa tiêu đề trong src/WebBanHang/Views/Home/Index.cshtml
git commit -am "feat(home): doi noi dung trang chu"
git push -u origin feature/home-banner
# GitHub: Open Pull Request → Merge pull request (vào main)
```

**Kỳ vọng:** webhook kích hoạt build mới trên `main`; 7 stage xanh; mở `http://localhost/webbanhang` thấy nội dung mới và số phiên bản bằng số build.

| Hạng mục | Kết quả |
|---|---|
| Build số | # ……… (kích hoạt tự động bởi webhook: Có / Không) |
| Stage dừng | Không (SUCCESS) |
| Phiên bản hiển thị trên IIS | 1.0.……… |
| Nội dung trang chủ | đã đổi / chưa đổi |
| Kết luận | **Đạt / Không đạt** |

Ảnh: Stage View build thành công; trang chủ trước và sau; Recent Deliveries của webhook.

## 3. Tình huống 2 – Biên dịch thất bại

**Mục tiêu:** lỗi cú pháp C# bị phát hiện ở Build, pipeline dừng, IIS giữ nguyên phiên bản đang chạy.

**Thực hiện** (nhánh `test/build-error` đã có sẵn lỗi; muốn tự tạo thì làm như dưới)

```powershell
git checkout -b test/build-error main
# trong src/WebBanHang/Services/OrderCalculator.cs xóa dấu ; ở cuối dòng "public const decimal DiscountRate = 0.05m;"
git commit -am "test: loi cu phap C#"
git push -u origin test/build-error
```

Jenkins → `WebBanHang-CICD` → **Build with Parameters** → `BRANCH = test/build-error`.

**Kỳ vọng:** build FAILURE ở stage **Build** (log có `error CS1002: ; expected`); Test, Publish, Deploy, Verify không chạy; website vẫn hiện phiên bản cũ.

| Hạng mục | Kết quả |
|---|---|
| Build số | # ……… |
| Stage dừng | Build |
| Thông báo lỗi trong log | ……… |
| Phiên bản IIS trước / sau | 1.0.……… / 1.0.……… (phải giống nhau) |
| Kết luận | **Đạt / Không đạt** |

## 4. Tình huống 3 – Kiểm thử thất bại

**Mục tiêu:** sửa hàm tính tiền đơn hàng làm Unit Test thất bại; pipeline dừng ở Test, không Publish và Deploy.

**Thực hiện** (nhánh `test/test-fail` đã có sẵn; muốn tự tạo thì làm như dưới)

```powershell
git checkout -b test/test-fail main
# trong src/WebBanHang/Services/OrderCalculator.cs đổi:  i.UnitPrice * i.Quantity  →  i.UnitPrice + i.Quantity
git commit -am "test: sua ham tinh tien lam unit test that bai"
git push -u origin test/test-fail
```

Jenkins → **Build with Parameters** → `BRANCH = test/test-fail`.

**Kỳ vọng:** code vẫn biên dịch được nên Build xanh; stage **Test** đỏ (trang job có *Test Result* với các test thất bại); Publish, Deploy, Verify không chạy; website vẫn hiện phiên bản cũ.

| Hạng mục | Kết quả |
|---|---|
| Build số | # ……… |
| Stage dừng | Test |
| Số test đạt / thất bại | ……… / ……… |
| Phiên bản IIS trước / sau | 1.0.……… / 1.0.……… (phải giống nhau) |
| Kết luận | **Đạt / Không đạt** |

## 5. Kết luận

Điền sau khi chạy đủ ba tình huống: pipeline tự động từ GitHub đến IIS, chặn đúng bản lỗi ở Build và Test, giữ nguyên dữ liệu (`App_Data`) và cấu hình khi cập nhật.
