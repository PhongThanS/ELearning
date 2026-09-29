// Kiểu dữ liệu khớp hợp đồng API (docs/05-api.md). Enum dạng UPPER_SNAKE_CASE; thời gian là chuỗi ISO UTC có "Z".

export interface ApiErrorItem {
  field: string | null;
  code: string;
  message: string;
}

export interface ApiEnvelope<T> {
  success: boolean;
  data: T | null;
  message: string | null;
  errors: ApiErrorItem[];
  traceId: string | null;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PageQuery {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDir?: "asc" | "desc";
}

// ----- Auth / identity -----

export interface AuthUser {
  id: string;
  userName: string;
  email: string;
  fullName: string;
  roles: string[];
  permissions: string[];
  mustChangePassword: boolean;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}

export interface RefItem {
  id: string;
  code: string;
  name: string;
}

export interface UserListItem {
  id: string;
  userName: string;
  email: string;
  fullName: string;
  isActive: boolean;
  isLockedOut: boolean;
  roles: string[];
  lastLoginAt: string | null;
  createdAt: string;
}

export interface UserDetail {
  id: string;
  userName: string;
  email: string;
  fullName: string;
  isActive: boolean;
  mustChangePassword: boolean;
  lockoutEnd: string | null;
  lastLoginAt: string | null;
  anonymizedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
  roles: RefItem[];
  groups: RefItem[];
  rowVersion: string;
}

export interface UserWithPassword {
  user: UserDetail;
  temporaryPassword: string | null;
}

export interface Group {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isActive: boolean;
  memberCount: number;
  createdAt: string;
  rowVersion: string;
}

export interface GroupMember {
  userId: string;
  userName: string;
  fullName: string;
  email: string;
  isActive: boolean;
  addedAt: string;
}

export interface Role {
  id: string;
  code: string;
  name: string;
  isSystem: boolean;
  isActive: boolean;
  permissions: string[];
}

export interface Permission {
  id: string;
  code: string;
  name: string;
}

// ----- Ngân hàng câu hỏi -----

export type QuestionType = "SINGLE_CHOICE" | "MULTIPLE_CHOICE" | "TRUE_FALSE" | "FILL_IN";
export type AnswerDataType = "TEXT" | "NUMBER";
export type QuestionDifficulty = "EASY" | "MEDIUM" | "HARD";
export type ContentFormat = "PLAIN" | "MARKDOWN";

export interface Category {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  questionCount: number;
  createdAt: string;
  updatedAt: string | null;
  rowVersion: string;
}

export interface QuestionListItem {
  id: string;
  code: string;
  contentPreview: string;
  questionType: QuestionType;
  answerDataType: AnswerDataType | null;
  categoryId: string | null;
  categoryName: string | null;
  defaultScore: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
  difficulty: QuestionDifficulty | null;
  tags: string[];
}

export interface QuestionOption {
  id?: string;
  optionCode: string;
  content: string;
  isCorrect: boolean;
  displayOrder?: number;
}

export interface QuestionDetail {
  id: string;
  code: string;
  categoryId: string | null;
  categoryName: string | null;
  content: string;
  contentFormat: ContentFormat;
  questionType: QuestionType;
  answerDataType: AnswerDataType | null;
  correctAnswerNumber: number | null;
  numericTolerance: number | null;
  caseSensitive: boolean;
  ignoreAccent: boolean;
  explanation: string | null;
  defaultScore: number;
  isActive: boolean;
  options: QuestionOption[];
  acceptedAnswers: string[];
  createdAt: string;
  updatedAt: string | null;
  rowVersion: string;
  difficulty: QuestionDifficulty | null;
  tags: string[];
}

export interface QuestionInput {
  categoryId: string | null;
  difficulty: QuestionDifficulty | null;
  tags: string[];
  code?: string | null;
  content: string;
  contentFormat: ContentFormat;
  questionType: QuestionType;
  answerDataType: AnswerDataType | null;
  defaultScore: number;
  explanation: string | null;
  options: { optionCode: string; content: string; isCorrect: boolean }[];
  acceptedAnswers: string[];
  correctAnswerNumber: number | null;
  numericTolerance: number | null;
  caseSensitive: boolean;
  ignoreAccent: boolean;
  rowVersion?: string;
}

// ----- Đề thi -----

export type ExamStatus = "DRAFT" | "PUBLISHED" | "CLOSED";
export type ExamVersionStatus = "DRAFT" | "PUBLISHED" | "ARCHIVED";
export type AccessMode = "PUBLIC" | "ASSIGNED";
export type RetakeScoringPolicy = "HIGHEST" | "LATEST";
export type ScoreVisibility = "IMMEDIATE" | "AFTER_EXAM_END" | "HIDDEN";
export type ReviewPolicy = "NEVER" | "AFTER_SUBMIT" | "AFTER_EXAM_END" | "AFTER_LAST_ATTEMPT";

export interface ExamListItem {
  id: string;
  code: string;
  name: string;
  status: ExamStatus;
  startAt: string | null;
  endAt: string | null;
  maxAttempts: number;
  accessMode: AccessMode;
  publishedVersionNumber: number | null;
  hasDraftVersion: boolean;
  attemptCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface VersionSummary {
  id: string;
  versionNumber: number;
  status: ExamVersionStatus;
  questionCount: number;
  maxScore: number;
  publishedAt: string | null;
  archivedAt: string | null;
  createdAt: string;
}

export interface ExamDetail {
  id: string;
  code: string;
  name: string;
  description: string | null;
  instructions: string | null;
  status: ExamStatus;
  startAt: string | null;
  endAt: string | null;
  maxAttempts: number;
  accessMode: AccessMode;
  retakeScoringPolicy: RetakeScoringPolicy;
  publishedVersionId: string | null;
  draftVersionId: string | null;
  assignmentCount: number;
  hasAttempts: boolean;
  versions: VersionSummary[];
  createdAt: string;
  updatedAt: string | null;
  closedAt: string | null;
  rowVersion: string;
}

export interface VersionOption {
  optionCode: string;
  content: string;
  isCorrect: boolean;
  displayOrder: number;
}

export interface VersionQuestion {
  id: string;
  order: number;
  sourceQuestionId: string | null;
  sourceCode: string | null;
  sourceChanged: boolean;
  content: string;
  contentFormat: ContentFormat;
  questionType: QuestionType;
  answerDataType: AnswerDataType | null;
  score: number;
  isVoided: boolean;
  options: VersionOption[];
  acceptedAnswers: string[];
  correctAnswerNumber: number | null;
  numericTolerance: number | null;
  caseSensitive: boolean;
  ignoreAccent: boolean;
  explanation: string | null;
  /** Khác null: câu ứng viên của quy tắc pool ngẫu nhiên. */
  poolRuleId: string | null;
}

/** Quy tắc pool ngẫu nhiên: mỗi lượt thi bốc drawCount câu trong candidateCount câu ứng viên. */
export interface PoolRule {
  id: string;
  order: number;
  categoryId: string | null;
  categoryName: string | null;
  difficulty: QuestionDifficulty | null;
  tag: string | null;
  questionType: QuestionType | null;
  drawCount: number;
  scorePerQuestion: number;
  candidateCount: number;
}

export interface PoolRuleInput {
  categoryId: string | null;
  difficulty: QuestionDifficulty | null;
  tag: string | null;
  questionType: QuestionType | null;
  drawCount: number;
  scorePerQuestion: number;
}

export interface VersionDetail {
  id: string;
  examId: string;
  versionNumber: number;
  status: ExamVersionStatus;
  durationMinutes: number;
  passPercentage: number | null;
  scoreVisibility: ScoreVisibility;
  reviewPolicy: ReviewPolicy;
  shuffleQuestions: boolean;
  shuffleOptions: boolean;
  questionCount: number;
  maxScore: number;
  publishedAt: string | null;
  archivedAt: string | null;
  questions: VersionQuestion[];
  rowVersion: string;
  poolRules: PoolRule[];
}

export interface PublishIssue {
  code: string;
  message: string;
  field: string | null;
}

export interface PublishValidation {
  isValid: boolean;
  issues: PublishIssue[];
}

export interface PlayerOption {
  code: string;
  content: string;
}

export interface PlayerQuestion {
  id: string;
  order: number;
  content: string;
  contentFormat: ContentFormat;
  type: QuestionType;
  answerDataType: AnswerDataType | null;
  score: number;
  options: PlayerOption[];
}

export interface ExamPreview {
  examId: string;
  versionId: string;
  examName: string;
  instructions: string | null;
  durationMinutes: number;
  questionCount: number;
  maxScore: number;
  questions: PlayerQuestion[];
}

export interface Assignments {
  accessMode: AccessMode;
  groups: { id: string; code: string; name: string; memberCount: number }[];
  users: { id: string; userName: string; fullName: string }[];
}

// ----- Học viên -----

export type ExamAvailability = "NOT_STARTED" | "AVAILABLE" | "IN_PROGRESS" | "NO_ATTEMPTS_LEFT" | "ENDED" | "CLOSED";
export type AttemptStatus = "IN_PROGRESS" | "SUBMITTED" | "AUTO_SUBMITTED" | "CANCELLED";
export type SubmitReason = "STUDENT" | "TIME_EXPIRED" | "FORCED_BY_ADMIN";
export type AttemptEventType =
  | "VISIBILITY_HIDDEN"
  | "VISIBILITY_VISIBLE"
  | "WINDOW_BLUR"
  | "WINDOW_FOCUS"
  | "FULLSCREEN_EXIT"
  | "OFFLINE"
  | "ONLINE"
  | "MULTI_TAB_DETECTED"
  | "PAGE_RELOAD"
  | "PASTE";

export interface StudentExamItem {
  examId: string;
  code: string;
  name: string;
  startAt: string | null;
  endAt: string | null;
  durationMinutes: number;
  questionCount: number;
  maxAttempts: number;
  usedAttempts: number;
  remainingAttempts: number;
  inProgressAttemptId: string | null;
  officialScore: number | null;
  availability: ExamAvailability;
}

export interface StudentAttemptSummary {
  attemptId: string;
  attemptNumber: number;
  status: AttemptStatus;
  startedAt: string;
  expiredAt: string;
  submittedAt: string | null;
  scoreVisible: boolean;
  totalScore: number | null;
  maxScore: number | null;
  percentage: number | null;
  passed: boolean | null;
}

export interface StudentExamDetail extends Omit<StudentExamItem, never> {
  description: string | null;
  instructions: string | null;
  maxScore: number;
  passPercentage: number | null;
  attempts: StudentAttemptSummary[];
}

export interface AnswerState {
  selectedOptions: string[];
  answerText: string | null;
  isMarkedForReview: boolean;
  clientSeq: number;
}

export interface AttemptQuestion extends PlayerQuestion {
  answer: AnswerState;
}

export interface Attempt {
  attemptId: string;
  examId: string;
  examName: string;
  attemptNumber: number;
  status: AttemptStatus;
  resumed: boolean;
  startedAt: string;
  expiredAt: string;
  serverTime: string;
  questions: AttemptQuestion[];
}

export interface SaveAnswerItem {
  questionId: string;
  clientSeq: number;
  selectedOptions: string[] | null;
  answerText: string | null;
  isMarkedForReview: boolean;
}

export interface SaveAnswersResponse {
  serverTime: string;
  expiredAt: string;
  answers: { questionId: string; applied: boolean; clientSeq: number }[];
}

export interface ReviewQuestion {
  id: string;
  order: number;
  content: string;
  contentFormat: ContentFormat;
  type: QuestionType;
  answerDataType: AnswerDataType | null;
  maxScore: number;
  options: PlayerOption[];
  selectedOptions: string[];
  answerText: string | null;
  correctOptions: string[];
  acceptedAnswers: string[];
  correctAnswerNumber: number | null;
  isCorrect: boolean;
  score: number;
  isVoided: boolean;
  explanation: string | null;
}

export interface StudentResult {
  attemptId: string;
  examId: string;
  examName: string;
  attemptNumber: number;
  status: AttemptStatus;
  submitReason: SubmitReason | null;
  startedAt: string;
  submittedAt: string | null;
  durationSeconds: number | null;
  scoreVisible: boolean;
  totalScore: number | null;
  maxScore: number | null;
  percentage: number | null;
  correctCount: number | null;
  totalQuestion: number;
  passed: boolean | null;
  reviewAvailable: boolean;
  reviewAvailableAt: string | null;
  questions: ReviewQuestion[] | null;
}

export interface StudentHistoryItem {
  attemptId: string;
  examId: string;
  examCode: string;
  examName: string;
  attemptNumber: number;
  status: AttemptStatus;
  startedAt: string;
  submittedAt: string | null;
  scoreVisible: boolean;
  totalScore: number | null;
  maxScore: number | null;
  percentage: number | null;
  passed: boolean | null;
}

// ----- Admin -----

export interface AdminAttemptRow {
  attemptId: string;
  userId: string;
  userName: string;
  fullName: string;
  attemptNumber: number;
  versionNumber: number;
  status: AttemptStatus;
  submitReason: SubmitReason | null;
  startedAt: string;
  expiredAt: string;
  submittedAt: string | null;
  timeExtensionMinutes: number;
  totalScore: number | null;
  maxScore: number | null;
  percentage: number | null;
  passed: boolean | null;
  eventCount: number;
}

export interface AdminAnswer {
  attemptQuestionId: string;
  order: number;
  content: string;
  type: QuestionType;
  answerDataType: AnswerDataType | null;
  maxScore: number;
  selectedOptions: string[];
  answerText: string | null;
  isAnswered: boolean;
  isMarkedForReview: boolean;
  answeredAt: string | null;
  saveCount: number;
  correctOptions: string[];
  acceptedAnswers: string[];
  correctAnswerNumber: number | null;
  isCorrect: boolean | null;
  score: number | null;
  isVoided: boolean;
}

export interface AdminAttemptDetail {
  attemptId: string;
  examId: string;
  examCode: string;
  examName: string;
  examVersionId: string;
  versionNumber: number;
  userId: string;
  userName: string;
  fullName: string;
  attemptNumber: number;
  status: AttemptStatus;
  submitReason: SubmitReason | null;
  startedAt: string;
  expiredAt: string;
  submittedAt: string | null;
  timeExtensionMinutes: number;
  cancelledAt: string | null;
  cancelReason: string | null;
  startedIp: string | null;
  startedUserAgent: string | null;
  submittedIp: string | null;
  totalScore: number | null;
  maxScore: number | null;
  percentage: number | null;
  correctCount: number | null;
  passed: boolean | null;
  gradingRevision: number | null;
  answers: AdminAnswer[];
  events: { type: AttemptEventType; clientTime: string | null; serverTime: string; ipAddress: string | null; detail: string | null }[];
}

export interface AdminResultRow {
  attemptId: string;
  userId: string;
  userName: string;
  fullName: string;
  email: string;
  attemptNumber: number;
  status: AttemptStatus;
  startedAt: string;
  submittedAt: string;
  durationSeconds: number;
  totalScore: number;
  maxScore: number;
  percentage: number;
  correctCount: number;
  totalQuestion: number;
  passed: boolean | null;
  isOfficial: boolean;
}

export interface RegradeSummary {
  correctionId: string;
  affectedAttempts: number;
  changedResults: number;
}

export interface Dashboard {
  totalUsers: number;
  totalStudents: number;
  totalExams: number;
  openExams: number;
  attemptsToday: number;
  inProgressAttempts: number;
  averagePercentage30Days: number | null;
  passRate30Days: number | null;
  upcomingExams: { examId: string; code: string; name: string; startAt: string | null; endAt: string | null; inProgressAttempts: number }[];
}

export interface QuestionStat {
  examQuestionId: string;
  order: number;
  contentPreview: string;
  type: QuestionType;
  attemptCount: number;
  answeredCount: number;
  correctCount: number;
  wrongCount: number;
  blankCount: number;
  correctRate: number | null;
  averageScore: number | null;
  maxScore: number;
  isVoided: boolean;
  optionDistribution: { optionCode: string; selectedCount: number }[];
}

export interface AuditLog {
  id: number;
  createdAt: string;
  userId: string | null;
  userName: string | null;
  action: string;
  entityName: string | null;
  entityId: string | null;
  oldValue: string | null;
  newValue: string | null;
  reason: string | null;
  ipAddress: string | null;
  traceId: string | null;
}

/** Số lượt cấp thêm cho một học viên ở một đề (ExamUserOverrides). */
export interface UserOverride {
  userId: string;
  userName: string;
  fullName: string;
  extraAttempts: number;
  note: string | null;
  updatedAt: string;
}

export type AnswerKeyCorrectionType = "ANSWER_KEY" | "VOID";

/** Lịch sử sửa đáp án / hủy câu (D-11). */
export interface AnswerKeyCorrection {
  id: string;
  examQuestionId: string;
  versionNumber: number;
  questionOrder: number;
  correctionType: AnswerKeyCorrectionType;
  oldKeyJson: string;
  newKeyJson: string;
  reason: string;
  affectedAttemptCount: number;
  correctedBy: string;
  correctedByName: string;
  correctedAt: string;
}

export interface QuestionImportIssue {
  field: string | null;
  code: string;
  message: string;
}

export interface QuestionImportRow {
  rowNumber: number;
  code: string | null;
  questionType: QuestionType | null;
  contentPreview: string;
  issues: QuestionImportIssue[];
}

/** Kết quả import câu hỏi từ Excel; imported = false thì không câu nào được tạo. */
export interface QuestionImportResult {
  dryRun: boolean;
  imported: boolean;
  totalRows: number;
  validRows: number;
  importedCount: number;
  rows: QuestionImportRow[];
}
