# 10 — Bẫy kỹ thuật cần tránh

Danh sách các lỗi cụ thể, rất dễ mắc khi triển khai stack này. Mỗi mục gồm: triệu chứng, nguyên nhân, cách làm đúng. **AI agent phải đọc file này trước khi viết code cho phần liên quan.**

## 1. DateTime mất UTC → lệch 7 tiếng

- **Triệu chứng:** đồng hồ đếm ngược sai 7 giờ; đề hiện là "chưa mở" dù đã tới giờ.
- **Nguyên nhân:**
  - EF Core đọc `DATETIME2` ra `DateTimeKind.Unspecified`.
  - System.Text.Json serialize thành `"2026-09-26T03:00:00"`, không có `Z`.
  - JS `new Date(...)` hiểu chuỗi không có offset là **giờ địa phương**.
- **Cách làm đúng:**
  ```csharp
  // ELearningDbContext.ConfigureConventions
  configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
  configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();

  public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
      v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
      v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
  ```
  Kèm một test API khẳng định mọi chuỗi thời gian trong JSON kết thúc bằng `Z`.
- Với Dapper: dùng một `SqlMapper.TypeHandler<DateTime>` tương tự.

## 2. Enum lưu sai định dạng

- **Triệu chứng:** DB chứa `SingleChoice`, còn API trả `1` hoặc `"SingleChoice"`, trong khi spec yêu cầu `SINGLE_CHOICE`. CHECK constraint làm insert thất bại.
- **Nguyên nhân:** `HasConversion<string>()` dùng đúng tên enum trong C#.
- **Cách làm đúng:**
  - EF: một converter chung chuyển `PascalCase` ↔ `UPPER_SNAKE_CASE`, áp dụng trong `ConfigureConventions` cho mọi enum domain.
  - JSON: `options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));`
  - Frontend: kiểu union literal `"SINGLE_CHOICE" | ...`.
  - Test: round-trip cho mọi enum.

## 3. Parse số kiểu Việt: `"3,5"` thành 35

- **Nguyên nhân:** `decimal.Parse("3,5", CultureInfo.InvariantCulture)` dùng `NumberStyles.Number`, có `AllowThousands`; `,` là dấu phân cách hàng nghìn của invariant culture.
- **Cách làm đúng:** kiểm tra regex `^[+-]?\d{1,20}([.,]\d{1,10})?$`, thay `,` bằng `.`, rồi parse với `NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint` và `InvariantCulture` (`02-nghiep-vu.md` mục 3.3).
- Frontend **không** dùng `parseFloat` để kiểm tra (vì `parseFloat("3,5") = 3`), mà dùng cùng regex.

## 4. So sánh chuỗi tiếng Việt: NFC và NFD

- **Triệu chứng:** học viên gõ đúng "Hà Nội" nhưng bị chấm sai.
- **Nguyên nhân:** chuỗi dựng sẵn (NFC) và chuỗi tổ hợp (NFD, thường gặp khi copy từ Word hoặc macOS) khác nhau về byte.
- **Cách làm đúng:** `Normalize(NormalizationForm.FormC)` cả đáp án lẫn bài làm trước khi so sánh; nên chuẩn hóa NFC ngay khi lưu nội dung.
- Với `IgnoreAccent`: `đ/Đ` không tách được bằng NFD, phải map thủ công.
- Dùng `ToLowerInvariant()`, không dùng `ToLower()`, vì kết quả của `ToLower()` phụ thuộc culture của server.

## 5. Retry strategy và transaction tự mở

- **Triệu chứng:** `InvalidOperationException: The configured execution strategy 'SqlServerRetryingExecutionStrategy' does not support user-initiated transactions.`
- **Cách làm đúng:** bọc toàn bộ khối transaction:
  ```csharp
  var strategy = db.Database.CreateExecutionStrategy();
  await strategy.ExecuteAsync(async () => { await using var tx = await db.Database.BeginTransactionAsync(ct); ... });
  ```
- Khối bên trong phải an toàn khi bị chạy lại: không gửi email, không gọi dịch vụ ngoài bên trong khối.

## 6. Khóa dòng khi nộp bài / lưu đáp án (D-21)

