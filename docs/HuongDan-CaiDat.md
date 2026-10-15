# Hướng dẫn cài đặt CI/CD (Jenkins + GitHub + IIS)

## Mô hình

```
GitHub ── webhook (HTTPS) ──▶ Jenkins controller  (VPS Ubuntu, https://<domain-jenkins>)
                                    │  giao việc cho agent nhãn "windows"
                                    ▼
                         Jenkins agent "windows-iis"  (máy Windows: .NET 8 SDK + Git + IIS)
                                    │  Checkout → Restore → Build → Test → Publish → Deploy → Verify
                                    ▼
                         IIS  http://localhost/webbanhang   (C:\wwwroot\webbanhang)
```

IIS chỉ chạy trên Windows, nên mọi stage chạy trên một **Windows agent** có nhãn `windows`. Controller trên VPS Linux chỉ nhận webhook, lập lịch và hiển thị kết quả.
Agent tự kết nối ra controller qua WebSocket/HTTPS, nên không phải mở cổng nào trên máy Windows. Máy Windows phải bật và agent phải online lúc chạy pipeline.
Controller có tên miền công khai nên GitHub gọi thẳng vào được, **không cần ngrok**.

## 0. Chạy nhanh phần CI trên server Linux

Cách nhanh nhất để thấy pipeline xanh (giống bài demo CI trên lớp): build, test và publish ngay trên server Linux chạy Jenkins, chưa cần Windows hay IIS.

1. SSH vào server, cài Git và .NET 8 SDK rồi khởi động lại Jenkins:

   ```bash
   sudo apt-get update && sudo apt-get install -y git dotnet-sdk-8.0
   dotnet --version                # phải ra 8.0.x
   sudo systemctl restart jenkins  # để Jenkins nhận dotnet
   ```

   Không có gói `dotnet-sdk-8.0` (ví dụ Ubuntu 20.04) thì dùng script chính thức của Microsoft:

   ```bash
   curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
   sudo bash dotnet-install.sh --channel 8.0 --install-dir /usr/share/dotnet
   sudo ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
   ```

2. Tạo job theo mục 4 và đặt **Script Path = `Jenkinsfile.ci`**. Pipeline gồm 5 stage Checkout → Restore → Build → Test → Publish và đính kèm file `WebBanHang-<phiên bản>.tar.gz` vào build.
3. Muốn deploy lên IIS thì làm tiếp mục 1 và 2, rồi đổi **Script Path** về `Jenkinsfile`.

## 1. Chuẩn bị máy Windows (làm một lần)

