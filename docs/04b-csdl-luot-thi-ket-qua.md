# 04b — Cơ sở dữ liệu (phần 2): lượt thi, kết quả, audit, index

Quy ước chung xem `04a-csdl-danh-muc-de-thi.md` mục 1.

## 1. Lượt thi

```sql
CREATE TABLE ExamAttempts (
    Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ExamAttempts PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    ExamId               UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamAttempts_Exam    REFERENCES Exams(Id),
    ExamVersionId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamAttempts_Version REFERENCES ExamVersions(Id),
    UserId               UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamAttempts_User    REFERENCES Users(Id),
    AttemptNumber        INT            NOT NULL,
    Status               VARCHAR(30)    NOT NULL
        CONSTRAINT CK_ExamAttempts_Status CHECK (Status IN ('IN_PROGRESS','SUBMITTED','AUTO_SUBMITTED','CANCELLED')),
    SubmitReason         VARCHAR(30)    NULL
        CONSTRAINT CK_ExamAttempts_SubmitReason CHECK (SubmitReason IN ('STUDENT','TIME_EXPIRED','FORCED_BY_ADMIN')),
    StartedAt            DATETIME2(3)   NOT NULL,
    ExpiredAt            DATETIME2(3)   NOT NULL,
    TimeExtensionMinutes INT            NOT NULL CONSTRAINT DF_ExamAttempts_TimeExtension DEFAULT 0,
    SubmittedAt          DATETIME2(3)   NULL,
    CancelledAt          DATETIME2(3)   NULL,
    CancelledBy          UNIQUEIDENTIFIER NULL CONSTRAINT FK_ExamAttempts_CancelledBy REFERENCES Users(Id),
    CancelReason         NVARCHAR(500)  NULL,
    QuestionCount        INT            NOT NULL,
    StartedIp            VARCHAR(45)    NULL,
    StartedUserAgent     NVARCHAR(500)  NULL,
    SubmittedIp          VARCHAR(45)    NULL,
    CreatedAt            DATETIME2(3)   NOT NULL CONSTRAINT DF_ExamAttempts_CreatedAt DEFAULT SYSUTCDATETIME(),
    RowVersion           ROWVERSION     NOT NULL,
    CONSTRAINT UQ_ExamAttempts_User_Exam_Number UNIQUE (UserId, ExamId, AttemptNumber),
    CONSTRAINT CK_ExamAttempts_Time CHECK (ExpiredAt > StartedAt),
    CONSTRAINT CK_ExamAttempts_Submitted CHECK (
        (Status IN ('SUBMITTED','AUTO_SUBMITTED') AND SubmittedAt IS NOT NULL AND SubmitReason IS NOT NULL) OR
        (Status IN ('IN_PROGRESS','CANCELLED')))
);
-- D-07: mỗi (UserId, ExamId) tối đa 1 lượt đang làm
CREATE UNIQUE INDEX UX_ExamAttempts_OneInProgress ON ExamAttempts(UserId, ExamId) WHERE Status = 'IN_PROGRESS';
-- Job tự nộp (D-05)
CREATE INDEX IX_ExamAttempts_InProgress_Expired ON ExamAttempts(ExpiredAt) WHERE Status = 'IN_PROGRESS';
CREATE INDEX IX_ExamAttempts_Exam    ON ExamAttempts(ExamId, Status) INCLUDE (UserId, SubmittedAt);
CREATE INDEX IX_ExamAttempts_Version ON ExamAttempts(ExamVersionId, Status);
```

- `UQ_ExamAttempts_User_Exam_Number` đã phục vụ luôn truy vấn "đếm lượt của user cho đề", nên không cần thêm index `(UserId, ExamId)` riêng.
- **Tính nhất quán `ExamId` và `ExamVersionId`:** `ExamId` là cột phi chuẩn hóa để truy vấn nhanh. Domain đảm bảo `ExamVersion.ExamId = ExamId`. Có thể thêm composite FK `(ExamVersionId, ExamId) → ExamVersions(Id, ExamId)` kèm unique `(Id, ExamId)` trên `ExamVersions` nếu muốn DB tự enforce.
- **Spec gốc có `TotalScore` và `CorrectCount` trong bảng này.** Hai cột đã bị bỏ (D-20); dùng `ExamResults`.