- EF Core không có `SELECT ... FOR UPDATE`. Dùng:
  ```csharp
  var attempt = await db.ExamAttempts
      .FromSqlInterpolated($"SELECT * FROM ExamAttempts WITH (UPDLOCK, ROWLOCK) WHERE Id = {attemptId}")
      .SingleOrDefaultAsync(ct);
  ```
  Câu lệnh phải nằm **bên trong** transaction.
- Hoặc với job tự nộp, dùng chuyển trạng thái nguyên tử:
  ```csharp
  var claimed = await db.ExamAttempts
      .Where(a => a.Id == id && a.Status == AttemptStatus.InProgress)
      .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, AttemptStatus.AutoSubmitted) /* ... */, ct);
  if (claimed == 0) return; // người khác đã xử lý
  ```
- Không đọc trạng thái rồi kiểm tra ở C# mà không khóa, vì sẽ bị race condition.

## 7. Filtered unique index và lỗi trùng khóa

- Khi 2 request `start` chạy song song, request thua nhận `SqlException` số **2601** hoặc **2627** (được bọc trong `DbUpdateException`).
- **Cách làm đúng:** bắt đúng hai số lỗi này tại `IAttemptService.StartAsync`, tạo `DbContext` / transaction mới rồi trả về lượt đang làm. Không để lỗi lọt ra thành 500.
- Xác định lỗi trùng `Code` cũng theo cách này, rồi trả 409 `DUPLICATE_CODE`.
- EF: khai báo filtered index bằng `.HasFilter("[Status] = 'IN_PROGRESS'")`.

## 8. Sắp xếp lại thứ tự vi phạm unique

- **Triệu chứng:** đổi chỗ câu 1 và câu 2 thì vi phạm `UQ_ExamQuestions_Order`.
- **Nguyên nhân:** SQL Server kiểm tra ràng buộc theo từng câu lệnh, còn EF gửi nhiều câu `UPDATE` riêng lẻ.
- **Cách làm đúng:** trong một transaction, bước 1 đặt `QuestionOrder = QuestionOrder + 100000` (hoặc giá trị âm) cho mọi dòng bằng `ExecuteUpdateAsync`, bước 2 ghi thứ tự cuối cùng.

## 9. Multiple cascade paths

- **Triệu chứng:** migration lỗi `may cause cycles or multiple cascade paths`.
- **Nguyên nhân:** convention của EF đặt `Cascade` cho FK bắt buộc; `ExamAttempts → Exams` và `ExamAttempts → ExamVersions → Exams` tạo thành hai đường cascade.
- **Cách làm đúng:** trong `ConfigureConventions` / `OnModelCreating`, đặt mọi FK thành `DeleteBehavior.Restrict` (D-16):
  ```csharp
  foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
      fk.DeleteBehavior = DeleteBehavior.Restrict;
  ```

## 10. Khóa chính GUID

- EF Core với SQL Server tự sinh GUID tuần tự phía client (`SequentialGuidValueGenerator`). Đây là cách đúng.
- **Không** tự gán `Guid.NewGuid()` (ngẫu nhiên, gây phân mảnh index).
- **Không** dùng `Guid.CreateVersion7()` làm khóa clustered. SQL Server so sánh `uniqueidentifier` bắt đầu từ **6 byte cuối**, nên UUIDv7 không còn tuần tự trong SQL Server.
- Nếu cần Id trước khi `SaveChanges`: gọi `db.Add(entity)` rồi đọc `entity.Id` (EF sinh ngay khi Add).

## 11. `DateTime.UtcNow` rải rác trong code

- Không test được logic hết giờ.
- **Cách làm đúng:** inject `TimeProvider`; dùng `_time.GetUtcNow().UtcDateTime`. Test dùng `FakeTimeProvider` và `Advance(...)`.
- `BackgroundService` dùng `PeriodicTimer` dựa trên `TimeProvider` để test được worker.

## 12. N+1 và tải thừa dữ liệu

