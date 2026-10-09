import { apiClient, http, toApiError } from "./apiClient";
import type {
  ManualGradeResult,
  MediaUpload,
  ManualGradingItem,
  PoolRuleInput,
  QuestionImportResult,
  AnswerKeyCorrection,
  UserOverride,
  AdminAttemptDetail,
  AdminAttemptRow,
  AdminResultRow,
  Assignments,
  Attempt,
  AttemptEventType,
  AuditLog,
  AuthResponse,
  AuthUser,
  Category,
  Classroom,
  ClassroomStudent,
  MyClassroom,
  Dashboard,
  ExamDetail,
  ExamListItem,
  ExamPreview,
  Group,
  GroupMember,
  PageQuery,
  Paged,
  Permission,
  PublishValidation,
  QuestionDetail,
  QuestionInput,
  QuestionListItem,
  QuestionStat,
  RegradeSummary,
  ReviewPolicy,
  Role,
  SaveAnswerItem,
  SaveAnswersResponse,
  StudentExamDetail,
  StudentExamItem,
  StudentHistoryItem,
  StudentResult,
  UserDetail,
  UserListItem,
  UserWithPassword,
  VersionDetail,
} from "../types/api";

type Query = PageQuery & Record<string, string | number | boolean | undefined | null>;

export const authApi = {
  login: (userName: string, password: string) => http.post<AuthResponse>("/auth/login", { userName, password }),
  register: (body: { userName: string; email: string; fullName: string; password: string; acceptTerms: boolean }) =>
    http.post<AuthUser>("/auth/register", body),
  logout: () => http.post<void>("/auth/logout"),
  me: () => http.get<AuthUser>("/auth/me"),
  changePassword: (currentPassword: string, newPassword: string) =>
    http.post<AuthResponse>("/auth/change-password", { currentPassword, newPassword }),
  forgotPassword: (email: string) => apiClient.post("/auth/forgot-password", { email }).then((r) => r.data.message as string),
};

export const usersApi = {
  list: (q: Query) => http.get<Paged<UserListItem>>("/users", q),
  get: (id: string) => http.get<UserDetail>(`/users/${id}`),
  create: (body: { userName: string; email: string; fullName: string; password?: string | null; roleIds?: string[]; groupIds?: string[] }) =>
    http.post<UserWithPassword>("/users", body),
  update: (id: string, body: { email: string; fullName: string; rowVersion: string }) => http.put<UserDetail>(`/users/${id}`, body),
  setStatus: (id: string, isActive: boolean, reason?: string) => http.patch<UserDetail>(`/users/${id}/status`, { isActive, reason }),
  setRoles: (id: string, roleIds: string[]) => http.put<UserDetail>(`/users/${id}/roles`, { roleIds }),
  resetPassword: (id: string) => http.post<UserWithPassword>(`/users/${id}/reset-password`),
  anonymize: (id: string, reason: string) => http.post<UserDetail>(`/users/${id}/anonymize`, { reason }),
};

export const groupsApi = {
  list: (q: Query) => http.get<Paged<Group>>("/groups", q),
  get: (id: string) => http.get<Group>(`/groups/${id}`),
  create: (body: { code: string; name: string; description?: string | null }) => http.post<Group>("/groups", body),
  update: (id: string, body: { name: string; description: string | null; isActive: boolean; rowVersion: string }) =>
    http.put<Group>(`/groups/${id}`, body),
  remove: (id: string) => http.delete<void>(`/groups/${id}`),
  members: (id: string, q: Query) => http.get<Paged<GroupMember>>(`/groups/${id}/members`, q),
  addMembers: (id: string, userIds: string[]) => http.post<Group>(`/groups/${id}/members`, { userIds }),
  removeMember: (id: string, userId: string) => http.delete<Group>(`/groups/${id}/members/${userId}`),
};

export interface ClassroomInput {
  name: string;
  schoolYear: string | null;
  startDate: string | null;
  endDate: string | null;
  description: string | null;
}

