# 04a — Cơ sở dữ liệu (phần 1): quy ước, người dùng, ngân hàng câu hỏi, đề thi

Phần 2 (lượt thi, kết quả, audit, index, sơ đồ quan hệ) nằm ở `04b-csdl-luot-thi-ket-qua.md`.

## 1. Quy ước chung

- **Database:** `ELearningDb`, SQL Server 2019 trở lên. Collation mặc định `SQL_Latin1_General_CP1_CI_AS`; mọi so sánh nghiệp vụ quan trọng (username, email, đáp án) đều được **chuẩn hóa ở tầng ứng dụng**, không dựa vào collation.
- **Thời gian:** mọi timestamp là **UTC**, kiểu `DATETIME2(3)`. Không dùng `DATETIME`.
- **Khóa chính:** `UNIQUEIDENTIFIER`, sinh phía client bởi EF Core (sequential GUID cho SQL Server). `DEFAULT NEWSEQUENTIALID()` chỉ phục vụ script tay. Không tự sinh key bằng `Guid.NewGuid()` hay `Guid.CreateVersion7()` (xem `10-bay-ky-thuat.md`).
- **Enum:** lưu dạng chuỗi `UPPER_SNAKE_CASE`, kiểu `VARCHAR(40) COLLATE Latin1_General_100_BIN2`, có `CHECK` constraint. Collation nhị phân là bắt buộc, vì với collation CI mặc định, CHECK sẽ chấp nhận cả `'Published'`.
- **Giá trị mặc định:** do ứng dụng gán khi tạo entity. Migration **không** tạo `DEFAULT` cho cột (các `DEFAULT` trong DDL dưới đây chỉ để minh họa khi viết script tay). Lý do: EF Core coi `false` / `0` là "chưa gán" với cột có DB default, dẫn tới ghi sai giá trị (xem `10-bay-ky-thuat.md` mục 17).
- **Unique:** các ràng buộc `UQ_*` được hiện thực bằng unique index (EF Core), tương đương về mặt chức năng.
- **Tiền / điểm:** `DECIMAL(10,2)`. Số đáp án: `DECIMAL(30,10)`. Phần trăm: `DECIMAL(5,2)`.
- **Khóa ngoại:** mọi FK là `ON DELETE NO ACTION` (EF: `DeleteBehavior.Restrict`), vì hệ thống dùng xóa mềm và tránh lỗi *multiple cascade paths*. Ngoại lệ: bảng con thuần túy của draft (option / đáp án chấp nhận của `Questions` và `ExamQuestions` trong version DRAFT) được xóa bằng code trong cùng transaction, không dùng cascade.
- **Optimistic concurrency:** các bảng cho phép sửa có cột `RowVersion ROWVERSION`. Ghi dữ liệu đã cũ → HTTP 409 `CONCURRENCY_CONFLICT`.
- **Cột kiểm vết:** `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` trên các bảng có thể sửa.

## 2. Người dùng và phân quyền