```sql
CREATE TABLE AttemptQuestions (                    -- D-01: chỉ tham chiếu + thứ tự, không copy nội dung
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AttemptQuestions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    AttemptId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AttemptQuestions_Attempt      REFERENCES ExamAttempts(Id),
    ExamQuestionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AttemptQuestions_ExamQuestion REFERENCES ExamQuestions(Id),
    QuestionOrder  INT            NOT NULL,        -- thứ tự hiển thị trong lượt này
    OptionOrder    VARCHAR(200)   NULL,            -- ví dụ 'C,A,D,B'; NULL = theo ExamQuestionOptions.DisplayOrder
    CONSTRAINT UQ_AttemptQuestions_Order    UNIQUE (AttemptId, QuestionOrder),
    CONSTRAINT UQ_AttemptQuestions_Question UNIQUE (AttemptId, ExamQuestionId)
);

CREATE TABLE AttemptAnswers (
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AttemptAnswers PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    AttemptQuestionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AttemptAnswers_Question REFERENCES AttemptQuestions(Id),
    AnswerText        NVARCHAR(MAX)  NULL,         -- chuỗi gốc của FILL_IN (TEXT / NUMBER) và bài tự luận (≤ 20.000 ký tự)
    ManualScore       DECIMAL(10,2)  NULL,         -- điểm chấm tay (ESSAY); giữ nguyên khi chấm lại tự động
    ManualComment     NVARCHAR(2000) NULL,
    ManualGradedBy    UNIQUEIDENTIFIER NULL REFERENCES Users(Id),
    ManualGradedAt    DATETIME2(3)   NULL,
    AnswerNumber      DECIMAL(30,10) NULL,         -- giá trị đã parse khi NUMBER
    IsAnswered        BIT            NOT NULL CONSTRAINT DF_AttemptAnswers_IsAnswered DEFAULT 0,
    IsMarkedForReview BIT            NOT NULL CONSTRAINT DF_AttemptAnswers_IsMarked DEFAULT 0,
    ClientSeq         BIGINT         NOT NULL CONSTRAINT DF_AttemptAnswers_ClientSeq DEFAULT 0,   -- D-18
    AnsweredAt        DATETIME2(3)   NULL,         -- giờ server lần ghi cuối
    SaveCount         INT            NOT NULL CONSTRAINT DF_AttemptAnswers_SaveCount DEFAULT 0,
    IsCorrect         BIT            NULL,         -- điền khi chấm
    Score             DECIMAL(10,2)  NULL,
    GradedAt          DATETIME2(3)   NULL,
    CONSTRAINT UQ_AttemptAnswers_Question UNIQUE (AttemptQuestionId)
);

CREATE TABLE AttemptAnswerOptions (                -- option đã chọn (SINGLE / MULTIPLE / TRUE_FALSE)
    AttemptAnswerId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AttemptAnswerOptions_Answer REFERENCES AttemptAnswers(Id),
    OptionCode      VARCHAR(10)    NOT NULL,
    CONSTRAINT PK_AttemptAnswerOptions PRIMARY KEY (AttemptAnswerId, OptionCode)
);

CREATE TABLE AttemptEvents (                       -- ghi nhận để răn đe / đối chiếu, không phải chống gian lận tuyệt đối
    Id         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AttemptEvents PRIMARY KEY,
    AttemptId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AttemptEvents_Attempt REFERENCES ExamAttempts(Id),
    EventType  VARCHAR(40)    NOT NULL CONSTRAINT CK_AttemptEvents_Type CHECK (EventType IN
        ('VISIBILITY_HIDDEN','VISIBILITY_VISIBLE','WINDOW_BLUR','WINDOW_FOCUS',
         'FULLSCREEN_EXIT','OFFLINE','ONLINE','MULTI_TAB_DETECTED','PAGE_RELOAD','PASTE')),
    ClientTime DATETIME2(3)   NULL,                -- chỉ để tham khảo
    ServerTime DATETIME2(3)   NOT NULL CONSTRAINT DF_AttemptEvents_ServerTime DEFAULT SYSUTCDATETIME(),
    IpAddress  VARCHAR(45)    NULL,
    Detail     NVARCHAR(500)  NULL
);
CREATE INDEX IX_AttemptEvents_Attempt ON AttemptEvents(AttemptId, ServerTime);
```

- Mỗi lượt thi ghi tối đa **1.000 sự kiện**; vượt ngưỡng thì bỏ qua và chỉ tăng một bộ đếm trong log.
- **Lưu đáp án:**
  - Câu trắc nghiệm: xóa `AttemptAnswerOptions` cũ rồi chèn mới, cùng transaction với việc cập nhật `AttemptAnswers`.
  - Câu điền: chỉ cập nhật `AnswerText` và `AnswerNumber`.
  - Cả hai trường hợp chỉ thực hiện khi `@ClientSeq > ClientSeq`.

## 2. Kết quả