export const classesApi = {
  list: (q: Query) => http.get<Paged<Classroom>>("/classes", q),
  get: (id: string) => http.get<Classroom>(`/classes/${id}`),
  create: (body: ClassroomInput & { code: string }) => http.post<Classroom>("/classes", body),
  update: (id: string, body: ClassroomInput & { isActive: boolean; rowVersion: string }) => http.put<Classroom>(`/classes/${id}`, body),
  setStatus: (id: string, isActive: boolean) => http.patch<Classroom>(`/classes/${id}/status`, { isActive }),
  remove: (id: string) => http.delete<void>(`/classes/${id}`),
  students: (id: string, q: Query) => http.get<Paged<ClassroomStudent>>(`/classes/${id}/students`, q),
  addStudents: (id: string, userIds: string[]) => http.post<Classroom>(`/classes/${id}/students`, { userIds }),
  removeStudent: (id: string, userId: string) => http.delete<Classroom>(`/classes/${id}/students/${userId}`),
};

export const rolesApi = {
  list: () => http.get<Role[]>("/roles"),
  permissions: () => http.get<Permission[]>("/permissions"),
  create: (body: { code: string; name: string; permissionIds: string[] }) => http.post<Role>("/roles", body),
  update: (id: string, body: { name: string; isActive: boolean }) => http.put<Role>(`/roles/${id}`, body),
  setPermissions: (id: string, permissionIds: string[]) => http.put<Role>(`/roles/${id}/permissions`, { permissionIds }),
};

export const categoriesApi = {
  list: (q: Query) => http.get<Paged<Category>>("/question-categories", q),
  create: (body: { code: string; name: string }) => http.post<Category>("/question-categories", body),
  update: (id: string, body: { name: string; isActive: boolean; rowVersion: string }) =>
    http.put<Category>(`/question-categories/${id}`, body),
  setStatus: (id: string, isActive: boolean) => http.patch<Category>(`/question-categories/${id}/status`, { isActive }),
  remove: (id: string, force = false) => http.delete<void>(`/question-categories/${id}${force ? "?force=true" : ""}`),
};

export const questionsApi = {
  list: (q: Query) => http.get<Paged<QuestionListItem>>("/questions", q),
  get: (id: string) => http.get<QuestionDetail>(`/questions/${id}`),
  create: (body: QuestionInput) => http.post<QuestionDetail>("/questions", body),
  update: (id: string, body: QuestionInput) => http.put<QuestionDetail>(`/questions/${id}`, body),
  setStatus: (id: string, isActive: boolean) => http.patch<QuestionDetail>(`/questions/${id}/status`, { isActive }),
  clone: (id: string) => http.post<QuestionDetail>(`/questions/${id}/clone`),
  remove: (id: string, force = false) => http.delete<void>(`/questions/${id}${force ? "?force=true" : ""}`),
  importTemplate: () => downloadFile("/questions/import/template", undefined, "mau-import-cau-hoi.xlsx"),
  /** dryRun: chỉ kiểm tra. Tất cả hoặc không: có dòng lỗi thì không tạo câu nào. */
  import: (file: File, dryRun: boolean) => {
    const form = new FormData();
    form.append("file", file);
    return http.post<QuestionImportResult>(`/questions/import?dryRun=${dryRun}`, form);
  },
};

/** Ảnh trong câu hỏi (D-27): PNG / JPEG / GIF / WebP, tối đa 2 MB; server kiểm tra lại. */
export const mediaApi = {
  upload: (file: File) => {
    const form = new FormData();
    form.append("file", file);
    return http.post<MediaUpload>("/media", form);
  },
};

export interface ExamDetailsBody {
  code?: string | null;
  name: string;
  description: string | null;
  instructions: string | null;
  startAt: string | null;
  endAt: string | null;
  maxAttempts: number;
  accessMode: string;
  retakeScoringPolicy: string;
}

