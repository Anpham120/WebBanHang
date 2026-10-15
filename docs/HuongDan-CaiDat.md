# Hướng dẫn cài đặt CI/CD (Jenkins + GitHub + IIS)

Giả định: Jenkins đã chạy trên **VPS Windows Server** (dạng Windows service, ví dụ `http://<IP-VPS>:8080`) và bạn có quyền Administrator trên VPS (Remote Desktop).
Jenkins, .NET SDK và IIS nằm trên cùng một máy nên Deploy chỉ là sao chép file cục bộ. Vì VPS có IP công khai, GitHub gọi thẳng vào Jenkins, **không cần ngrok**.

## 1. Chuẩn bị máy chủ (chạy một lần)

Trên VPS, lấy mã nguồn (hoặc chỉ cần thư mục `scripts`) rồi mở **PowerShell bằng Run as administrator**:

```powershell
git clone https://github.com/Anpham120/WebBanHang.git C:\src\WebBanHang
cd C:\src\WebBanHang
powershell -ExecutionPolicy Bypass -File scripts\setup-server.ps1
```

Script lần lượt (bước nào đã xong sẽ được bỏ qua, chạy lại an toàn):

1. Cài **.NET 8 SDK** cho mọi người dùng và kiểm tra **Git** (cài [Git for Windows](https://git-scm.com/download/win) nếu thiếu).
2. Bật **IIS**, rồi cài **ASP.NET Core 8 Hosting Bundle** (phải cài sau IIS).
3. Tạo thư mục triển khai `C:\wwwroot\webbanhang` cùng `App_Data` và `logs`.
4. Tạo Application Pool **WebBanHangPool**, `.NET CLR version = No Managed Code`.
5. Tạo ứng dụng **webbanhang** dưới **Default Web Site**, gán pool vừa tạo → địa chỉ `http://localhost/webbanhang`.
6. Phân quyền: identity của pool chỉ đọc site, ghi được `App_Data` và `logs`; tài khoản chạy service Jenkins có quyền Modify trên thư mục triển khai.

Sau khi cài .NET SDK, **khởi động lại service Jenkins** (`Restart-Service Jenkins`) để Jenkins nhận PATH mới. Jenkinsfile cũng đã tự thêm `C:\Program Files\dotnet` vào PATH.

> Làm thủ công trong IIS Manager (để chụp ảnh): *Application Pools → Add Application Pool* (Name `WebBanHangPool`, .NET CLR version **No Managed Code**, Integrated); *Sites → Default Web Site → Add Application* (Alias `webbanhang`, Application pool `WebBanHangPool`, Physical path `C:\wwwroot\webbanhang`); thư mục → *Properties → Security* để cấp quyền như mục 6.

## 2. Plugin Jenkins

Dùng bộ **Install suggested plugins**, cần có: *Pipeline*, *Git*, *GitHub*, *JUnit*. (*Manage Jenkins → Plugins → Installed plugins* để kiểm tra.)

## 3. Tạo job `WebBanHang-CICD`

*New Item → tên `WebBanHang-CICD` → Pipeline → OK*, rồi cấu hình:

| Mục | Giá trị |
|---|---|
| Build Triggers | Tích **GitHub hook trigger for GITScm polling** |
| Pipeline → Definition | **Pipeline script from SCM** |
| SCM | **Git** |
| Repository URL | URL repo GitHub của nhóm |
| Credentials | Repo public: để trống. Repo private: *Add → Username with password* (username GitHub + Personal Access Token quyền `repo`) |
| Branch Specifier | `*/main` |
| Script Path | `Jenkinsfile` |

Bấm **Save** rồi **Build Now** lần đầu. Sau lần chạy đầu Jenkins đọc `parameters` trong Jenkinsfile, nút đổi thành **Build with Parameters** với tham số `BRANCH` (mặc định `main`) để chạy thử nhánh khác. Chỉ `main` được deploy.

Cách nhanh hơn: import `jenkins/WebBanHang-CICD.config.xml` (sửa phần tử `<url>` thành repo của nhóm trước):

```powershell
$h = @{ Authorization = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("<user>:<api-token>")) }
Invoke-WebRequest "http://<IP-VPS>:8080/createItem?name=WebBanHang-CICD" -Method Post -Headers $h `
  -ContentType "application/xml" -InFile jenkins\WebBanHang-CICD.config.xml
```

(API token tạo ở *tên người dùng → Security → API Token*.)

## 4. GitHub Webhook

### 4.1 Mở cổng cho GitHub

Cho phép cổng Jenkins (8080) vào VPS ở **cả hai nơi**: tường lửa của nhà cung cấp VPS và Windows Firewall:

```powershell
New-NetFirewallRule -DisplayName "Jenkins 8080" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow
```

### 4.2 Tạo secret và khai báo ở Jenkins

1. Sinh chuỗi bí mật ngẫu nhiên:
   ```powershell
   -join ((48..57)+(65..90)+(97..122) | Get-Random -Count 40 | ForEach-Object { [char]$_ })
   ```
2. Jenkins → *Manage Jenkins → Credentials → System → Global → Add Credentials* → Kind **Secret text**, Secret = chuỗi trên, ID `github-webhook-secret`.
3. *Manage Jenkins → System → GitHub → Advanced → Shared secrets → Shared secret* = `github-webhook-secret`, *Signature algorithm* = **SHA-256** → Save.

### 4.3 Thêm webhook trên GitHub

Repo → *Settings → Webhooks → Add webhook*:

| Trường | Giá trị |
|---|---|
| Payload URL | `http://<IP-VPS>:8080/github-webhook/` (dấu `/` cuối bắt buộc) |
| Content type | `application/json` |
| Secret | chuỗi bí mật ở bước 4.2 |
| Events | **Just the push event** |
| Active | tích |

Kiểm tra ở *Webhooks → Recent Deliveries*: phải có dấu tick xanh, response `200`.
Khi PR được merge vào `main`, GitHub gửi sự kiện push, Jenkins thấy `main` có commit mới và tự chạy pipeline.

### 4.4 Bảo vệ endpoint

- **Secret + chữ ký SHA-256**: Jenkins từ chối request không có chữ ký đúng của GitHub.
- Jenkins **bật đăng nhập** (*Manage Jenkins → Security*: Jenkins' own user database, không cho Anonymous đọc/ghi), giữ bật CSRF protection.
- Nên đặt HTTPS phía trước Jenkins (reverse proxy có chứng chỉ) rồi dùng `https://...` làm Payload URL.
- Có thể thu hẹp cổng 8080 chỉ cho dải IP webhook của GitHub (lấy tại `https://api.github.com/meta`, khóa `hooks`) và IP của bạn:
  ```powershell
  $hooks = (Invoke-RestMethod https://api.github.com/meta).hooks | Where-Object { $_ -notmatch ':' }   # IPv4
  $mine  = "<IP-nha-ban>"
  Set-NetFirewallRule -DisplayName "Jenkins 8080" -RemoteAddress ($hooks + $mine)
  ```

## 5. Chạy thử

1. Jenkins → `WebBanHang-CICD` → *Build Now*. Pipeline xanh cả 7 stage.
2. Mở `http://localhost/webbanhang` trên VPS: thấy trang chủ, chân trang ghi `phiên bản 1.0.<số build>`.
3. Sửa trang chủ trên một nhánh, merge PR vào `main`: Jenkins tự chạy, chân trang đổi sang số build mới (xem `docs/BaoCao-KiemThu.md`).

## 6. Xử lý sự cố

| Triệu chứng | Nguyên nhân thường gặp / cách xử lý |
|---|---|
| `'dotnet' is not recognized` trong log Jenkins | Chưa cài .NET SDK cho mọi người dùng hoặc service chưa restart: `Restart-Service Jenkins` |
| `git` lỗi ở stage Checkout | Chưa cài Git for Windows trên VPS |
| HTTP **500.19** khi mở site | Chưa cài Hosting Bundle (hoặc cài trước IIS): chạy lại `setup-server.ps1`, rồi `iisreset` |
| HTTP **500.30** (app không khởi động) | Xem `C:\wwwroot\webbanhang\logs` hoặc Event Viewer → Windows Logs → Application. Hay gặp: pool `WebBanHangPool` không có quyền ghi `App_Data` (SQLite) |
| HTTP **404/403** trước lần deploy đầu | Thư mục `C:\wwwroot\webbanhang` còn trống: chạy pipeline để deploy |
| Deploy lỗi `Access to the path ... is denied` | Tài khoản chạy service Jenkins thiếu quyền Modify trên `C:\wwwroot\webbanhang` (chạy lại `setup-server.ps1`) |
| Deploy lỗi `WebBanHang.dll is still locked` | Ứng dụng `webbanhang` chưa gán đúng pool, hoặc có tiến trình khác giữ file: kiểm tra *Application Pool → WebBanHangPool* |
| Site hiện trang "đang được cập nhật" mãi | Deploy lỗi giữa chừng nên giữ `app_offline.htm`: sửa lỗi rồi chạy lại pipeline (hoặc xóa file đó) |
| Verify báo `Stylesheet link ... PathBase` | Có link tuyệt đối kiểu `/css/...` trong view: dùng `~/` hoặc tag helper (`asp-controller`, `asp-action`) |
| Webhook trả 4xx trong Recent Deliveries | Sai secret / thuật toán chữ ký, thiếu dấu `/` cuối URL, hoặc cổng 8080 bị chặn |
| Bật log lỗi khởi động | Trong `web.config` đặt `stdoutLogEnabled="true"` tạm thời, xem `logs\stdout*.log`, xong đặt lại `false` |

## 7. Ảnh cần chụp để nộp bài

| Sản phẩm nộp | Chụp gì |
|---|---|
| Ảnh cấu hình Jenkins Job | Trang *Configure* của `WebBanHang-CICD` (Build Triggers + Pipeline from SCM) |
| Lịch sử chạy Pipeline | Trang job: **Stage View** và danh sách *Builds* (có build xanh, build lỗi Build, build lỗi Test) |
| Ảnh cấu hình IIS | Application Pools (`WebBanHangPool`, No Managed Code); *Default Web Site → webbanhang → Basic Settings*; thư mục `C:\wwwroot\webbanhang` trong Explorer; tab Security |
| Truy cập thành công | Trình duyệt mở `http://localhost/webbanhang` trên VPS |
| Webhook | GitHub → Webhooks → Recent Deliveries (tick xanh) |