```sql
CREATE TABLE Users (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserName            NVARCHAR(100)  NOT NULL,
    NormalizedUserName  NVARCHAR(100)  NOT NULL,          -- UPPER-invariant, dùng để tra cứu
    Email               NVARCHAR(255)  NOT NULL,
    NormalizedEmail     NVARCHAR(255)  NOT NULL,
    EmailConfirmed      BIT            NOT NULL CONSTRAINT DF_Users_EmailConfirmed DEFAULT 0,
    PasswordHash        NVARCHAR(500)  NULL,              -- NULL khi đã ẩn danh hóa
    SecurityStamp       UNIQUEIDENTIFIER NOT NULL,        -- đổi khi đổi mật khẩu / vai trò / khóa
    FullName            NVARCHAR(200)  NOT NULL,
    IsActive            BIT            NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
    MustChangePassword  BIT            NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT 0,
    AccessFailedCount   INT            NOT NULL CONSTRAINT DF_Users_AccessFailedCount DEFAULT 0,
    LockoutEnd          DATETIME2(3)   NULL,
    LastLoginAt         DATETIME2(3)   NULL,
    AnonymizedAt        DATETIME2(3)   NULL,
    CreatedAt           DATETIME2(3)   NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2(3)   NULL,
    RowVersion          ROWVERSION     NOT NULL,
    CONSTRAINT UQ_Users_NormalizedUserName UNIQUE (NormalizedUserName),
    CONSTRAINT UQ_Users_NormalizedEmail    UNIQUE (NormalizedEmail)
);

CREATE TABLE Roles (
    Id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Roles PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Code      VARCHAR(50)    NOT NULL CONSTRAINT UQ_Roles_Code UNIQUE,
    Name      NVARCHAR(100)  NOT NULL,
    IsSystem  BIT            NOT NULL CONSTRAINT DF_Roles_IsSystem DEFAULT 0,  -- ADMIN, STUDENT: không xóa/đổi code
    IsActive  BIT            NOT NULL CONSTRAINT DF_Roles_IsActive DEFAULT 1
);

CREATE TABLE Permissions (
    Id    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Code  VARCHAR(100)   NOT NULL CONSTRAINT UQ_Permissions_Code UNIQUE,
    Name  NVARCHAR(200)  NOT NULL
);

CREATE TABLE UserRoles (
    UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_UserRoles_User REFERENCES Users(Id),
    RoleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_UserRoles_Role REFERENCES Roles(Id),
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId)
);

CREATE TABLE RolePermissions (
    RoleId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_RolePermissions_Role REFERENCES Roles(Id),
    PermissionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_RolePermissions_Permission REFERENCES Permissions(Id),
    CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionId)
);

CREATE TABLE UserGroups (
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UserGroups PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Code        VARCHAR(100)   NOT NULL CONSTRAINT UQ_UserGroups_Code UNIQUE,
    Name        NVARCHAR(200)  NOT NULL,
    Description NVARCHAR(1000) NULL,
    IsActive    BIT            NOT NULL CONSTRAINT DF_UserGroups_IsActive DEFAULT 1,
    CreatedBy   UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_UserGroups_CreatedBy REFERENCES Users(Id),
    CreatedAt   DATETIME2(3)   NOT NULL CONSTRAINT DF_UserGroups_CreatedAt DEFAULT SYSUTCDATETIME(),
    RowVersion  ROWVERSION     NOT NULL
);

CREATE TABLE UserGroupMembers (
    GroupId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_UserGroupMembers_Group REFERENCES UserGroups(Id),
    UserId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_UserGroupMembers_User REFERENCES Users(Id),
    AddedAt  DATETIME2(3)     NOT NULL CONSTRAINT DF_UserGroupMembers_AddedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_UserGroupMembers PRIMARY KEY (GroupId, UserId)
);
CREATE INDEX IX_UserGroupMembers_User ON UserGroupMembers(UserId);

CREATE TABLE RefreshTokens (
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_RefreshTokens_User REFERENCES Users(Id),
    FamilyId          UNIQUEIDENTIFIER NOT NULL,   -- mọi token sinh ra từ một lần đăng nhập
    TokenHash         VARCHAR(128)   NOT NULL,     -- SHA-256 hex của token ngẫu nhiên 256 bit
    ExpiresAt         DATETIME2(3)   NOT NULL,
    CreatedAt         DATETIME2(3)   NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedByIp       VARCHAR(45)    NULL,         -- đủ cho IPv6
    UserAgent         NVARCHAR(500)  NULL,
    RevokedAt         DATETIME2(3)   NULL,
    RevokedReason     VARCHAR(30)    NULL
        CONSTRAINT CK_RefreshTokens_RevokedReason CHECK (RevokedReason IN
            ('ROTATED','LOGOUT','REUSE_DETECTED','PASSWORD_CHANGED','USER_DISABLED','ADMIN')),
    ReplacedByTokenId UNIQUEIDENTIFIER NULL,
    CONSTRAINT UQ_RefreshTokens_TokenHash UNIQUE (TokenHash)
);
CREATE INDEX IX_RefreshTokens_User   ON RefreshTokens(UserId);
CREATE INDEX IX_RefreshTokens_Family ON RefreshTokens(FamilyId);
```