Cần: Windows 10/11 hoặc Windows Server, quyền Administrator, [Git for Windows](https://git-scm.com/download/win), Java 17 trở lên (cho agent; kiểm tra bằng `java -version`, thiếu thì cài [Temurin 21](https://adoptium.net/)).

Mở **PowerShell bằng Run as administrator** tại thư mục repo:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup-server.ps1
```

Script lần lượt (bước nào đã xong sẽ bỏ qua, chạy lại an toàn):

1. Kiểm tra .NET 8 SDK (chưa có thì cài) và Git.
2. Bật **IIS**, rồi cài **ASP.NET Core 8 Hosting Bundle** (phải cài sau IIS).
3. Tạo thư mục triển khai `C:\wwwroot\webbanhang` cùng `App_Data` và `logs`.
4. Tạo Application Pool **WebBanHangPool**, `.NET CLR version = No Managed Code`.
5. Tạo ứng dụng **webbanhang** dưới **Default Web Site**, gán pool vừa tạo, địa chỉ `http://localhost/webbanhang`.
6. Phân quyền: identity của pool chỉ đọc site, ghi được `App_Data` và `logs`; tài khoản chạy agent có quyền Modify trên thư mục triển khai (mặc định là tài khoản đang chạy script; đổi bằng `-JenkinsAccount "TENMAY\tennguoidung"`).

> Làm thủ công trong IIS Manager (để chụp ảnh): *Application Pools → Add Application Pool* (Name `WebBanHangPool`, .NET CLR version **No Managed Code**, Integrated); *Sites → Default Web Site → Add Application* (Alias `webbanhang`, Application pool `WebBanHangPool`, Physical path `C:\wwwroot\webbanhang`).

## 2. Kết nối máy Windows vào Jenkins làm agent

**Trên Jenkins (trình duyệt):**

1. *Manage Jenkins → System → Jenkins URL* phải là `https://<domain-jenkins>/` (đúng HTTPS và tên miền) → Save.
2. *Manage Jenkins → Nodes → New Node* → Node name `windows-iis` → **Permanent Agent** → Create. Điền:

   | Trường | Giá trị |
   |---|---|
   | Number of executors | `1` |
   | Remote root directory | `C:\jenkins` |
   | Labels | `windows` |
   | Usage | Use this node as much as possible |
   | Launch method | **Launch agent by connecting it to the controller**, tích **Use WebSocket** |

3. Save, mở node `windows-iis`: trang hiện sẵn các lệnh chạy agent (có `-secret`). Giữ bí mật secret này.

**Trên máy Windows** (PowerShell thường, mở cửa sổ mới để nhận PATH):

```powershell
mkdir C:\jenkins; cd C:\jenkins
curl.exe -sO https://<domain-jenkins>/jnlpJars/agent.jar
# dán đúng lệnh "java -jar agent.jar ..." mà trang node hiển thị, dạng:
java -jar agent.jar -url https://<domain-jenkins>/ -secret <secret> -name "windows-iis" -webSocket -workDir "C:\jenkins"
```

Thấy `Connected` là xong; trang *Nodes* hiện `windows-iis` online. Giữ cửa sổ này mở khi chạy pipeline (muốn tự chạy khi đăng nhập thì tạo task trong Task Scheduler chạy cùng lệnh).

## 3. Plugin Jenkins

Cần: *Pipeline*, *Git*, *GitHub*, *JUnit* (có sẵn trong bộ Install suggested plugins). Kiểm tra ở *Manage Jenkins → Plugins → Installed plugins*.

## 4. Tạo job `WebBanHang-CICD`

*New Item* → tên **`WebBanHang-CICD`** → **Pipeline** → OK (job đã tạo với tên khác thì vào trang job → *Rename*). Cấu hình theo các thẻ ở menu trái:

| Thẻ | Mục | Giá trị |
|---|---|---|
| General | | Giữ mặc định (tùy chọn: tích *Do not allow concurrent builds*) |
| Triggers | Build Triggers | Tích **GitHub hook trigger for GITScm polling** |
| Pipeline | Definition | **Pipeline script from SCM** |
| Pipeline | SCM | **Git** |
| Pipeline | Repository URL | `https://github.com/Anpham120/WebBanHang.git` |
| Pipeline | Credentials | Repo public: *- none -*. Repo private: *Add → Username with password* (GitHub username + Personal Access Token quyền `repo`) |
| Pipeline | Branch Specifier | `*/main` |
| Pipeline | Script Path | `Jenkinsfile` (CI/CD đầy đủ, cần agent Windows) hoặc `Jenkinsfile.ci` (chỉ CI, chạy trên Linux) |

Bấm **Save**, rồi **Build Now**. Sau lần chạy đầu Jenkins đọc `parameters` trong Jenkinsfile, nút đổi thành **Build with Parameters** với tham số `BRANCH` (mặc định `main`) để chạy thử nhánh khác. Chỉ `main` được deploy.

Cách nhanh hơn: import `jenkins/WebBanHang-CICD.config.xml` (sửa phần tử `<url>` thành repo của nhóm trước):

```powershell
$h = @{ Authorization = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("<user>:<api-token>")) }
Invoke-WebRequest "https://<domain-jenkins>/createItem?name=WebBanHang-CICD" -Method Post -Headers $h `
  -ContentType "application/xml" -InFile jenkins\WebBanHang-CICD.config.xml
```

(API token tạo ở *tên người dùng → Security → API Token*.)

## 5. GitHub Webhook

1. Sinh chuỗi bí mật ngẫu nhiên:
   ```powershell
   -join ((48..57)+(65..90)+(97..122) | Get-Random -Count 40 | ForEach-Object { [char]$_ })
   ```
2. Jenkins → *Manage Jenkins → Credentials → System → Global credentials → Add Credentials* → Kind **Secret text**, Secret = chuỗi trên, ID `github-webhook-secret`.
3. *Manage Jenkins → System → GitHub → Advanced → Shared secrets → Shared secret* = `github-webhook-secret`, *Signature algorithm* = **SHA-256** → Save.
4. GitHub: repo → *Settings → Webhooks → Add webhook*:

   | Trường | Giá trị |
   |---|---|
   | Payload URL | `https://<domain-jenkins>/github-webhook/` (dấu `/` cuối bắt buộc) |
   | Content type | `application/json` |
   | Secret | chuỗi bí mật ở bước 1 |
   | Events | **Just the push event** |
   | Active | tích |

5. Kiểm tra ở *Webhooks → Recent Deliveries*: tick xanh, response `200`.

Khi PR được merge vào `main`, GitHub gửi sự kiện push, Jenkins thấy `main` có commit mới và tự chạy pipeline trên agent `windows-iis`.

**Bảo vệ endpoint webhook:** secret + chữ ký SHA-256 (Jenkins từ chối request không có chữ ký đúng của GitHub); HTTPS qua tên miền; Jenkins bật đăng nhập (*Manage Jenkins → Security*, không cho Anonymous đọc/ghi) và giữ bật CSRF protection.

## 6. Chạy thử

1. Jenkins → `WebBanHang-CICD` → *Build Now*. Pipeline xanh cả 7 stage (log ghi `Running on windows-iis`).
2. Mở `http://localhost/webbanhang` trên máy Windows: thấy trang chủ, chân trang ghi `phiên bản 1.0.<số build>`.
3. Sửa trang chủ trên một nhánh, merge PR vào `main`: Jenkins tự chạy, chân trang đổi sang số build mới (xem `docs/BaoCao-KiemThu.md`).

## 7. Xử lý sự cố

| Triệu chứng | Nguyên nhân thường gặp / cách xử lý |
|---|---|
| Build đứng ở `Waiting for next available executor` / `There are no nodes with the label 'windows'` | Agent chưa online hoặc nhãn sai: kiểm tra node `windows-iis` có Labels `windows` và đang kết nối |
| Build đứng ở `Waiting for next available executor` khi dùng `Jenkinsfile.ci` | Built-In Node đang để 0 executor: *Manage Jenkins → Nodes → Built-In Node → Configure → Number of executors* = `2` |
| `dotnet: command not found` trên Linux | Chưa cài SDK hoặc chưa khởi động lại Jenkins: làm lại mục 0 |
| Build báo `Killed` ở stage Build hoặc Test | VPS thiếu RAM: tạo swap `sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile` |
| Agent không kết nối được | `Jenkins URL` sai (phải `https://<domain-jenkins>/`); reverse proxy (nginx) chưa chuyển tiếp WebSocket (header `Upgrade`/`Connection`); secret sai; Java dưới 17 |
| `'dotnet' is not recognized` trong log | Agent chạy từ cửa sổ mở trước khi cài SDK: đóng, mở PowerShell mới rồi chạy lại lệnh agent |
| Lỗi `git` ở stage Checkout | Chưa cài Git for Windows trên máy agent |
| Lỗi liên quan `cmd.exe` / `bat` | Job chạy nhầm trên controller Linux: Jenkinsfile phải có `agent { label 'windows' }` (bản mới nhất ở `main`) |
| HTTP **500.19** khi mở site | Chưa cài Hosting Bundle (hoặc cài trước IIS): chạy lại `setup-server.ps1`, rồi `iisreset` |
| HTTP **500.30** (app không khởi động) | Xem `C:\wwwroot\webbanhang\logs` hoặc Event Viewer → Windows Logs → Application. Hay gặp: pool `WebBanHangPool` không có quyền ghi `App_Data` (SQLite) |
| HTTP **404/403** trước lần deploy đầu | Thư mục `C:\wwwroot\webbanhang` còn trống: chạy pipeline để deploy |
| Deploy lỗi `Access to the path ... is denied` | Tài khoản chạy agent thiếu quyền Modify trên `C:\wwwroot\webbanhang`: chạy lại `setup-server.ps1 -JenkinsAccount "TENMAY\tennguoidung"` |
| Deploy lỗi `WebBanHang.dll is still locked` | Ứng dụng `webbanhang` chưa gán đúng pool, hoặc có tiến trình khác giữ file: kiểm tra *Application Pool → WebBanHangPool* |
| Site hiện trang "đang được cập nhật" mãi | Deploy lỗi giữa chừng nên giữ `app_offline.htm`: sửa lỗi rồi chạy lại pipeline (hoặc xóa file đó) |
| Verify báo `Stylesheet link ... PathBase` | Có link tuyệt đối kiểu `/css/...` trong view: dùng `~/` hoặc tag helper (`asp-controller`, `asp-action`) |
| Webhook trả 4xx trong Recent Deliveries | Sai secret hoặc thuật toán chữ ký, thiếu dấu `/` cuối URL, tên miền/HTTPS lỗi |
| Bật log lỗi khởi động | Trong `web.config` đặt `stdoutLogEnabled="true"` tạm thời, xem `logs\stdout*.log`, xong đặt lại `false` |

## 8. Ảnh cần chụp để nộp bài

| Sản phẩm nộp | Chụp gì |
|---|---|
| Ảnh cấu hình Jenkins Job | Trang *Configure* của `WebBanHang-CICD` (thẻ Triggers và thẻ Pipeline); trang *Nodes* có `windows-iis` online |
| Lịch sử chạy Pipeline | Trang job: **Stage View** và danh sách *Builds* (có build xanh, build lỗi Build, build lỗi Test) |
| Ảnh cấu hình IIS | Application Pools (`WebBanHangPool`, No Managed Code); *Default Web Site → webbanhang → Basic Settings*; thư mục `C:\wwwroot\webbanhang` trong Explorer; tab Security |
| Truy cập thành công | Trình duyệt mở `http://localhost/webbanhang` |
| Webhook | GitHub → Webhooks → Recent Deliveries (tick xanh) |
