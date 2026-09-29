namespace ELearning.Domain.Identity;

/// <summary>Danh sách permission chuẩn (docs/07-bao-mat.md mục 4.1). Mỗi mã là một policy.</summary>
public static class Permissions
{
    public const string UserView = "User.View";
    public const string UserCreate = "User.Create";
    public const string UserUpdate = "User.Update";
    public const string UserResetPassword = "User.ResetPassword";
    public const string UserAnonymize = "User.Anonymize";

    public const string GroupView = "Group.View";
    public const string GroupManage = "Group.Manage";

    public const string RoleView = "Role.View";
    public const string RoleManage = "Role.Manage";
    public const string RoleAssign = "Role.Assign";

    public const string CategoryView = "Category.View";
    public const string CategoryManage = "Category.Manage";

    public const string QuestionView = "Question.View";
    public const string QuestionCreate = "Question.Create";
    public const string QuestionUpdate = "Question.Update";

    public const string ExamView = "Exam.View";
    public const string ExamCreate = "Exam.Create";
    public const string ExamUpdate = "Exam.Update";
    public const string ExamDelete = "Exam.Delete";
    public const string ExamPublish = "Exam.Publish";
    public const string ExamClose = "Exam.Close";
    public const string ExamAssign = "Exam.Assign";
    public const string ExamRegrade = "Exam.Regrade";

    public const string AttemptView = "Attempt.View";
    public const string AttemptManage = "Attempt.Manage";
    public const string AttemptGrade = "Attempt.Grade";

    public const string ResultView = "Result.View";
    public const string ResultExport = "Result.Export";

    public const string ReportView = "Report.View";

    public const string AuditView = "Audit.View";

    /// <summary>Mã → tên hiển thị tiếng Việt, dùng khi seed.</summary>
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [UserView] = "Xem người dùng",
        [UserCreate] = "Tạo người dùng",
        [UserUpdate] = "Sửa người dùng",
        [UserResetPassword] = "Đặt lại mật khẩu người dùng",
        [UserAnonymize] = "Ẩn danh hóa người dùng",
        [GroupView] = "Xem nhóm",
        [GroupManage] = "Quản lý nhóm",
        [RoleView] = "Xem vai trò và quyền",
        [RoleManage] = "Quản lý vai trò",
        [RoleAssign] = "Gán vai trò cho người dùng",
        [CategoryView] = "Xem danh mục",
        [CategoryManage] = "Quản lý danh mục",
        [QuestionView] = "Xem câu hỏi",
        [QuestionCreate] = "Tạo câu hỏi",
        [QuestionUpdate] = "Sửa câu hỏi",
        [ExamView] = "Xem đề thi",
        [ExamCreate] = "Tạo đề thi",
        [ExamUpdate] = "Sửa đề thi",
        [ExamDelete] = "Xóa đề thi nháp",
        [ExamPublish] = "Publish đề thi",
        [ExamClose] = "Đóng / mở lại đề thi",
        [ExamAssign] = "Gán đề thi",
        [ExamRegrade] = "Sửa đáp án và chấm lại",
        [AttemptView] = "Xem lượt thi",
        [AttemptManage] = "Thao tác trên lượt thi",
        [AttemptGrade] = "Chấm tay câu tự luận",
        [ResultView] = "Xem kết quả",
        [ResultExport] = "Export kết quả",
        [ReportView] = "Xem báo cáo",
        [AuditView] = "Xem audit log",
    };
}
