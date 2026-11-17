# 03 — Kiến trúc

## 1. Công nghệ

### 1.1 Backend
| Mục đích | Lựa chọn |
|---|---|
| Runtime | .NET 10, ASP.NET Core Web API (Controllers) |
| Database | PostgreSQL 17 (D-35) |
| ORM | EF Core 10 (provider `Npgsql.EntityFrameworkCore.PostgreSQL`); Dapper cho báo cáo / truy vấn đọc phức tạp |
| Validation | FluentValidation |
| Xác thực | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) + refresh token |
| Mật khẩu | `Microsoft.AspNetCore.Identity.PasswordHasher<T>` (chỉ dùng hasher, không dùng toàn bộ Identity) |
| Logging | Serilog (structured, xuất JSON) |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` để sinh tài liệu; `Swashbuckle.AspNetCore.SwaggerUI` để hiển thị, trỏ tới `/openapi/v1.json` |
| Rate limit | `Microsoft.AspNetCore.RateLimiting` (có sẵn) |
| Health check | `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` (`AddDbContextCheck`) |
| Đồng hồ | `System.TimeProvider` (có sẵn trong .NET) |
| Excel | `ClosedXML` (export kết quả, điểm danh; import câu hỏi, tài khoản) |
| Kiểm thử | xUnit, AwesomeAssertions, NSubstitute, Testcontainers (PostgreSql), `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Extensions.TimeProvider.Testing` |

### 1.2 Frontend
| Mục đích | Lựa chọn |
|---|---|
| Nền tảng | React 19, TypeScript (`strict`), Vite |
| Routing | React Router |
| UI | Bootstrap 5 + `react-bootstrap` (không thêm UI framework thứ hai) |
| HTTP | Axios (một instance duy nhất) |
| Server state | TanStack Query |
| Form | React Hook Form + Zod |
| i18n | `react-i18next`, mặc định `vi` |
| Markdown | `react-markdown` + `remark-gfm` + `remark-math` / `rehype-katex` cho công thức LaTeX (**không** dùng `rehype-raw`) |
| Lint | `oxlint` |
| Ngày giờ | `Intl.DateTimeFormat` với `timeZone: 'Asia/Ho_Chi_Minh'` |
| Kiểm thử | Vitest, React Testing Library, MSW, Playwright (E2E) |

### 1.3 Thư viện bị cấm hoặc cần lưu ý (D-19)
| Thư viện | Quyết định | Lý do |
|---|---|---|
| MediatR | **Không dùng** | Đã chuyển sang license thương mại; application service thuần là đủ |
| AutoMapper | **Không dùng** | Đã chuyển sang license thương mại; map DTO tường minh dễ kiểm soát việc lộ đáp án |
| FluentAssertions ≥ 8 | **Không dùng** | Cần license trả phí cho mục đích thương mại; dùng AwesomeAssertions (fork, API tương đương) |
| Generic repository | **Không dùng** | Chỉ tạo abstraction có ý nghĩa nghiệp vụ |
| Redis, RabbitMQ, Hangfire, SignalR | **Chưa dùng** | Chỉ thêm khi có yêu cầu thực tế |

Muốn thêm một thư viện mới phải kiểm tra license và ghi lại quyết định.

## 2. Cấu trúc repository

```text
ELearning/
├── backend/
│   ├── ELearning.sln
│   ├── Directory.Build.props        # Nullable, TreatWarningsAsErrors, LangVersion
│   ├── Directory.Packages.props     # Quản lý version package tập trung
│   ├── src/
│   │   ├── ELearning.Api/
│   │   ├── ELearning.Application/
│   │   ├── ELearning.Domain/
│   │   ├── ELearning.Infrastructure/
│   │   └── ELearning.Shared/
│   ├── tests/
│   │   ├── ELearning.UnitTests/
│   │   ├── ELearning.IntegrationTests/
│   │   ├── ELearning.ApiTests/
│   │   └── ELearning.TestSupport/   # Tạo database PostgreSQL tạm cho test
│   └── Dockerfile                   # target api và migrator (EF migration bundle)
├── frontend/
│   └── elearning-web/               # src/, e2e/ (Playwright), Dockerfile (build Vite → Nginx)
├── deploy/
│   ├── nginx/                       # Cấu hình Nginx (header bảo mật, proxy /api, TLS)
│   ├── scripts/                     # backup / restore / monitor / demo-data
│   ├── server/                      # Compose + script cập nhật cho máy chủ chỉ kéo image
│   ├── monitor/                     # Dockerfile image giám sát
│   └── sql/
├── load-tests/                      # Kịch bản k6
├── docs/
├── .github/workflows/               # backend, frontend, docker, release
├── docker-compose.yml               # Cả hệ thống build từ mã nguồn
├── docker-compose.prod.yml          # Override HTTPS
├── .env.example
├── README.md
└── .gitignore
```

## 3. Các tầng backend

Chiều phụ thuộc: `Api → Application → Domain`, `Infrastructure → Application + Domain`. **Domain không phụ thuộc gì.**

### 3.1 `ELearning.Api`
- Controller, middleware, cấu hình xác thực / phân quyền, OpenAPI, DI, CORS, rate limit, health check (gồm `/health/alerts`, `Monitoring/`).
- Chỉ xử lý những việc thuộc về HTTP.
- **Không đặt logic nghiệp vụ trong controller.** Controller chỉ gọi application service rồi chuyển `Result` thành `ApiResponse`.

### 3.2 `ELearning.Application`
- Application service, DTO, validator, interface (repository, query, clock, current user), điều phối use case.
- Module:
```text
Application/
├── Auth/  Users/  Groups/  Roles/
├── Classes/          # Lớp học (D-28)
├── Attendance/       # Điểm danh & BTVN (D-31)
├── Questions/        # Chuyên đề, câu hỏi, import Excel, nhập đề Markdown / Word (ExamMarkdown, DocxImageExtractor,
│                     #   MathTypeLatex, OmmlLatex: chuyển công thức Word sang LaTeX)
├── Exams/            # Exam, ExamVersion, câu hỏi trong version, pool ngẫu nhiên, publish
├── Attempts/         # start, lưu đáp án, nộp, sự kiện, tự nộp (AttemptFinalizer, AttemptExpirationService)
├── Grading/
├── Admin/            # Thao tác trên lượt thi, kết quả, export, sửa đáp án / chấm lại, chấm tay, dashboard, audit log
├── Media/            # Ảnh câu hỏi, ký URL (D-27)
├── Videos/           # Kho video bài giảng
├── Monitoring/       # OperationalMetrics (meter ELearning), AlertRules, đếm lượt quá hạn (D-26)
├── Audit/
└── Common/           # Abstractions (IAppDbContext, ICurrentUser, IAttemptLock…), Options, Like (escape LIKE)
```

### 3.3 `ELearning.Domain`
- Entity, enum, value object, domain exception, các quy tắc bất biến.
- **Quy tắc chuyển trạng thái nằm trong entity**, ví dụ `ExamVersion.Publish()`, `ExamAttempt.Submit(now, grace)`, `ExamAttempt.Extend(minutes)`. Mọi chuyển trạng thái không hợp lệ đều ném domain exception.
- Chuẩn hóa văn bản và parse số (`AnswerNormalizer`, `NumericAnswerParser`) là domain service thuần, không phụ thuộc hạ tầng.

### 3.4 `ELearning.Infrastructure`
```text
Infrastructure/
├── Persistence/
│   ├── ELearningDbContext.cs
│   ├── Configurations/      # mỗi entity một IEntityTypeConfiguration
│   ├── Converters/          # UtcDateTimeConverter, các enum converter
│   ├── Migrations/
│   ├── Seed/
│   ├── AttemptLock.cs       # IAttemptLock: SELECT … FOR UPDATE, nhận biết lỗi trùng khóa
│   └── CodeGenerator.cs     # Mã câu hỏi từ sequence / theo chuyên đề
├── Queries/                 # Dapper: ReportQuery (dashboard, thống kê câu hỏi)
├── Security/                # TokenService, PasswordHasher, UserAccessService (cache quyền)
├── Services/                # RegistrationSettingService (bật / tắt tự đăng ký)
├── Media/                   # FileSystemMediaStore
├── Videos/                  # YoutubeOEmbedClient
├── BackgroundJobs/          # AttemptExpirationWorker, MonitoringSnapshotWorker
└── Excel/                   # ResultExporter, AttendanceExporter, workbook import câu hỏi / tài khoản
```
Không có thư mục `Repositories/`: service dùng `IAppDbContext` trực tiếp (D-23); thao tác cần SQL riêng nằm sau interface nhỏ (`IAttemptLock`, `ICodeGenerator`, `IReportQuery`).

Không viết hàng trăm lệnh `.Has...()` trong `OnModelCreating`; dùng `ApplyConfigurationsFromAssembly`.

### 3.5 `ELearning.Shared`
- `Result<T>`, `Error`, `PagedResult<T>`, mã lỗi (`ErrorCodes`), hằng số.
- Không biến `Shared` thành nơi chứa mọi thứ không biết đặt đâu.

## 4. Ranh giới service

| Service | Trách nhiệm | Không được |
|---|---|---|
| `IAuthService` | Đăng ký, đăng nhập, refresh, logout, đổi / đặt lại mật khẩu | Biết gì về đề thi |
| `IUserService`, `IGroupService`, `IRoleService` | Quản lý user, nhóm, vai trò, permission | |
| `IQuestionCategoryService`, `IQuestionService` | CRUD, bật/tắt, clone, preview câu hỏi | |
| `IExamService` | Tạo / sửa / xóa đề nháp, đóng / mở lại, gán đề | Tính điểm học viên |
| `IExamVersionService` | Tạo version, thêm / xóa / sắp xếp / đồng bộ câu hỏi, preview, publish | |
| `IAnswerKeyService` | Sửa đáp án, hủy câu, kích hoạt chấm lại | Sửa câu trả lời của học viên |
| `IAttemptService` | Start, tải, lưu đáp án, nộp, tự nộp, ghi sự kiện, kiểm tra quyền sở hữu và thời hạn | Tự chấm (phải gọi `IGradingService`) |
| `IAttemptAdminService` | Gia hạn, buộc nộp, hủy, cấp thêm lượt | |
| `IGradingService` | Tải dữ liệu chấm, chấm từng câu, tính tổng | Biết về HTTP, JWT, UI |
| `IResultService` | Lưu kết quả, lịch sử điểm, áp chính sách hiển thị, export | |
| `IAuditService` | Ghi audit trong cùng transaction với thao tác | |
| `IClassroomService`, `IAttendanceService` | Lớp học; điểm danh & BTVN theo buổi (D-28, D-31) | |
| `IMediaService`, `IVideoService` | Ảnh câu hỏi (D-27); kho video bài giảng | |
| `IQuestionImportService`, `IExamMarkdownImportService`, `IUserImportService` | Import câu hỏi / đề / tài khoản từ file | Bỏ qua validator của chức năng tạo lẻ |

Tên trong bảng là vai trò; tên interface thực tế có thể khác đôi chút (ví dụ `ICategoryService`, `IAdminAttemptService`, `IResultAdminService`). Xem `Application/DependencyInjection.cs` để có danh sách đăng ký đầy đủ.

## 5. Grading engine

```csharp
public interface IQuestionGrader
{
    QuestionType Type { get; }
    GradingResult Grade(GradingQuestion question, StudentAnswer answer);
}