```sql
-- (sau MVP) ExamResults có thêm PendingManualCount INT NOT NULL: số câu tự luận chờ chấm tay (02-nghiep-vu mục 2.3)
CREATE TABLE ExamResults (                         -- D-20: nguồn điểm duy nhất
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ExamResults PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    AttemptId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamResults_Attempt REFERENCES ExamAttempts(Id),
    ExamId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamResults_Exam    REFERENCES Exams(Id),
    ExamVersionId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamResults_Version REFERENCES ExamVersions(Id),
    UserId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamResults_User    REFERENCES Users(Id),
    TotalQuestion   INT            NOT NULL,
    AnsweredCount   INT            NOT NULL,
    CorrectCount    INT            NOT NULL,
    TotalScore      DECIMAL(10,2)  NOT NULL,
    MaxScore        DECIMAL(10,2)  NOT NULL CONSTRAINT CK_ExamResults_MaxScore CHECK (MaxScore > 0),
    Percentage      DECIMAL(5,2)   NOT NULL,
    Passed          BIT            NULL,           -- NULL khi version không có PassPercentage
    StartedAt       DATETIME2(3)   NOT NULL,
    SubmittedAt     DATETIME2(3)   NOT NULL,
    DurationSeconds INT            NOT NULL,       -- SubmittedAt − StartedAt, giới hạn bởi ExpiredAt
    GradingRevision INT            NOT NULL CONSTRAINT DF_ExamResults_GradingRevision DEFAULT 1,
    GradedAt        DATETIME2(3)   NOT NULL,
    RegradedAt      DATETIME2(3)   NULL,
    CONSTRAINT UQ_ExamResults_Attempt UNIQUE (AttemptId)
);
CREATE INDEX IX_ExamResults_Exam ON ExamResults(ExamId) INCLUDE (UserId, TotalScore, Percentage, Passed, SubmittedAt);
CREATE INDEX IX_ExamResults_User ON ExamResults(UserId, SubmittedAt DESC);

CREATE TABLE ExamResultHistory (                   -- D-11: điểm trước mỗi lần chấm lại
    Id                 BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExamResultHistory PRIMARY KEY,
    ExamResultId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamResultHistory_Result REFERENCES ExamResults(Id),
    GradingRevision    INT            NOT NULL,
    CorrectCount       INT            NOT NULL,
    TotalScore         DECIMAL(10,2)  NOT NULL,
    Percentage         DECIMAL(5,2)   NOT NULL,
    Passed             BIT            NULL,
    AnswerKeyCorrectionId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT FK_ExamResultHistory_Correction REFERENCES AnswerKeyCorrections(Id),
    RecordedAt         DATETIME2(3)   NOT NULL
);
CREATE INDEX IX_ExamResultHistory_Result ON ExamResultHistory(ExamResultId);
```

**Điểm chính thức theo `RetakeScoringPolicy` (D-08)** được tính khi truy vấn:

```sql
SELECT r.*
FROM (
    SELECT r.*, ROW_NUMBER() OVER (
        PARTITION BY r.UserId
        ORDER BY CASE WHEN e.RetakeScoringPolicy = 'HIGHEST' THEN r.TotalScore END DESC,
                 r.SubmittedAt DESC) AS rn
    FROM ExamResults r
    JOIN ExamAttempts a ON a.Id = r.AttemptId AND a.Status <> 'CANCELLED'
    JOIN Exams e ON e.Id = r.ExamId
    WHERE r.ExamId = @ExamId
) r
WHERE r.rn = 1;
```

Với `LATEST`, biểu thức `CASE` trả về NULL nên thứ tự chỉ còn theo `SubmittedAt DESC`. Với `HIGHEST`, nếu hai lượt bằng điểm thì lấy lượt nộp sau.

## 3. Audit log

```sql
CREATE TABLE AuditLogs (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
    UserId      UNIQUEIDENTIFIER NULL,             -- không có FK: log phải tồn tại độc lập
    Action      VARCHAR(100)   NOT NULL,
    EntityName  VARCHAR(100)   NULL,
    EntityId    UNIQUEIDENTIFIER NULL,
    OldValue    NVARCHAR(MAX)  NULL,               -- JSON, đã loại bỏ trường nhạy cảm
    NewValue    NVARCHAR(MAX)  NULL,
    Reason      NVARCHAR(1000) NULL,
    IpAddress   VARCHAR(45)    NULL,
    UserAgent   NVARCHAR(500)  NULL,
    TraceId     VARCHAR(64)    NULL,
    CreatedAt   DATETIME2(3)   NOT NULL CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_AuditLogs_CreatedAt ON AuditLogs(CreatedAt DESC);
CREATE INDEX IX_AuditLogs_User      ON AuditLogs(UserId, CreatedAt DESC);
CREATE INDEX IX_AuditLogs_Entity    ON AuditLogs(EntityName, EntityId);
```

- Danh sách `Action` và quy tắc loại bỏ trường nhạy cảm: xem `07-bao-mat.md`.
- **Lưu trữ:** giữ ít nhất 2 năm *(cần xác nhận)*; job dọn dẹp chuyển dữ liệu cũ sang bảng lưu trữ hoặc file.

