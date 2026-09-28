using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EntityName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TraceId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SecurityStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    AnonymizedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permission",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Role",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Exams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Instructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    StartAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    EndAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    AccessMode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RetakeScoringPolicy = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exams", x => x.Id);
                    table.CheckConstraint("CK_Exams_AccessMode", "[AccessMode] IN ('PUBLIC','ASSIGNED')");
                    table.CheckConstraint("CK_Exams_MaxAttempts", "[MaxAttempts] BETWEEN 1 AND 50");
                    table.CheckConstraint("CK_Exams_RetakeScoring", "[RetakeScoringPolicy] IN ('HIGHEST','LATEST')");
                    table.CheckConstraint("CK_Exams_Status", "[Status] IN ('DRAFT','PUBLISHED','CLOSED')");
                    table.CheckConstraint("CK_Exams_Window", "[StartAt] IS NULL OR [EndAt] IS NULL OR [StartAt] < [EndAt]");
                    table.ForeignKey(
                        name: "FK_Exams_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Exams_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionCategories_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedByIp = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RevokedReason = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true, collation: "Latin1_General_100_BIN2"),
                    ReplacedByTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.CheckConstraint("CK_RefreshTokens_RevokedReason", "[RevokedReason] IN ('ROTATED','LOGOUT','REUSE_DETECTED','PASSWORD_CHANGED','USER_DISABLED','ADMIN')");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGroups_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Role",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamUserOverrides",
                columns: table => new
                {
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtraAttempts = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamUserOverrides", x => new { x.ExamId, x.UserId });
                    table.CheckConstraint("CK_ExamUserOverrides_Extra", "[ExtraAttempts] BETWEEN 0 AND 50");
                    table.ForeignKey(
                        name: "FK_ExamUserOverrides_Exam",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamUserOverrides_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamUserOverrides_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    PassPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 10, scale: 2, nullable: true),
                    ScoreVisibility = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ReviewPolicy = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ShuffleQuestions = table.Column<bool>(type: "bit", nullable: false),
                    ShuffleOptions = table.Column<bool>(type: "bit", nullable: false),
                    QuestionCount = table.Column<int>(type: "int", nullable: true),
                    MaxScore = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    PublishedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamVersions", x => x.Id);
                    table.CheckConstraint("CK_ExamVersions_Duration", "[DurationMinutes] BETWEEN 1 AND 600");
                    table.CheckConstraint("CK_ExamVersions_Pass", "[PassPercentage] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_ExamVersions_ReviewPolicy", "[ReviewPolicy] IN ('NEVER','AFTER_SUBMIT','AFTER_EXAM_END','AFTER_LAST_ATTEMPT')");
                    table.CheckConstraint("CK_ExamVersions_ScoreVisibility", "[ScoreVisibility] IN ('IMMEDIATE','AFTER_EXAM_END','HIDDEN')");
                    table.CheckConstraint("CK_ExamVersions_Status", "[Status] IN ('DRAFT','PUBLISHED','ARCHIVED')");
                    table.ForeignKey(
                        name: "FK_ExamVersions_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamVersions_Exam",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamVersions_PublishedBy",
                        column: x => x.PublishedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentFormat = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    QuestionType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    AnswerDataType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CorrectAnswerNumber = table.Column<decimal>(type: "decimal(30,10)", precision: 10, scale: 2, nullable: true),
                    NumericTolerance = table.Column<decimal>(type: "decimal(30,10)", precision: 10, scale: 2, nullable: true),
                    CaseSensitive = table.Column<bool>(type: "bit", nullable: false),
                    IgnoreAccent = table.Column<bool>(type: "bit", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultScore = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                    table.CheckConstraint("CK_Questions_AnswerDataType", "[AnswerDataType] IN ('TEXT','NUMBER')");
                    table.CheckConstraint("CK_Questions_ContentFormat", "[ContentFormat] IN ('PLAIN','MARKDOWN')");
                    table.CheckConstraint("CK_Questions_FillIn", "([QuestionType] = 'FILL_IN' AND [AnswerDataType] IS NOT NULL) OR ([QuestionType] <> 'FILL_IN' AND [AnswerDataType] IS NULL)");
                    table.CheckConstraint("CK_Questions_Number", "[AnswerDataType] IS NULL OR [AnswerDataType] <> 'NUMBER' OR ([CorrectAnswerNumber] IS NOT NULL AND [NumericTolerance] IS NOT NULL AND [NumericTolerance] >= 0)");
                    table.CheckConstraint("CK_Questions_Score", "[DefaultScore] > 0");
                    table.CheckConstraint("CK_Questions_Type", "[QuestionType] IN ('SINGLE_CHOICE','MULTIPLE_CHOICE','TRUE_FALSE','FILL_IN')");
                    table.ForeignKey(
                        name: "FK_Questions_Category",
                        column: x => x.CategoryId,
                        principalTable: "QuestionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Questions_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Questions_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamAssignments", x => x.Id);
                    table.CheckConstraint("CK_ExamAssignments_Target", "([GroupId] IS NULL AND [UserId] IS NOT NULL) OR ([GroupId] IS NOT NULL AND [UserId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ExamAssignments_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamAssignments_Exam",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamAssignments_Group",
                        column: x => x.GroupId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamAssignments_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserGroupMembers",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroupMembers", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_UserGroupMembers_Group",
                        column: x => x.GroupId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserGroupMembers_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    SubmitReason = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true, collation: "Latin1_General_100_BIN2"),
                    StartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ExpiredAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    TimeExtensionMinutes = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QuestionCount = table.Column<int>(type: "int", nullable: false),
                    StartedIp = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    StartedUserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubmittedIp = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamAttempts", x => x.Id);
                    table.CheckConstraint("CK_ExamAttempts_Status", "[Status] IN ('IN_PROGRESS','SUBMITTED','AUTO_SUBMITTED','CANCELLED')");
                    table.CheckConstraint("CK_ExamAttempts_SubmitReason", "[SubmitReason] IN ('STUDENT','TIME_EXPIRED','FORCED_BY_ADMIN')");
                    table.CheckConstraint("CK_ExamAttempts_Submitted", "([Status] IN ('SUBMITTED','AUTO_SUBMITTED') AND [SubmittedAt] IS NOT NULL AND [SubmitReason] IS NOT NULL) OR ([Status] IN ('IN_PROGRESS','CANCELLED'))");
                    table.CheckConstraint("CK_ExamAttempts_Time", "[ExpiredAt] > [StartedAt]");
                    table.ForeignKey(
                        name: "FK_ExamAttempts_CancelledBy",
                        column: x => x.CancelledBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamAttempts_Exam",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamAttempts_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamAttempts_Version",
                        column: x => x.ExamVersionId,
                        principalTable: "ExamVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceRowVersion = table.Column<byte[]>(type: "binary(8)", nullable: true),
                    CopiedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    QuestionOrder = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentFormat = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    QuestionType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    AnswerDataType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CorrectAnswerNumber = table.Column<decimal>(type: "decimal(30,10)", precision: 10, scale: 2, nullable: true),
                    NumericTolerance = table.Column<decimal>(type: "decimal(30,10)", precision: 10, scale: 2, nullable: true),
                    CaseSensitive = table.Column<bool>(type: "bit", nullable: false),
                    IgnoreAccent = table.Column<bool>(type: "bit", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Score = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IsVoided = table.Column<bool>(type: "bit", nullable: false),
                    VoidedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamQuestions", x => x.Id);
                    table.CheckConstraint("CK_ExamQuestions_AnswerDataType", "[AnswerDataType] IN ('TEXT','NUMBER')");
                    table.CheckConstraint("CK_ExamQuestions_ContentFormat", "[ContentFormat] IN ('PLAIN','MARKDOWN')");
                    table.CheckConstraint("CK_ExamQuestions_Score", "[Score] > 0");
                    table.CheckConstraint("CK_ExamQuestions_Type", "[QuestionType] IN ('SINGLE_CHOICE','MULTIPLE_CHOICE','TRUE_FALSE','FILL_IN')");
                    table.ForeignKey(
                        name: "FK_ExamQuestions_SourceQuestion",
                        column: x => x.SourceQuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamQuestions_Version",
                        column: x => x.ExamVersionId,
                        principalTable: "ExamVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamQuestions_VoidedBy",
                        column: x => x.VoidedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionAcceptedAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnswerText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionAcceptedAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionAcceptedAnswers_Question",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionOptions_Question",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttemptEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ClientTime = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ServerTime = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    IpAddress = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptEvents", x => x.Id);
                    table.CheckConstraint("CK_AttemptEvents_Type", "[EventType] IN ('VISIBILITY_HIDDEN','VISIBILITY_VISIBLE','WINDOW_BLUR','WINDOW_FOCUS','FULLSCREEN_EXIT','OFFLINE','ONLINE','MULTI_TAB_DETECTED','PAGE_RELOAD','PASTE')");
                    table.ForeignKey(
                        name: "FK_AttemptEvents_Attempt",
                        column: x => x.AttemptId,
                        principalTable: "ExamAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalQuestion = table.Column<int>(type: "int", nullable: false),
                    AnsweredCount = table.Column<int>(type: "int", nullable: false),
                    CorrectCount = table.Column<int>(type: "int", nullable: false),
                    TotalScore = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 10, scale: 2, nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    GradingRevision = table.Column<int>(type: "int", nullable: false),
                    GradedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RegradedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamResults", x => x.Id);
                    table.CheckConstraint("CK_ExamResults_MaxScore", "[MaxScore] > 0");
                    table.ForeignKey(
                        name: "FK_ExamResults_Attempt",
                        column: x => x.AttemptId,
                        principalTable: "ExamAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamResults_Exam",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamResults_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamResults_Version",
                        column: x => x.ExamVersionId,
                        principalTable: "ExamVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnswerKeyCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectionType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    OldKeyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewKeyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AffectedAttemptCount = table.Column<int>(type: "int", nullable: false),
                    CorrectedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnswerKeyCorrections", x => x.Id);
                    table.CheckConstraint("CK_AnswerKeyCorrections_Type", "[CorrectionType] IN ('ANSWER_KEY','VOID')");
                    table.ForeignKey(
                        name: "FK_AnswerKeyCorrections_By",
                        column: x => x.CorrectedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnswerKeyCorrections_Question",
                        column: x => x.ExamQuestionId,
                        principalTable: "ExamQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttemptQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionOrder = table.Column<int>(type: "int", nullable: false),
                    OptionOrder = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttemptQuestions_Attempt",
                        column: x => x.AttemptId,
                        principalTable: "ExamAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttemptQuestions_ExamQuestion",
                        column: x => x.ExamQuestionId,
                        principalTable: "ExamQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamQuestionAcceptedAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnswerText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamQuestionAcceptedAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamQuestionAcceptedAnswers_Question",
                        column: x => x.ExamQuestionId,
                        principalTable: "ExamQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamQuestionOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamQuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamQuestionOptions_Question",
                        column: x => x.ExamQuestionId,
                        principalTable: "ExamQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamResultHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradingRevision = table.Column<int>(type: "int", nullable: false),
                    CorrectCount = table.Column<int>(type: "int", nullable: false),
                    TotalScore = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 10, scale: 2, nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: true),
                    AnswerKeyCorrectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamResultHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamResultHistory_Correction",
                        column: x => x.AnswerKeyCorrectionId,
                        principalTable: "AnswerKeyCorrections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamResultHistory_Result",
                        column: x => x.ExamResultId,
                        principalTable: "ExamResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttemptAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnswerText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AnswerNumber = table.Column<decimal>(type: "decimal(30,10)", precision: 10, scale: 2, nullable: true),
                    IsAnswered = table.Column<bool>(type: "bit", nullable: false),
                    IsMarkedForReview = table.Column<bool>(type: "bit", nullable: false),
                    ClientSeq = table.Column<long>(type: "bigint", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    SaveCount = table.Column<int>(type: "int", nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: true),
                    Score = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    GradedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttemptAnswers_Question",
                        column: x => x.AttemptQuestionId,
                        principalTable: "AttemptQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttemptAnswerOptions",
                columns: table => new
                {
                    AttemptAnswerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptAnswerOptions", x => new { x.AttemptAnswerId, x.OptionCode });
                    table.ForeignKey(
                        name: "FK_AttemptAnswerOptions_Answer",
                        column: x => x.AttemptAnswerId,
                        principalTable: "AttemptAnswers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerKeyCorrections_CorrectedBy",
                table: "AnswerKeyCorrections",
                column: "CorrectedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AnswerKeyCorrections_Question",
                table: "AnswerKeyCorrections",
                column: "ExamQuestionId");

            migrationBuilder.CreateIndex(
                name: "UQ_AttemptAnswers_Question",
                table: "AttemptAnswers",
                column: "AttemptQuestionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttemptEvents_Attempt",
                table: "AttemptEvents",
                columns: new[] { "AttemptId", "ServerTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AttemptQuestions_ExamQuestionId",
                table: "AttemptQuestions",
                column: "ExamQuestionId");

            migrationBuilder.CreateIndex(
                name: "UQ_AttemptQuestions_Order",
                table: "AttemptQuestions",
                columns: new[] { "AttemptId", "QuestionOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_AttemptQuestions_Question",
                table: "AttemptQuestions",
                columns: new[] { "AttemptId", "ExamQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Entity",
                table: "AuditLogs",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_User",
                table: "AuditLogs",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAssignments_CreatedBy",
                table: "ExamAssignments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ExamAssignments_GroupId",
                table: "ExamAssignments",
                column: "GroupId",
                filter: "[GroupId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExamAssignments_UserId",
                table: "ExamAssignments",
                column: "UserId",
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ExamAssignments_Group",
                table: "ExamAssignments",
                columns: new[] { "ExamId", "GroupId" },
                unique: true,
                filter: "[GroupId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ExamAssignments_User",
                table: "ExamAssignments",
                columns: new[] { "ExamId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_CancelledBy",
                table: "ExamAttempts",
                column: "CancelledBy");

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_Exam",
                table: "ExamAttempts",
                columns: new[] { "ExamId", "Status" })
                .Annotation("SqlServer:Include", new[] { "UserId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_InProgress_Expired",
                table: "ExamAttempts",
                column: "ExpiredAt",
                filter: "[Status] = 'IN_PROGRESS'");

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_Version",
                table: "ExamAttempts",
                columns: new[] { "ExamVersionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UQ_ExamAttempts_User_Exam_Number",
                table: "ExamAttempts",
                columns: new[] { "UserId", "ExamId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttempts_OneInProgress",
                table: "ExamAttempts",
                columns: new[] { "UserId", "ExamId" },
                unique: true,
                filter: "[Status] = 'IN_PROGRESS'");

            migrationBuilder.CreateIndex(
                name: "UQ_ExamQuestionAcceptedAnswers_Order",
                table: "ExamQuestionAcceptedAnswers",
                columns: new[] { "ExamQuestionId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ExamQuestionOptions_Code",
                table: "ExamQuestionOptions",
                columns: new[] { "ExamQuestionId", "OptionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamQuestions_SourceQuestionId",
                table: "ExamQuestions",
                column: "SourceQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamQuestions_VoidedBy",
                table: "ExamQuestions",
                column: "VoidedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_ExamQuestions_Order",
                table: "ExamQuestions",
                columns: new[] { "ExamVersionId", "QuestionOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamQuestions_Source",
                table: "ExamQuestions",
                columns: new[] { "ExamVersionId", "SourceQuestionId" },
                unique: true,
                filter: "[SourceQuestionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExamResultHistory_AnswerKeyCorrectionId",
                table: "ExamResultHistory",
                column: "AnswerKeyCorrectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamResultHistory_Result",
                table: "ExamResultHistory",
                column: "ExamResultId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamResults_Exam",
                table: "ExamResults",
                column: "ExamId")
                .Annotation("SqlServer:Include", new[] { "UserId", "TotalScore", "Percentage", "Passed", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamResults_ExamVersionId",
                table: "ExamResults",
                column: "ExamVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamResults_User",
                table: "ExamResults",
                columns: new[] { "UserId", "SubmittedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UQ_ExamResults_Attempt",
                table: "ExamResults",
                column: "AttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_CreatedBy",
                table: "Exams",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Status",
                table: "Exams",
                column: "Status")
                .Annotation("SqlServer:Include", new[] { "StartAt", "EndAt", "AccessMode" });

            migrationBuilder.CreateIndex(
                name: "IX_Exams_UpdatedBy",
                table: "Exams",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_Exams_Code",
                table: "Exams",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamUserOverrides_UpdatedBy",
                table: "ExamUserOverrides",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ExamUserOverrides_UserId",
                table: "ExamUserOverrides",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamVersions_CreatedBy",
                table: "ExamVersions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ExamVersions_PublishedBy",
                table: "ExamVersions",
                column: "PublishedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_ExamVersions_Number",
                table: "ExamVersions",
                columns: new[] { "ExamId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamVersions_OneDraft",
                table: "ExamVersions",
                column: "ExamId",
                unique: true,
                filter: "[Status] = 'DRAFT'");

            migrationBuilder.CreateIndex(
                name: "UX_ExamVersions_OnePublished",
                table: "ExamVersions",
                column: "ExamId",
                unique: true,
                filter: "[Status] = 'PUBLISHED'");

            migrationBuilder.CreateIndex(
                name: "UQ_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_QuestionAcceptedAnswers_Order",
                table: "QuestionAcceptedAnswers",
                columns: new[] { "QuestionId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionCategories_CreatedBy",
                table: "QuestionCategories",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_QuestionCategories_Code",
                table: "QuestionCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_QuestionOptions_Code",
                table: "QuestionOptions",
                columns: new[] { "QuestionId", "OptionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Category_Type",
                table: "Questions",
                columns: new[] { "CategoryId", "QuestionType" })
                .Annotation("SqlServer:Include", new[] { "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_CreatedBy",
                table: "Questions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_UpdatedBy",
                table: "Questions",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_Questions_Code",
                table: "Questions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Family",
                table: "RefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_User",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UQ_Roles_Code",
                table: "Roles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMembers_User",
                table: "UserGroupMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroups_CreatedBy",
                table: "UserGroups",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_UserGroups_Code",
                table: "UserGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UQ_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Users_NormalizedUserName",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttemptAnswerOptions");

            migrationBuilder.DropTable(
                name: "AttemptEvents");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ExamAssignments");

            migrationBuilder.DropTable(
                name: "ExamQuestionAcceptedAnswers");

            migrationBuilder.DropTable(
                name: "ExamQuestionOptions");

            migrationBuilder.DropTable(
                name: "ExamResultHistory");

            migrationBuilder.DropTable(
                name: "ExamUserOverrides");

            migrationBuilder.DropTable(
                name: "QuestionAcceptedAnswers");

            migrationBuilder.DropTable(
                name: "QuestionOptions");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserGroupMembers");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "AttemptAnswers");

            migrationBuilder.DropTable(
                name: "AnswerKeyCorrections");

            migrationBuilder.DropTable(
                name: "ExamResults");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "UserGroups");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "AttemptQuestions");

            migrationBuilder.DropTable(
                name: "ExamAttempts");

            migrationBuilder.DropTable(
                name: "ExamQuestions");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "ExamVersions");

            migrationBuilder.DropTable(
                name: "QuestionCategories");

            migrationBuilder.DropTable(
                name: "Exams");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
