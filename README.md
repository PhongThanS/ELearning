# ELearning

Nền tảng thi trực tuyến: .NET 10 Web API + EF Core 10 + SQL Server, frontend React + TypeScript + Vite.

Thiết kế đầy đủ nằm ở [`docs/`](docs/00-muc-luc.md). Quy tắc cho AI agent nằm ở [`CLAUDE.md`](CLAUDE.md).

## Trạng thái

| Milestone | Nội dung | Trạng thái |
|---|---|---|
| M1 | Hạ tầng: solution, schema đầy đủ + migration, health check, Serilog, OpenAPI, CI | Xong |
| M2 | Identity: đăng ký/đăng nhập, refresh cookie xoay vòng, khóa tài khoản, permission, user/nhóm/vai trò | Xong |
| M3 | Ngân hàng câu hỏi: danh mục, 4 loại câu hỏi, đáp án chấp nhận, Markdown, mã tự sinh, clone, dữ liệu demo | Xong |
| M4 | Đề thi: version, snapshot câu hỏi, sắp xếp, đồng bộ, preview, publish, đóng/mở, clone, gán đề, đề demo | Xong |
| M5 | Lượt thi: start (an toàn khi song song), autosave theo `clientSeq`, khóa dòng, ân hạn, sự kiện, nộp bài idempotent, job tự nộp, đóng đề buộc nộp | Xong |
| M6 | Chấm điểm: 4 grader, chuẩn hóa tiếng Việt, câu hủy, tổng kết, chính sách xem điểm / xem lại, điểm chính thức | Xong |
| M8 | Vận hành admin: gia hạn / buộc nộp / hủy lượt, sửa đáp án / hủy câu + chấm lại, kết quả + export Excel, dashboard, thống kê câu hỏi, audit log | Xong |
| M7 | Frontend React: đăng nhập, khu vực admin (user, nhóm, vai trò, câu hỏi, đề, publish, kết quả, chấm lại, audit), khu vực học viên (danh sách đề, làm bài có autosave / timer, kết quả, lịch sử) | Xong |
| M9 | Kiểm thử: E2E Playwright (học viên, admin, hết giờ), load test k6 (`load-tests/`: start dồn dập, autosave liên tục, đợt nộp bài, job tự nộp) | Xong (cần chạy đo thật trên staging) |
| M10 | Triển khai: Docker Compose (SQL Server, migration bundle, tài khoản DB quyền tối thiểu, API, Nginx + HTTPS), backup / thử khôi phục, giám sát (chỉ số, `/health/alerts`, container `monitor` gửi webhook) | Xong |
| Sau MVP | Import câu hỏi Excel, xáo câu / đáp án, pool ngẫu nhiên, độ khó / tag, tự luận + chấm tay, chấm từng phần, ảnh trong đề / lựa chọn / giải thích | Xong |

## Yêu cầu

- .NET SDK 10.0.x
- SQL Server 2019 trở lên (local hoặc Docker)
- Node.js 24 (cho frontend, từ M7)

## Chạy backend

```bash
cd backend
dotnet tool restore
dotnet ef database update -p src/ELearning.Infrastructure -s src/ELearning.Api
dotnet run --project src/ELearning.Api --launch-profile http
```

- Connection string mặc định cho Development (`appsettings.Development.json`) dùng SQL Server local với Windows Authentication. Muốn dùng máy chủ khác thì ghi đè bằng user-secrets:
  ```bash
  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection string>" --project src/ELearning.Api
  ```
- Tài khoản dev (tự seed): `admin` / `Admin@123456`, `student01` và `student02` / `Student@123456`.
- Các URL:
  - Swagger UI: http://localhost:5136/swagger
  - OpenAPI: http://localhost:5136/openapi/v1.json
  - Health: http://localhost:5136/health/live và http://localhost:5136/health/ready

## Chạy frontend

```bash
cd frontend/elearning-web
npm ci
npm run dev
```

- Mở http://localhost:5173. Vite proxy `/api` sang backend ở http://localhost:5136, nên cần chạy backend trước.
- Kiểm tra: `npm run lint && npm run typecheck && npm run test && npm run build`.

## Chạy bằng Docker (cả hệ thống)

```bash
cp .env.example .env        # đổi toàn bộ mật khẩu / khóa trước khi dùng thật
docker compose up -d --build
```

- Mở http://localhost:8080, đăng nhập `admin` với `ADMIN_INITIAL_PASSWORD` trong `.env` (bắt buộc đổi mật khẩu lần đầu). Môi trường Production không có dữ liệu demo.
- HTTPS, backup, thử khôi phục: xem [docs/09-van-hanh.md](docs/09-van-hanh.md) mục 5.1 và 6.
- Giám sát và cảnh báo: đặt `ALERT_WEBHOOK_URL` trong `.env`; container `monitor` gửi webhook khi API / Nginx lỗi, tỉ lệ 5xx cao, có lượt thi quá hạn chưa nộp hoặc thiếu backup. Xem [docs/09-van-hanh.md](docs/09-van-hanh.md) mục 7.1.

## Chạy test

Integration test và API test chạy trên **SQL Server thật**. Mỗi lần chạy, test tạo một database tạm `ELearningTest_<guid>` rồi xóa khi xong.

- **Có SQL Server local:** đặt biến môi trường `ELEARNING_TEST_SQL` là connection string tới server (không cần `Database`).
  ```bash
  export ELEARNING_TEST_SQL="Server=localhost;Trusted_Connection=True;TrustServerCertificate=True"
  dotnet test backend/ELearning.sln
  ```
- **Không đặt biến:** test tự khởi động SQL Server bằng Testcontainers, nên cần Docker (CI dùng cách này).

## Load test

Kịch bản k6 ở [`load-tests/`](load-tests/README.md), chạy trên staging: `BASE_URL=https://<staging> ADMIN_PASSWORD='...' load-tests/run.sh all`. Ngưỡng ở [docs/08-kiem-thu.md](docs/08-kiem-thu.md) mục 7.

## Cấu trúc

```text
backend/
  src/ELearning.Api             Controller, middleware, cấu hình host
  src/ELearning.Application     Application service, DTO, validator, options
  src/ELearning.Domain          Entity, enum, quy tắc nghiệp vụ
  src/ELearning.Infrastructure  EF Core, migration, Dapper, bảo mật, job nền
  src/ELearning.Shared          Result, mã lỗi, phân trang
  tests/                        Unit, Integration, Api, TestSupport
frontend/elearning-web/          React + TypeScript + Vite
load-tests/                     Kịch bản k6 (xem load-tests/README.md)
docs/                           Thiết kế (nguồn chuẩn)
.github/workflows/              CI
```
