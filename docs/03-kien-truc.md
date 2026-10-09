# 03 — Kiến trúc

## 1. Công nghệ

### 1.1 Backend
| Mục đích | Lựa chọn |
|---|---|
| Runtime | .NET 10, ASP.NET Core Web API (Controllers) |
| ORM | EF Core 10 (`Npgsql.EntityFrameworkCore.PostgreSQL`); Dapper cho báo cáo / truy vấn đọc phức tạp |
| Validation | FluentValidation |
| Xác thực | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) + refresh token |
| Mật khẩu | `Microsoft.AspNetCore.Identity.PasswordHasher<T>` (chỉ dùng hasher, không dùng toàn bộ Identity) |
| Logging | Serilog (structured, xuất JSON) |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` để sinh tài liệu; `Swashbuckle.AspNetCore.SwaggerUI` để hiển thị, trỏ tới `/openapi/v1.json` |
| Rate limit | `Microsoft.AspNetCore.RateLimiting` (có sẵn) |
| Health check | `Microsoft.Extensions.Diagnostics.HealthChecks` + `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` |
| Đồng hồ | `System.TimeProvider` (có sẵn trong .NET) |
| Excel | `ClosedXML` (export kết quả) |
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
| Markdown | `react-markdown` + `remark-gfm` (**không** dùng `rehype-raw`) |
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
│   └── tests/
│       ├── ELearning.UnitTests/
│       ├── ELearning.IntegrationTests/
│       └── ELearning.ApiTests/
├── frontend/
│   └── elearning-web/
├── database/
│   ├── scripts/                     # Script idempotent sinh từ migration
│   └── seed/
├── load-tests/                      # Kịch bản k6
├── docs/
├── docker/
├── .github/workflows/
├── docker-compose.yml
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
├── Categories/  Questions/
├── Exams/            # Exam, ExamVersion, ExamQuestion, publish, regrade
├── Attempts/         # start, save, submit, events, admin ops
├── Grading/
├── Results/
├── Reports/
├── Monitoring/       # OperationalMetrics (meter ELearning), AlertRules, đếm lượt quá hạn (D-26)
└── Audit/
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
│   └── Seed/
├── Repositories/            # IExamRepository, IAttemptRepository, ...
├── Queries/                 # Dapper: IExamReportQuery, IDashboardQuery
├── Security/                # JwtTokenService, RefreshTokenService, PasswordHasher
├── BackgroundJobs/          # AttemptExpirationWorker, MonitoringSnapshotWorker
└── Excel/                   # ResultExporter
```
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

        var attempt = await _attempts.GetForUpdateAsync(attemptId, ct);   // SELECT ... FOR UPDATE
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
├── app/            App.tsx, routes.tsx, providers/ (QueryClient, Auth, I18n)
├── components/     common/, form/, table/, modal/, feedback/, markdown/
├── layouts/        AdminLayout.tsx, StudentLayout.tsx, PublicLayout.tsx
├── features/
│   ├── auth/  users/  groups/  roles/
│   ├── categories/  question-bank/
│   ├── exams/            # builder, versions, publish, assignments, regrade
│   ├── attempts/         # exam player
│   ├── results/  reports/  audit/
├── pages/
├── services/       apiClient.ts + <feature>Api.ts
├── hooks/  types/  utils/  constants/
└── i18n/           vi.json, en.json
```

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

- **(M2) D-23 — `IAppDbContext`:** application service làm việc trực tiếp với `DbSet` qua interface `IAppDbContext` (khai báo ở Application, `ELearningDbContext` implement). Vì vậy Application tham chiếu gói `Microsoft.EntityFrameworkCore`, nhưng không tham chiếu SQL Server provider. Repository chuyên biệt chỉ dùng cho thao tác cần SQL riêng (khóa dòng `UPDLOCK`, báo cáo Dapper).
- **(M2) Kiểm tra quyền:** `AccessAuthorizationHandler` đọc ảnh chụp quyền đã cache (`IUserAccessService`), không đọc role trong token. JwtBearer `OnTokenValidated` kiểm tra `IsActive` và `SecurityStamp`.

- **D-19:** MediatR, AutoMapper và FluentAssertions v8+ bị loại vì license. Spec gốc chỉ ghi FluentAssertions; đổi sang AwesomeAssertions (API tương thích).
- **OpenAPI:** từ .NET 9, template mặc định dùng `Microsoft.AspNetCore.OpenApi`. Giữ Swagger UI cho quen thuộc.
- **`react-bootstrap`** là component React của chính Bootstrap, không vi phạm quy tắc "không thêm UI framework thứ hai".
- **Grader** trả về `record` thay cho class có `Feedback`. Phản hồi theo từng câu nằm ở `Explanation` của câu hỏi.
- **Cache permission bằng `IMemoryCache`** chỉ đúng khi chạy một instance. Đây là giả định của MVP.
- **(D-29) Chuyển sang PostgreSQL:** `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 thay `Microsoft.EntityFrameworkCore.SqlServer`; gói Npgsql cũng được Application tham chiếu để dùng `EF.Functions.ILike`. `IAttemptLock` (Infrastructure) khóa dòng bằng `FOR UPDATE` và nhận biết lỗi trùng unique qua `PostgresException`. Test harness dùng `Testcontainers.PostgreSql`. Không đổi các tầng, repository hay quy ước khác.
