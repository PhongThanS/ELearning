using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Attempts;
using ELearning.Domain.Audit;
using ELearning.Domain.Classes;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Domain.Identity;
using ELearning.Domain.Media;
using ELearning.Domain.Questions;
using ELearning.Domain.Results;
using ELearning.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Infrastructure.Persistence;

public sealed class ELearningDbContext(DbContextOptions<ELearningDbContext> options) : DbContext(options), IAppDbContext
{
    public const string EnumCollation = "Latin1_General_100_BIN2";
    public const string QuestionCodeSequence = "QuestionCodeSequence";

    /// <summary>Mọi enum domain được lưu dạng UPPER_SNAKE_CASE (VARCHAR) kèm CHECK constraint.</summary>
    internal static readonly Type[] DomainEnums =
    [
        typeof(QuestionType), typeof(AnswerDataType), typeof(ContentFormat), typeof(ExamStatus),
        typeof(ExamVersionStatus), typeof(AccessMode), typeof(RetakeScoringPolicy), typeof(ScoreVisibility),
        typeof(ReviewPolicy), typeof(AttemptStatus), typeof(SubmitReason), typeof(AttemptEventType),
        typeof(RefreshTokenRevokedReason), typeof(AnswerKeyCorrectionType), typeof(QuestionDifficulty),
    ];

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserGroup> UserGroups => Set<UserGroup>();

    public DbSet<UserGroupMember> UserGroupMembers => Set<UserGroupMember>();

    public DbSet<Classroom> Classrooms => Set<Classroom>();

    public DbSet<ClassroomStudent> ClassroomStudents => Set<ClassroomStudent>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<QuestionCategory> QuestionCategories => Set<QuestionCategory>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<Exam> Exams => Set<Exam>();

    public DbSet<ExamVersion> ExamVersions => Set<ExamVersion>();

    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();

    public DbSet<ExamAssignment> ExamAssignments => Set<ExamAssignment>();

    public DbSet<ExamUserOverride> ExamUserOverrides => Set<ExamUserOverride>();

    public DbSet<AnswerKeyCorrection> AnswerKeyCorrections => Set<AnswerKeyCorrection>();

    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();

    public DbSet<AttemptQuestion> AttemptQuestions => Set<AttemptQuestion>();

    public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();

    public DbSet<AttemptEvent> AttemptEvents => Set<AttemptEvent>();

    public DbSet<ExamResult> ExamResults => Set<ExamResult>();

    public DbSet<ExamResultHistory> ExamResultHistory => Set<ExamResultHistory>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // UTC cho mọi DateTime (docs/10-bay-ky-thuat.md mục 1)
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>()
            .HaveColumnType("datetime2(3)");
        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<NullableUtcDateTimeConverter>()
            .HaveColumnType("datetime2(3)");

        // Điểm mặc định DECIMAL(10,2); số đáp án / phần trăm được ghi đè trong từng configuration
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);

        // Enum → UPPER_SNAKE_CASE (docs/10-bay-ky-thuat.md mục 2).
        // Collation nhị phân: CHECK constraint và filtered index so khớp đúng chữ hoa,
        // nếu không 'Published' vẫn lọt qua CHECK trên collation mặc định CI.
        foreach (var enumType in DomainEnums)
        {
            var converter = typeof(SnakeCaseEnumConverter<>).MakeGenericType(enumType);
            configurationBuilder.Properties(enumType)
                .HaveConversion(converter).HaveMaxLength(40).AreUnicode(false).UseCollation(EnumCollation);
            configurationBuilder.Properties(typeof(Nullable<>).MakeGenericType(enumType))
                .HaveConversion(converter).HaveMaxLength(40).AreUnicode(false).UseCollation(EnumCollation);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ELearningDbContext).Assembly);
        modelBuilder.HasSequence<long>(QuestionCodeSequence).StartsAt(1).IncrementsBy(1);

        // Không cascade ở DB: hệ thống xóa mềm và tránh "multiple cascade paths" (D-16, docs/10-bay-ky-thuat.md mục 9).
        // Ngoại lệ: ClientCascade (DB vẫn NO ACTION) cho bảng con thuần túy như option của câu hỏi,
        // để EF xóa được dòng con khi thay danh sách option.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            if (foreignKey.DeleteBehavior != DeleteBehavior.ClientCascade)
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}
