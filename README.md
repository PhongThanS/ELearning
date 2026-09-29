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
| M9–M10 | Kiểm thử E2E / load, triển khai | Chưa làm |

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

## Chạy test

Integration test và API test chạy trên **SQL Server thật**. Mỗi lần chạy, test tạo một database tạm `ELearningTest_<guid>` rồi xóa khi xong.

- **Có SQL Server local:** đặt biến môi trường `ELEARNING_TEST_SQL` là connection string tới server (không cần `Database`).
  ```bash
  export ELEARNING_TEST_SQL="Server=localhost;Trusted_Connection=True;TrustServerCertificate=True"
  dotnet test backend/ELearning.sln
  ```
- **Không đặt biến:** test tự khởi động SQL Server bằng Testcontainers, nên cần Docker (CI dùng cách này).

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
docs/                           Thiết kế (nguồn chuẩn)
.github/workflows/              CI
```