export const examsApi = {
  list: (q: Query) => http.get<Paged<ExamListItem>>("/exams", q),
  get: (id: string) => http.get<ExamDetail>(`/exams/${id}`),
  create: (body: ExamDetailsBody & { durationMinutes: number; passPercentage: number | null; scoreVisibility: string; reviewPolicy: string }) =>
    http.post<ExamDetail>("/exams", body),
  update: (id: string, body: ExamDetailsBody & { rowVersion: string }) => http.put<ExamDetail>(`/exams/${id}`, body),
  remove: (id: string) => http.delete<void>(`/exams/${id}`),
  clone: (id: string, body: { code?: string; name?: string }) => http.post<ExamDetail>(`/exams/${id}/clone`, body),
  close: (id: string, forceSubmitInProgress: boolean) => http.post<ExamDetail>(`/exams/${id}/close`, { forceSubmitInProgress }),
  reopen: (id: string) => http.post<ExamDetail>(`/exams/${id}/reopen`),
  setReviewPolicy: (id: string, reviewPolicy: ReviewPolicy) => http.patch<ExamDetail>(`/exams/${id}/review-policy`, { reviewPolicy }),
  assignments: (id: string) => http.get<Assignments>(`/exams/${id}/assignments`),
  setAssignments: (id: string, next: { groupIds: string[]; userIds: string[]; classroomIds: string[] }) =>
    http.put<Assignments>(`/exams/${id}/assignments`, next),
  userOverrides: (id: string, q: Query) => http.get<Paged<UserOverride>>(`/exams/${id}/user-overrides`, q),
  setUserOverride: (id: string, userId: string, extraAttempts: number, note?: string) =>
    http.put(`/exams/${id}/user-overrides/${userId}`, { extraAttempts, note }),
  answerKeyCorrections: (id: string) => http.get<AnswerKeyCorrection[]>(`/exams/${id}/answer-key-corrections`),

  createVersion: (id: string, copyFromVersionId?: string) => http.post<VersionDetail>(`/exams/${id}/versions`, { copyFromVersionId }),
  version: (id: string, versionId: string) => http.get<VersionDetail>(`/exams/${id}/versions/${versionId}`),
  updateVersion: (id: string, versionId: string, body: { durationMinutes: number; passPercentage: number | null; scoreVisibility: string; reviewPolicy: string; shuffleQuestions: boolean; shuffleOptions: boolean; rowVersion: string }) =>
    http.put<VersionDetail>(`/exams/${id}/versions/${versionId}`, body),
  deleteVersion: (id: string, versionId: string) => http.delete<void>(`/exams/${id}/versions/${versionId}`),
  addPoolRule: (id: string, versionId: string, body: PoolRuleInput) =>
    http.post<VersionDetail>(`/exams/${id}/versions/${versionId}/pool-rules`, body),
  refreshPoolRule: (id: string, versionId: string, ruleId: string) =>
    http.post<VersionDetail>(`/exams/${id}/versions/${versionId}/pool-rules/${ruleId}/refresh`),
  removePoolRule: (id: string, versionId: string, ruleId: string) =>
    http.delete<VersionDetail>(`/exams/${id}/versions/${versionId}/pool-rules/${ruleId}`),
  preview: (id: string, versionId: string) => http.get<ExamPreview>(`/exams/${id}/versions/${versionId}/preview`),
  validate: (id: string, versionId: string) => http.post<PublishValidation>(`/exams/${id}/versions/${versionId}/validate`),
  publish: (id: string, versionId: string) => http.post<VersionDetail>(`/exams/${id}/versions/${versionId}/publish`),
  addQuestions: (id: string, versionId: string, questionIds: string[], score?: number | null) =>
    http.post<VersionDetail>(`/exams/${id}/versions/${versionId}/questions`, { questionIds, score }),
  setQuestionScore: (id: string, versionId: string, examQuestionId: string, score: number) =>
    http.patch<VersionDetail>(`/exams/${id}/versions/${versionId}/questions/${examQuestionId}`, { score }),
  removeQuestion: (id: string, versionId: string, examQuestionId: string) =>
    http.delete<VersionDetail>(`/exams/${id}/versions/${versionId}/questions/${examQuestionId}`),
  reorder: (id: string, versionId: string, examQuestionIds: string[]) =>
    http.put<VersionDetail>(`/exams/${id}/versions/${versionId}/questions/order`, { examQuestionIds }),
  sync: (id: string, versionId: string, examQuestionIds?: string[]) =>
    http.post<VersionDetail>(`/exams/${id}/versions/${versionId}/questions/sync`, { examQuestionIds }),
  correctAnswerKey: (id: string, versionId: string, examQuestionId: string, body: { correctOptionCodes?: string[]; acceptedAnswers?: string[]; correctAnswerNumber?: number; numericTolerance?: number; reason: string }) =>
    http.post<RegradeSummary>(`/exams/${id}/versions/${versionId}/questions/${examQuestionId}/answer-key`, body),
  voidQuestion: (id: string, versionId: string, examQuestionId: string, reason: string) =>
    http.post<RegradeSummary>(`/exams/${id}/versions/${versionId}/questions/${examQuestionId}/void`, { reason }),
};

