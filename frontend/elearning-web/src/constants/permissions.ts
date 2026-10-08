/** Khớp ELearning.Domain.Identity.Permissions (docs/07-bao-mat.md mục 4.1). Chỉ dùng để ẩn/hiện UI. */
export const Permissions = {
  UserView: "User.View",
  UserCreate: "User.Create",
  UserUpdate: "User.Update",
  UserResetPassword: "User.ResetPassword",
  UserAnonymize: "User.Anonymize",
  GroupView: "Group.View",
  GroupManage: "Group.Manage",
  ClassView: "Class.View",
  ClassManage: "Class.Manage",
  RoleView: "Role.View",
  RoleManage: "Role.Manage",
  RoleAssign: "Role.Assign",
  CategoryView: "Category.View",
  CategoryManage: "Category.Manage",
  QuestionView: "Question.View",
  QuestionCreate: "Question.Create",
  QuestionUpdate: "Question.Update",
  ExamView: "Exam.View",
  ExamCreate: "Exam.Create",
  ExamUpdate: "Exam.Update",
  ExamDelete: "Exam.Delete",
  ExamPublish: "Exam.Publish",
  ExamClose: "Exam.Close",
  ExamAssign: "Exam.Assign",
  ExamRegrade: "Exam.Regrade",
  AttemptView: "Attempt.View",
  AttemptManage: "Attempt.Manage",
  AttemptGrade: "Attempt.Grade",
  ResultView: "Result.View",
  ResultExport: "Result.Export",
  ReportView: "Report.View",
  AuditView: "Audit.View",
} as const;

export type PermissionCode = (typeof Permissions)[keyof typeof Permissions];

export const StudentRole = "STUDENT";
export const AdminRole = "ADMIN";