- Query đọc: `AsNoTracking()` và `Select` thẳng sang DTO.
- Tải lượt thi cho học viên: **một** query lấy câu hỏi, option và câu trả lời (projection), không lặp theo từng câu.
- Chấm điểm: tải toàn bộ `ExamQuestions` của version cùng option / đáp án chấp nhận một lần, rồi chấm trong bộ nhớ.
- Bật `QuerySplittingBehavior.SplitQuery` khi `Include` nhiều collection.
- Log cảnh báo khi một request thực hiện trên 20 query (dev).

## 13. Lộ đáp án qua serialize

- Không bao giờ trả entity EF ra API.
- DTO của học viên là type riêng; không kế thừa từ DTO của admin.
- Không dùng `[JsonIgnore(Condition = ...)]` để "ẩn tạm".
- Có test duyệt JSON (`08-kiem-thu.md` mục 4).

## 14. Frontend

- **Access token trong `localStorage`:** cấm (D-15).
- **Nhiều instance Axios hoặc refresh song song:** dùng một instance duy nhất và một promise refresh dùng chung.
- **`setInterval` làm nguồn thời gian:** timer bị trễ khi tab ở nền. Luôn tính lại từ `expiredAt` và `clockOffsetMs`.
- **Race khi autosave:** mỗi thời điểm chỉ một request lưu đang chạy; dùng `clientSeq`.
- **`parseFloat` cho số kiểu Việt:** xem mục 3.
- **`dangerouslySetInnerHTML` / `rehype-raw`:** cấm với nội dung câu hỏi.
- **Mất dữ liệu form:** React Hook Form với `shouldUnregister: false` khi đổi loại câu hỏi, và hỏi xác nhận trước khi xóa option.

## 15. Thư viện và license (D-19)

- **FluentAssertions ≥ 8** cần license trả phí cho mục đích thương mại. Dùng **AwesomeAssertions**.
- **MediatR, AutoMapper** đã chuyển sang license thương mại. **Không dùng.**
- **Swashbuckle:** chỉ dùng phần `SwaggerUI`; tài liệu OpenAPI do `Microsoft.AspNetCore.OpenApi` sinh ra.
- Thêm package mới: kiểm tra license (MIT / Apache-2.0 / BSD là ổn) và ghi vào `Directory.Packages.props`.

## 16. Migration

- Không gọi `Database.Migrate()` khi khởi động ở Staging / Production (`09-van-hanh.md` mục 4).
- Không sửa migration đã được áp dụng; tạo migration mới.
- Filtered index, CHECK constraint và `ROWVERSION` phải được khai báo trong EF configuration (`HasFilter`, `ToTable(t => t.HasCheckConstraint(...))`, `IsRowVersion()`), không chèn tay vào migration, để model và DB không lệch nhau.

## 17. Phát hiện khi triển khai M1

- **Hai filtered index trên cùng cột bị EF gộp làm một.** `HasIndex(v => v.ExamId)` gọi hai lần trả về cùng một index builder, nên filter và tên của lần gọi sau ghi đè lần trước. Phải dùng overload có tên: `HasIndex(v => v.ExamId, "UX_ExamVersions_OnePublished")`.
- **CHECK constraint trên cột enum không phân biệt hoa thường** khi DB dùng collation CI mặc định, nên `'Published'` lọt qua CHECK rồi làm EF ném lỗi khi đọc. Cách làm đúng: cột enum dùng `UseCollation("Latin1_General_100_BIN2")` (đã cấu hình chung trong `ConfigureConventions`).
- **DB default với `bool` / `int`:** nếu cấu hình `HasDefaultValue(true)` cho `IsActive`, EF coi giá trị `false` là "chưa gán" và để DB ghi `true`. Không cấu hình DB default cho những cột này; gán giá trị trong constructor của entity.
- **Code do EF sinh (migration) vi phạm analyzer**, ví dụ CA1861. Thư mục `Persistence/Migrations` được đánh dấu `generated_code` trong `.editorconfig`.
- **`dotnet ef migrations script --no-build` dùng bản build cũ**: sau khi thêm migration phải build lại trước khi sinh script.

## 18. Quyết định / Giả định

- Mọi mục trong file này là quy tắc bắt buộc. Nếu có lý do để làm khác thì phải ghi lại thành quyết định mới trong `00-muc-luc.md`.
