using ELearning.Domain.Attempts;
using ELearning.Domain.Audit;
using ELearning.Domain.Exams;
using ELearning.Domain.Identity;
using ELearning.Domain.Media;
using ELearning.Domain.Questions;
using ELearning.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ELearning.Application.Common.Abstractions;

/// <summary>
/// DbContext nhìn từ tầng Application (quyết định D-23: application service làm việc trực tiếp với DbSet;
/// repository riêng chỉ dành cho thao tác đặc biệt như khóa dòng).
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<Permission> Permissions { get; }

    DbSet<UserRole> UserRoles { get; }

    DbSet<RolePermission> RolePermissions { get; }

    DbSet<UserGroup> UserGroups { get; }

    DbSet<UserGroupMember> UserGroupMembers { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<QuestionCategory> QuestionCategories { get; }

    DbSet<Question> Questions { get; }

    DbSet<Exam> Exams { get; }

    DbSet<ExamVersion> ExamVersions { get; }

    DbSet<ExamQuestion> ExamQuestions { get; }

    DbSet<ExamAssignment> ExamAssignments { get; }

    DbSet<ExamUserOverride> ExamUserOverrides { get; }

    DbSet<AnswerKeyCorrection> AnswerKeyCorrections { get; }

    DbSet<ExamAttempt> ExamAttempts { get; }

    DbSet<AttemptQuestion> AttemptQuestions { get; }

    DbSet<AttemptAnswer> AttemptAnswers { get; }

    DbSet<AttemptEvent> AttemptEvents { get; }

    DbSet<ExamResult> ExamResults { get; }

    DbSet<ExamResultHistory> ExamResultHistory { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<MediaFile> MediaFiles { get; }

    DatabaseFacade Database { get; }

    ChangeTracker ChangeTracker { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