**Seed:**
- Role: `ADMIN`, `STUDENT` (đều có `IsSystem = 1`).
- Permission: danh sách đầy đủ ở `07-bao-mat.md`.
- `ADMIN` có mọi permission. `STUDENT` không có permission quản trị nào; quyền của học viên được kiểm tra bằng policy riêng `StudentOnly`.

## 3. Ngân hàng câu hỏi

```sql
CREATE TABLE QuestionCategories (
    Id         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_QuestionCategories PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Code       VARCHAR(100)   NOT NULL CONSTRAINT UQ_QuestionCategories_Code UNIQUE,
    Name       NVARCHAR(200)  NOT NULL,
    IsActive   BIT            NOT NULL CONSTRAINT DF_QuestionCategories_IsActive DEFAULT 1,
    CreatedBy  UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_QuestionCategories_CreatedBy REFERENCES Users(Id),
    CreatedAt  DATETIME2(3)   NOT NULL CONSTRAINT DF_QuestionCategories_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt  DATETIME2(3)   NULL,
    RowVersion ROWVERSION     NOT NULL
);

CREATE TABLE Questions (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Questions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CategoryId          UNIQUEIDENTIFIER NULL CONSTRAINT FK_Questions_Category REFERENCES QuestionCategories(Id),
    Code                VARCHAR(100)   NOT NULL CONSTRAINT UQ_Questions_Code UNIQUE,
    Content             NVARCHAR(MAX)  NOT NULL,
    ContentFormat       VARCHAR(20)    NOT NULL CONSTRAINT DF_Questions_ContentFormat DEFAULT 'PLAIN'
        CONSTRAINT CK_Questions_ContentFormat CHECK (ContentFormat IN ('PLAIN','MARKDOWN')),
    QuestionType        VARCHAR(30)    NOT NULL
        CONSTRAINT CK_Questions_Type CHECK (QuestionType IN ('SINGLE_CHOICE','MULTIPLE_CHOICE','TRUE_FALSE','FILL_IN')),
    AnswerDataType      VARCHAR(20)    NULL
        CONSTRAINT CK_Questions_AnswerDataType CHECK (AnswerDataType IN ('TEXT','NUMBER')),
    CorrectAnswerNumber DECIMAL(30,10) NULL,
    NumericTolerance    DECIMAL(30,10) NULL,
    CaseSensitive       BIT            NOT NULL CONSTRAINT DF_Questions_CaseSensitive DEFAULT 0,
    IgnoreAccent        BIT            NOT NULL CONSTRAINT DF_Questions_IgnoreAccent DEFAULT 0,
    Explanation         NVARCHAR(MAX)  NULL,
    DefaultScore        DECIMAL(10,2)  NOT NULL CONSTRAINT DF_Questions_DefaultScore DEFAULT 1,
    IsActive            BIT            NOT NULL CONSTRAINT DF_Questions_IsActive DEFAULT 1,
    CreatedBy           UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Questions_CreatedBy REFERENCES Users(Id),
    CreatedAt           DATETIME2(3)   NOT NULL CONSTRAINT DF_Questions_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedBy           UNIQUEIDENTIFIER NULL CONSTRAINT FK_Questions_UpdatedBy REFERENCES Users(Id),
    UpdatedAt           DATETIME2(3)   NULL,
    RowVersion          ROWVERSION     NOT NULL,
    CONSTRAINT CK_Questions_FillIn CHECK (
        (QuestionType = 'FILL_IN' AND AnswerDataType IS NOT NULL) OR
        (QuestionType <> 'FILL_IN' AND AnswerDataType IS NULL)),
    CONSTRAINT CK_Questions_Number CHECK (
        AnswerDataType IS NULL OR AnswerDataType <> 'NUMBER' OR
        (CorrectAnswerNumber IS NOT NULL AND NumericTolerance IS NOT NULL AND NumericTolerance >= 0)),
    CONSTRAINT CK_Questions_Score CHECK (DefaultScore > 0)
);
CREATE INDEX IX_Questions_Category_Type ON Questions(CategoryId, QuestionType) INCLUDE (IsActive);

CREATE TABLE QuestionOptions (
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_QuestionOptions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    QuestionId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_QuestionOptions_Question REFERENCES Questions(Id),
    OptionCode   VARCHAR(10)    NOT NULL,
    Content      NVARCHAR(2000) NOT NULL,
    IsCorrect    BIT            NOT NULL CONSTRAINT DF_QuestionOptions_IsCorrect DEFAULT 0,
    DisplayOrder INT            NOT NULL,
    CONSTRAINT UQ_QuestionOptions_Code UNIQUE (QuestionId, OptionCode)
);

CREATE TABLE QuestionAcceptedAnswers (
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_QuestionAcceptedAnswers PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    QuestionId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_QuestionAcceptedAnswers_Question REFERENCES Questions(Id),
    AnswerText  NVARCHAR(1000) NOT NULL,     -- giữ nguyên như admin nhập, chuẩn hóa khi chấm
    DisplayOrder INT           NOT NULL,
    CONSTRAINT UQ_QuestionAcceptedAnswers_Order UNIQUE (QuestionId, DisplayOrder)
);
```