export const studentApi = {
  exams: (q: Query) => http.get<Paged<StudentExamItem>>("/student/exams", q),
  classes: () => http.get<MyClassroom[]>("/student/classes"),
  exam: (id: string) => http.get<StudentExamDetail>(`/student/exams/${id}`),
  start: (id: string) => http.post<Attempt>(`/student/exams/${id}/start`),
  attempt: (id: string) => http.get<Attempt>(`/student/attempts/${id}`),
  saveAnswers: (id: string, answers: SaveAnswerItem[]) => http.put<SaveAnswersResponse>(`/student/attempts/${id}/answers`, { answers }),
  events: (id: string, events: { type: AttemptEventType; clientTime: string; detail?: string }[]) =>
    http.post<{ accepted: number }>(`/student/attempts/${id}/events`, { events }),
  submit: (id: string) => http.post<StudentResult>(`/student/attempts/${id}/submit`),
  result: (id: string) => http.get<StudentResult>(`/student/attempts/${id}/result`),
  history: (q: Query) => http.get<Paged<StudentHistoryItem>>("/student/history", q),
};

export const adminApi = {
  dashboard: () => http.get<Dashboard>("/admin/dashboard"),
  attempts: (examId: string, q: Query) => http.get<Paged<AdminAttemptRow>>(`/admin/exams/${examId}/attempts`, q),
  attempt: (id: string) => http.get<AdminAttemptDetail>(`/admin/attempts/${id}`),
  extend: (id: string, minutes: number, reason: string) => http.post<AdminAttemptDetail>(`/admin/attempts/${id}/extend`, { minutes, reason }),
  forceSubmit: (id: string, reason: string) => http.post<AdminAttemptDetail>(`/admin/attempts/${id}/force-submit`, { reason }),
  cancel: (id: string, reason: string) => http.post<AdminAttemptDetail>(`/admin/attempts/${id}/cancel`, { reason }),
  results: (examId: string, q: Query) => http.get<Paged<AdminResultRow>>(`/admin/exams/${examId}/results`, q),
  questionStats: (examId: string, versionId?: string) =>
    http.get<QuestionStat[]>("/admin/reports/question-statistics", { examId, versionId }),
  auditLogs: (q: Query) => http.get<Paged<AuditLog>>("/admin/audit-logs", q),
  manualGrading: (examId: string, q: Query) => http.get<Paged<ManualGradingItem>>(`/admin/exams/${examId}/manual-grading`, q),
  manualGrade: (attemptId: string, attemptQuestionId: string, score: number, comment: string | null) =>
    http.post<ManualGradeResult>(`/admin/attempts/${attemptId}/questions/${attemptQuestionId}/manual-grade`, { score, comment }),

  exportResults: (examId: string, official: boolean) =>
    downloadFile(`/admin/exams/${examId}/results/export`, { official }, "ket-qua.xlsx"),
};

/** Tải file qua Axios (có Authorization) rồi lưu bằng blob; tên file lấy từ Content-Disposition. */
async function downloadFile(url: string, params: object | undefined, fallbackName: string): Promise<void> {
  try {
    const response = await apiClient.get<Blob>(url, { params, responseType: "blob" });
    const disposition = String(response.headers["content-disposition"] ?? "");
    const match = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(disposition);
    const fileName = decodeURIComponent(match?.[1] ?? match?.[2] ?? fallbackName);
    const objectUrl = URL.createObjectURL(response.data);
    const link = document.createElement("a");
    link.href = objectUrl;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(objectUrl);
  } catch (error) {
    throw toApiError(error);
  }
}