public interface IGradingService
{
    Task<AttemptGradingResult> GradeAttemptAsync(Guid attemptId, CancellationToken ct);
}

public sealed record GradingResult(bool IsCorrect, decimal Score, decimal MaxScore);
```

- Có 4 implementation: `SingleChoiceGrader`, `MultipleChoiceGrader`, `TrueFalseGrader`, `FillInGrader` (bên trong xử lý cả `TEXT` và `NUMBER`).
- `GradingService` chọn grader theo một `Dictionary<QuestionType, IQuestionGrader>` được dựng từ DI. Loại chưa có grader → ném exception khi khởi động (fail fast).
- Câu bị hủy (`IsVoided`) được xử lý **trước** khi gọi grader: điểm tối đa, `IsCorrect = true`.
- `GradingQuestion` được dựng từ `ExamQuestions` (D-01), không bao giờ dựng từ `Questions`.
- Thêm câu tự luận sau này: thêm strategy hoặc luồng chấm tay mới. **Không** gộp thành một `switch` khổng lồ.

```csharp
public sealed class MultipleChoiceGrader : IQuestionGrader
{
    public QuestionType Type => QuestionType.MultipleChoice;

    public GradingResult Grade(GradingQuestion q, StudentAnswer a)
    {
        var expected = q.CorrectOptionCodes.ToHashSet(StringComparer.Ordinal);
        var actual = a.SelectedOptionCodes.ToHashSet(StringComparer.Ordinal);
        var correct = expected.SetEquals(actual);
        return new GradingResult(correct, correct ? q.Score : 0m, q.Score);
    }
}
```

## 6. Luồng nộp bài (mẫu)

```csharp
public async Task<Result<StudentResultDto>> SubmitAsync(Guid attemptId, Guid userId, CancellationToken ct)
{
    var strategy = _db.Database.CreateExecutionStrategy();       // bắt buộc khi bật retry
    return await strategy.ExecuteAsync(async () =>
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var attempt = await _attempts.GetForUpdateAsync(attemptId, ct);   // SELECT … FOR UPDATE (IAttemptLock)
        if (attempt is null || attempt.UserId != userId)
            return Result.NotFound<StudentResultDto>(ErrorCodes.AttemptNotFound);

        if (attempt.Status != AttemptStatus.InProgress)
            return await _results.GetStudentResultAsync(attempt.Id, ct);   // idempotent

        var now = _time.GetUtcNow().UtcDateTime;
        attempt.Submit(now, _options.GraceSeconds, SubmitReason.Student); // domain chọn SUBMITTED hay AUTO_SUBMITTED

        var grading = await _grading.GradeAttemptAsync(attempt.Id, ct);
        await _results.SaveAsync(attempt, grading, ct);
        await _audit.WriteAsync(AuditActions.AttemptSubmitted, attempt, ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await _results.GetStudentResultAsync(attempt.Id, ct);
    });
}
```
Chi tiết có thể khác, nhưng bốn nguyên tắc phải giữ: transaction, khóa dòng, idempotent, kiểm tra quyền sở hữu.

## 7. Kiến trúc frontend

```text
src/
├── app/            App.tsx, routes.tsx, pageLoaders.ts (tách bundle theo trang), chunkReload.ts
├── components/common/   DataTable, Feedback, MarkdownView, MediaUrls, SearchableSelect, MultiSearchableSelect, toast
├── layouts/        Layouts.tsx (AdminLayout, StudentLayout, PlayerLayout, PublicLayout)
├── features/
│   ├── auth/             # AuthContext, guards (RequireAuth, RequirePermission, RequireStudent), trang đăng nhập / hồ sơ
│   ├── admin/            # IdentityPages (người dùng, nhóm, vai trò), ClassPages, DashboardPage, UserImportDialog
│   ├── questions/        # Chuyên đề, câu hỏi, import Excel, nhập đề Markdown / Word
│   ├── exams/            # Danh sách / chi tiết đề, VersionBuilderPage, pool ngẫu nhiên, kết quả, chấm tay, audit log
│   ├── attempts/         # ExamPlayerPage (+ playerState, playerHooks), các trang của học viên
│   ├── attendance/       # Điểm danh (giáo viên) và lịch sử điểm danh (học viên)
│   └── videos/           # Kho video bài giảng, trang xem
├── services/       apiClient.ts (instance Axios duy nhất), api.ts (mọi lời gọi API theo nhóm)
├── types/api.ts    Kiểu DTO viết tay, phải giữ khớp với backend
├── hooks/  utils/  constants/
├── i18n/           index.ts, vi.json
└── theme.scss, styles*.css
```
Mỗi file `*Pages.tsx` xuất nhiều trang của cùng một khu chức năng; `routes.tsx` tải chúng bằng `React.lazy` qua `pageLoaders`.

- **Server state:** TanStack Query cho mọi dữ liệu từ API. Không copy dữ liệu API vào global store.
- **State của exam player:** `useReducer` cục bộ (xem `06-frontend.md`).
- **Chuỗi hiển thị:** đặt trong `i18n/vi.json`, không hard-code trong logic.
- **API client:** một instance Axios duy nhất, lo các việc sau:
  - Gắn `baseURL` `/api`.
  - Gắn header `Authorization` từ access token giữ trong bộ nhớ.
  - Khi gặp 401: gọi `/api/auth/refresh` **một lần** (các request song song dùng chung một promise), rồi gửi lại request.
  - Nếu refresh thất bại: đăng xuất.
- **Môi trường dev:** Vite proxy `/api` → backend, để frontend và API cùng origin (cookie `SameSite=Strict` hoạt động, không cần CORS).

## 8. Caching

| Được cache | Không cache |
|---|---|
| Danh mục; permission theo user (5 phút, xóa khi đổi quyền); cấu hình tĩnh; metadata của version đã publish | Trạng thái lượt thi; kết quả chấm (nguồn chuẩn là DB) |

MVP dùng `IMemoryCache` (chạy một instance). Khi scale nhiều instance phải chuyển sang cache dùng chung hoặc bỏ cache permission (xem `09-van-hanh.md`).

## 9. Quyết định / Giả định

- **(M2) D-23 — `IAppDbContext`:** application service làm việc trực tiếp với `DbSet` qua interface `IAppDbContext` (khai báo ở Application, `ELearningDbContext` implement). Vì vậy Application tham chiếu gói `Microsoft.EntityFrameworkCore`. Interface chuyên biệt chỉ dùng cho thao tác cần SQL riêng (khóa dòng `FOR UPDATE`, sequence, báo cáo Dapper).
- **(D-35) Application tham chiếu cả gói Npgsql** (`Npgsql.EntityFrameworkCore.PostgreSQL`) để gọi `EF.Functions.ILike` trong các truy vấn tìm kiếm (khoảng 12 service). Đây là chỗ tầng Application phụ thuộc provider; nếu muốn bỏ, gom `ILike` sau một helper ở Infrastructure. `GlobalExceptionHandler` (Api) cũng tham chiếu `PostgresException` để nhận lỗi trùng khóa.
- **Cấu trúc thư mục ở mục 2, 3 và 7 mô tả code hiện tại**, không phải cấu trúc dự kiến ban đầu (đã bỏ `database/`, `docker/`, `Repositories/`, `Results/`, `Reports/`, `pages/`).
- **(M2) Kiểm tra quyền:** `AccessAuthorizationHandler` đọc ảnh chụp quyền đã cache (`IUserAccessService`), không đọc role trong token. JwtBearer `OnTokenValidated` kiểm tra `IsActive` và `SecurityStamp`.

- **D-19:** MediatR, AutoMapper và FluentAssertions v8+ bị loại vì license. Spec gốc chỉ ghi FluentAssertions; đổi sang AwesomeAssertions (API tương thích).
- **OpenAPI:** từ .NET 9, template mặc định dùng `Microsoft.AspNetCore.OpenApi`. Giữ Swagger UI cho quen thuộc.
- **`react-bootstrap`** là component React của chính Bootstrap, không vi phạm quy tắc "không thêm UI framework thứ hai".
- **Grader** trả về `record` thay cho class có `Feedback`. Phản hồi theo từng câu nằm ở `Explanation` của câu hỏi.
- **Cache permission bằng `IMemoryCache`** chỉ đúng khi chạy một instance. Đây là giả định của MVP.