- Các quy tắc cần đếm số dòng (số option, số option đúng, số đáp án chấp nhận) được kiểm tra bằng FluentValidation và domain, không dùng CHECK.
- Sửa câu hỏi: xóa rồi tạo lại option và đáp án chấp nhận trong cùng transaction là chấp nhận được, vì câu hỏi trong ngân hàng không được tham chiếu trực tiếp khi chấm (D-01).

## 4. Đề thi và version

```sql
CREATE TABLE Exams (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Exams PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Code                VARCHAR(100)   NOT NULL CONSTRAINT UQ_Exams_Code UNIQUE,
    Name                NVARCHAR(300)  NOT NULL,
    Description         NVARCHAR(MAX)  NULL,
    Instructions        NVARCHAR(MAX)  NULL,          -- Markdown, hiển thị trước khi bắt đầu
    Status              VARCHAR(30)    NOT NULL CONSTRAINT DF_Exams_Status DEFAULT 'DRAFT'
        CONSTRAINT CK_Exams_Status CHECK (Status IN ('DRAFT','PUBLISHED','CLOSED')),
    StartAt             DATETIME2(3)   NULL,
    EndAt               DATETIME2(3)   NULL,
    MaxAttempts         INT            NOT NULL CONSTRAINT DF_Exams_MaxAttempts DEFAULT 1
        CONSTRAINT CK_Exams_MaxAttempts CHECK (MaxAttempts BETWEEN 1 AND 50),
    AccessMode          VARCHAR(20)    NOT NULL CONSTRAINT DF_Exams_AccessMode DEFAULT 'ASSIGNED'
        CONSTRAINT CK_Exams_AccessMode CHECK (AccessMode IN ('PUBLIC','ASSIGNED')),
    RetakeScoringPolicy VARCHAR(20)    NOT NULL CONSTRAINT DF_Exams_RetakeScoring DEFAULT 'HIGHEST'
        CONSTRAINT CK_Exams_RetakeScoring CHECK (RetakeScoringPolicy IN ('HIGHEST','LATEST')),
    CreatedBy           UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Exams_CreatedBy REFERENCES Users(Id),
    CreatedAt           DATETIME2(3)   NOT NULL CONSTRAINT DF_Exams_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedBy           UNIQUEIDENTIFIER NULL CONSTRAINT FK_Exams_UpdatedBy REFERENCES Users(Id),
    UpdatedAt           DATETIME2(3)   NULL,
    ClosedAt            DATETIME2(3)   NULL,
    RowVersion          ROWVERSION     NOT NULL,
    CONSTRAINT CK_Exams_Window CHECK (StartAt IS NULL OR EndAt IS NULL OR StartAt < EndAt)
);
CREATE INDEX IX_Exams_Status ON Exams(Status) INCLUDE (StartAt, EndAt, AccessMode);

CREATE TABLE ExamVersions (
    Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ExamVersions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    ExamId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamVersions_Exam REFERENCES Exams(Id),
    VersionNumber    INT            NOT NULL,
    Status           VARCHAR(30)    NOT NULL
        CONSTRAINT CK_ExamVersions_Status CHECK (Status IN ('DRAFT','PUBLISHED','ARCHIVED')),
    DurationMinutes  INT            NOT NULL CONSTRAINT CK_ExamVersions_Duration CHECK (DurationMinutes BETWEEN 1 AND 600),
    PassPercentage   DECIMAL(5,2)   NULL CONSTRAINT CK_ExamVersions_Pass CHECK (PassPercentage BETWEEN 0 AND 100),
    ScoreVisibility  VARCHAR(30)    NOT NULL CONSTRAINT DF_ExamVersions_ScoreVisibility DEFAULT 'IMMEDIATE'
        CONSTRAINT CK_ExamVersions_ScoreVisibility CHECK (ScoreVisibility IN ('IMMEDIATE','AFTER_EXAM_END','HIDDEN')),
    ReviewPolicy     VARCHAR(30)    NOT NULL CONSTRAINT DF_ExamVersions_ReviewPolicy DEFAULT 'NEVER'
        CONSTRAINT CK_ExamVersions_ReviewPolicy CHECK (ReviewPolicy IN ('NEVER','AFTER_SUBMIT','AFTER_EXAM_END','AFTER_LAST_ATTEMPT')),
    ShuffleQuestions BIT            NOT NULL CONSTRAINT DF_ExamVersions_ShuffleQ DEFAULT 0,   -- sau MVP
    ShuffleOptions   BIT            NOT NULL CONSTRAINT DF_ExamVersions_ShuffleO DEFAULT 0,   -- sau MVP
    QuestionCount    INT            NULL,      -- tính lúc publish
    MaxScore         DECIMAL(10,2)  NULL,      -- tính lúc publish
    PublishedAt      DATETIME2(3)   NULL,
    PublishedBy      UNIQUEIDENTIFIER NULL CONSTRAINT FK_ExamVersions_PublishedBy REFERENCES Users(Id),
    ArchivedAt       DATETIME2(3)   NULL,
    CreatedBy        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamVersions_CreatedBy REFERENCES Users(Id),
    CreatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_ExamVersions_CreatedAt DEFAULT SYSUTCDATETIME(),
    RowVersion       ROWVERSION     NOT NULL,
    CONSTRAINT UQ_ExamVersions_Number UNIQUE (ExamId, VersionNumber)
);
-- Tối đa 1 version PUBLISHED và 1 version DRAFT cho mỗi đề (D-04)
CREATE UNIQUE INDEX UX_ExamVersions_OnePublished ON ExamVersions(ExamId) WHERE Status = 'PUBLISHED';
CREATE UNIQUE INDEX UX_ExamVersions_OneDraft     ON ExamVersions(ExamId) WHERE Status = 'DRAFT';

CREATE TABLE ExamQuestions (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ExamQuestions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    ExamVersionId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamQuestions_Version REFERENCES ExamVersions(Id),
    SourceQuestionId    UNIQUEIDENTIFIER NULL CONSTRAINT FK_ExamQuestions_SourceQuestion REFERENCES Questions(Id),
    SourceRowVersion    BINARY(8)      NULL,      -- RowVersion của câu hỏi gốc lúc copy (phát hiện thay đổi)
    CopiedAt            DATETIME2(3)   NOT NULL,
    QuestionOrder       INT            NOT NULL,
    Content             NVARCHAR(MAX)  NOT NULL,
    ContentFormat       VARCHAR(20)    NOT NULL CONSTRAINT CK_ExamQuestions_ContentFormat CHECK (ContentFormat IN ('PLAIN','MARKDOWN')),
    QuestionType        VARCHAR(30)    NOT NULL
        CONSTRAINT CK_ExamQuestions_Type CHECK (QuestionType IN ('SINGLE_CHOICE','MULTIPLE_CHOICE','TRUE_FALSE','FILL_IN')),
    AnswerDataType      VARCHAR(20)    NULL CONSTRAINT CK_ExamQuestions_AnswerDataType CHECK (AnswerDataType IN ('TEXT','NUMBER')),
    CorrectAnswerNumber DECIMAL(30,10) NULL,
    NumericTolerance    DECIMAL(30,10) NULL,
    CaseSensitive       BIT            NOT NULL,
    IgnoreAccent        BIT            NOT NULL,
    Explanation         NVARCHAR(MAX)  NULL,
    Score               DECIMAL(10,2)  NOT NULL CONSTRAINT CK_ExamQuestions_Score CHECK (Score > 0),
    IsVoided            BIT            NOT NULL CONSTRAINT DF_ExamQuestions_IsVoided DEFAULT 0,   -- D-11
    VoidedAt            DATETIME2(3)   NULL,
    VoidedBy            UNIQUEIDENTIFIER NULL CONSTRAINT FK_ExamQuestions_VoidedBy REFERENCES Users(Id),
    CONSTRAINT UQ_ExamQuestions_Order UNIQUE (ExamVersionId, QuestionOrder)
);
-- Không thêm trùng một câu hỏi nguồn vào cùng version
CREATE UNIQUE INDEX UX_ExamQuestions_Source ON ExamQuestions(ExamVersionId, SourceQuestionId)
    WHERE SourceQuestionId IS NOT NULL;

CREATE TABLE ExamQuestionOptions (
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ExamQuestionOptions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    ExamQuestionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamQuestionOptions_Question REFERENCES ExamQuestions(Id),
    OptionCode     VARCHAR(10)    NOT NULL,
    Content        NVARCHAR(2000) NOT NULL,
    IsCorrect      BIT            NOT NULL,    -- KHÔNG BAO GIỜ trả cho học viên khi đang thi
    DisplayOrder   INT            NOT NULL,
    CONSTRAINT UQ_ExamQuestionOptions_Code UNIQUE (ExamQuestionId, OptionCode)
);

CREATE TABLE ExamQuestionAcceptedAnswers (
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ExamQuestionAcceptedAnswers PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    ExamQuestionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamQuestionAcceptedAnswers_Question REFERENCES ExamQuestions(Id),
    AnswerText     NVARCHAR(1000) NOT NULL,
    DisplayOrder   INT            NOT NULL,
    CONSTRAINT UQ_ExamQuestionAcceptedAnswers_Order UNIQUE (ExamQuestionId, DisplayOrder)
);

CREATE TABLE ExamAssignments (                     -- D-10
    Id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ExamAssignments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    ExamId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamAssignments_Exam REFERENCES Exams(Id),
    GroupId   UNIQUEIDENTIFIER NULL CONSTRAINT FK_ExamAssignments_Group REFERENCES UserGroups(Id),
    UserId    UNIQUEIDENTIFIER NULL CONSTRAINT FK_ExamAssignments_User  REFERENCES Users(Id),
    CreatedBy UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamAssignments_CreatedBy REFERENCES Users(Id),
    CreatedAt DATETIME2(3)     NOT NULL CONSTRAINT DF_ExamAssignments_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_ExamAssignments_Target CHECK ((GroupId IS NULL) <> (UserId IS NULL))
);
CREATE UNIQUE INDEX UX_ExamAssignments_Group ON ExamAssignments(ExamId, GroupId) WHERE GroupId IS NOT NULL;
CREATE UNIQUE INDEX UX_ExamAssignments_User  ON ExamAssignments(ExamId, UserId)  WHERE UserId  IS NOT NULL;
CREATE INDEX IX_ExamAssignments_GroupId ON ExamAssignments(GroupId) WHERE GroupId IS NOT NULL;
CREATE INDEX IX_ExamAssignments_UserId  ON ExamAssignments(UserId)  WHERE UserId  IS NOT NULL;

CREATE TABLE ExamUserOverrides (                   -- cấp thêm lượt cho từng học viên
    ExamId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamUserOverrides_Exam REFERENCES Exams(Id),
    UserId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamUserOverrides_User REFERENCES Users(Id),
    ExtraAttempts INT            NOT NULL CONSTRAINT CK_ExamUserOverrides_Extra CHECK (ExtraAttempts BETWEEN 0 AND 50),
    Note          NVARCHAR(500)  NULL,
    UpdatedBy     UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ExamUserOverrides_UpdatedBy REFERENCES Users(Id),
    UpdatedAt     DATETIME2(3)   NOT NULL,
    CONSTRAINT PK_ExamUserOverrides PRIMARY KEY (ExamId, UserId)
);

CREATE TABLE AnswerKeyCorrections (                -- D-11: mọi lần sửa đáp án / hủy câu
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AnswerKeyCorrections PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    ExamQuestionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AnswerKeyCorrections_Question REFERENCES ExamQuestions(Id),
    CorrectionType VARCHAR(20)    NOT NULL CONSTRAINT CK_AnswerKeyCorrections_Type CHECK (CorrectionType IN ('ANSWER_KEY','VOID')),
    OldKeyJson     NVARCHAR(MAX)  NOT NULL,
    NewKeyJson     NVARCHAR(MAX)  NOT NULL,
    Reason         NVARCHAR(1000) NOT NULL,
    AffectedAttemptCount INT      NOT NULL,
    CorrectedBy    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AnswerKeyCorrections_By REFERENCES Users(Id),
    CorrectedAt    DATETIME2(3)   NOT NULL
);
CREATE INDEX IX_AnswerKeyCorrections_Question ON AnswerKeyCorrections(ExamQuestionId);
```