## 4. Tổng hợp index

| Index | Mục đích |
|---|---|
| `UX_ExamVersions_OnePublished`, `UX_ExamVersions_OneDraft` | D-04 |
| `UX_ExamQuestions_Source` | Không thêm trùng câu hỏi vào một version |
| `UX_ExamAttempts_OneInProgress` | D-07; chống race khi 2 request start song song |
| `IX_ExamAttempts_InProgress_Expired` | Job tự nộp |
| `IX_ExamAttempts_Exam`, `IX_ExamAttempts_Version` | Danh sách lượt thi cho admin; chấm lại |
| `UQ_RefreshTokens_TokenHash`, `IX_RefreshTokens_User`, `IX_RefreshTokens_Family` | Refresh, thu hồi, phát hiện dùng lại token |
| `IX_ExamResults_Exam`, `IX_ExamResults_User` | Báo cáo, lịch sử học viên |
| `IX_UserGroupMembers_User`, `IX_ExamAssignments_*` | Lọc đề mà học viên được thi |
| `IX_AuditLogs_*` | Tra cứu audit |

**Đã bỏ so với spec gốc** vì trùng với index mà unique constraint tự tạo:
- `IX_QuestionOptions_Question`
- `IX_ExamVersions_Exam`
- `IX_ExamQuestions_Version`
- `IX_AttemptQuestions_Attempt`
- `IX_AttemptAnswers_Question`

Cũng bỏ `IX_ExamAttempts_Status` (độ chọn lọc thấp), thay bằng filtered index. EF Core sẽ không tạo thêm index cho FK khi đã có index bắt đầu bằng cột đó.

## 5. Sơ đồ quan hệ

```text
Users ──< UserRoles >── Roles ──< RolePermissions >── Permissions
  │ ──< UserGroupMembers >── UserGroups ──< ExamAssignments
  │ ──< RefreshTokens
  └──< ExamAttempts

QuestionCategories ──< Questions ──< QuestionOptions
                                 └──< QuestionAcceptedAnswers

Exams ──< ExamVersions ──< ExamQuestions ──< ExamQuestionOptions
  │                           │         └──< ExamQuestionAcceptedAnswers
  │                           └──< AnswerKeyCorrections
  ├──< ExamAssignments
  └──< ExamUserOverrides

ExamAttempts ──< AttemptQuestions ──> ExamQuestions   (tham chiếu snapshot, D-01)
     │                 └── AttemptAnswers ──< AttemptAnswerOptions
     ├──< AttemptEvents
     └── ExamResults ──< ExamResultHistory ──> AnswerKeyCorrections
```

## 6. Các ràng buộc bất biến

1. Version đã publish không bị sửa, trừ `IsCorrect`, đáp án chấp nhận, `CorrectAnswerNumber`, `NumericTolerance` và `IsVoided`, qua `IAnswerKeyService` (D-11).
2. Một lượt thi thuộc đúng một user và một version.
3. Mỗi `ExamQuestion` của version xuất hiện đúng một lần trong mỗi lượt thi.
4. Mỗi câu của lượt thi có đúng một `AttemptAnswers`.
5. Lượt thi đã nộp hoặc đã hủy thì không nhận thêm câu trả lời.
6. Mỗi lượt thi có tối đa một `ExamResults`.
7. Số thứ tự lượt thi là duy nhất theo user và đề, không được dùng lại.
8. Loại câu hỏi quyết định cấu trúc câu trả lời.
9. Không trả đáp án đúng cho học viên trước khi chính sách hiển thị cho phép.

## 7. Quyết định / Giả định

- **D-20:** bỏ `ExamAttempts.TotalScore` và `CorrectCount` để tránh hai nguồn dữ liệu. `ExamResults` bổ sung `ExamVersionId`, `StartedAt`, `SubmittedAt`, `DurationSeconds`, `AnsweredCount`.
- **Bảng `AttemptQuestionOptions` của spec gốc bị bỏ**, thay bằng cột `OptionOrder`. Khi version bật `ShuffleOptions`, cột lưu thứ tự đã xáo của câu chọn một / chọn nhiều; còn lại là NULL.
- **`AttemptEvents` được thêm vào MVP** vì chi phí thấp và rất hữu ích khi xử lý khiếu nại.
- **Chấm lại chỉ giữ lịch sử điểm tổng** (`ExamResultHistory`). Lịch sử điểm từng câu có thể dựng lại từ `AnswerKeyCorrections` khi cần.
- **Không đặt FK trên `AuditLogs.UserId`** để log không bị chặn hoặc bị mất khi dữ liệu user thay đổi.