**Tính bất biến:**
- Khi version không còn ở `DRAFT`, tầng ứng dụng từ chối mọi thay đổi trên `ExamQuestions`, `ExamQuestionOptions` và `ExamQuestionAcceptedAnswers` của version đó (lỗi `VERSION_IMMUTABLE`). Ngoại lệ duy nhất là `IAnswerKeyService` (D-11).
- Khuyến nghị thêm một `SaveChangesInterceptor` để chặn ở tầng hạ tầng, làm lớp bảo vệ thứ hai.

## 5. Quyết định / Giả định

- **(M1) Cột enum dùng `VARCHAR(40)` và collation `Latin1_General_100_BIN2`**, thay cho `VARCHAR(20/30)` như DDL ban đầu. Một độ dài chung cho mọi enum, và collation nhị phân để CHECK constraint / filtered index so khớp đúng chữ hoa. Integration test đã phát hiện lỗi khi thiếu collation này.
- **(M1) Migration không tạo `DEFAULT`**; giá trị mặc định do entity gán.
- **(M1) EF tự tạo thêm index cho các cột FK chưa được index nào bao phủ** (ví dụ `IX_ExamAttempts_CancelledBy`). Chấp nhận, vì chi phí nhỏ và giúp kiểm tra FK khi xóa.

- **Chỉ tiết kiệm ở `AttemptQuestions` (D-01).** Các bảng snapshot của version đầy đủ như spec gốc; bảng của lượt thi thì gọn lại.
- **`CorrectAnswerText` bị thay** bằng bảng `QuestionAcceptedAnswers` / `ExamQuestionAcceptedAnswers` (D-12).
- **`AccessMode` mặc định `ASSIGNED`** (an toàn hơn). Muốn mở cho mọi học viên thì admin phải chọn `PUBLIC` một cách chủ động.
- **Không có `IsDeleted`:** dùng `IsActive` và quy tắc xóa ở `02-nghiep-vu.md` mục 10 (D-16). Đề chưa từng publish thì xóa cứng, xóa bảng con bằng code.
- **Thêm `NormalizedUserName` / `NormalizedEmail`** để tính duy nhất không phụ thuộc collation.
- **`CreatedByIp VARCHAR(45)`** thay cho `VARCHAR(50)` (đủ cho IPv6, dư không cần thiết).
- **Spec gốc có `Exams.PassScore`, `DurationMinutes`, `IsShowResult`, `IsShowCorrectAnswer`.** Các cột này đã chuyển sang `ExamVersions` với tên mới (D-03, D-09).
