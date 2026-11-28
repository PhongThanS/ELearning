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

/** Kết quả khóa / mở khóa hàng loạt: skipped = tài khoản của chính mình / không tồn tại / đã ẩn danh. */
export interface BulkUserStatusResult {
  changed: number;
  unchanged: number;
  skipped: number;
}

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

export interface RegistrationStatus {
  allowSelfRegistration: boolean;
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
  /** Lớp học viên đang học và lớp giáo viên chủ nhiệm. */
  classes?: { id: string; code: string; name: string }[];
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
  /** Liên hệ (D-43): SĐT học sinh, tên và SĐT phụ huynh. */
  studentPhone?: string | null;
  parentName?: string | null;
  parentPhone?: string | null;
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

/** Lớp học (D-28): học viên ↔ lớp nhiều-nhiều. Ngày học dạng "yyyy-MM-dd". */
export interface Classroom {
  id: string;
  code: string;
  name: string;
  schoolYear: string | null;
  startDate: string | null;
  endDate: string | null;
  description: string | null;
  isActive: boolean;
  studentCount: number;
  examCount: number;
  createdAt: string;
  rowVersion: string;
  homeroomTeacherId?: string | null;
  homeroomTeacherName?: string | null;
  homeroomTeacherEmail?: string | null;
}

export interface ClassroomStudent {
  userId: string;
  userName: string;
  fullName: string;
  email: string;
  isActive: boolean;
  joinedAt: string;
}

export interface MyClassroom {
  id: string;
  code: string;
  name: string;
  schoolYear: string | null;
  startDate: string | null;
  endDate: string | null;
  description: string | null;
  studentCount: number;
  examCount: number;
  joinedAt: string;
}

export interface StudentClassExamItem {
  examId: string;
  code: string;
  name: string;
  startAt: string | null;
  endAt: string | null;
  durationMinutes: number;
  questionCount: number;
  maxAttempts: number;
  usedAttempts: number;
  officialScore: number | null;
  percentage: number | null;
  passed: boolean | null;
  lastSubmittedAt: string | null;
  isSubmitted: boolean;
  isInProgress: boolean;
  inProgressAttemptId: string | null;
  statusText: string;
  latestAttemptId?: string | null;
  examVersionId?: string | null;
}

export interface StudentClassProgress {
  userId: string;
  userName: string;
  fullName: string;
  email: string;
  classroomId: string;
  classroomCode: string;
  classroomName: string;
  totalAssignedExams: number;
  completedExams: number;
  inProgressExams: number;
  notStartedExams: number;
  exams: StudentClassExamItem[];
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

export type QuestionType = "SINGLE_CHOICE" | "MULTIPLE_CHOICE" | "TRUE_FALSE" | "FILL_IN" | "ESSAY";
export type AnswerDataType = "TEXT" | "NUMBER";
/** Mức độ (độ khó) 1–5: Mức 1 nhận biết … Mức 4 vận dụng cao, Mức 5 nâng cao. */
export type QuestionDifficulty =
  | "LEVEL1" | "LEVEL2" | "LEVEL3" | "LEVEL4" | "LEVEL5"
  | "LEVEL6" | "LEVEL7" | "LEVEL8" | "LEVEL9" | "LEVEL10";
export type ContentFormat = "PLAIN" | "MARKDOWN";

export interface Category {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  /** Câu trực thuộc chính chuyên đề. */
  questionCount: number;
  createdAt: string;
  updatedAt: string | null;
  rowVersion: string;
  /** Chuyên đề cha; null = chuyên đề gốc (2 cấp). */
  parentId: string | null;
  parentName: string | null;
  childCount: number;
  /** Gồm cả câu của các chuyên đề con. */
  totalQuestionCount: number;
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
  partialScoring: boolean;
  /** Ảnh trong nội dung: id → URL đã ký, chỉ gồm ảnh của các trường được trả về (D-27). */
  media?: MediaUrlMap;
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
  partialScoring: boolean;
}

// ----- Đề thi -----

export type ExamStatus = "DRAFT" | "PUBLISHED" | "CLOSED";
export type ExamVersionStatus = "DRAFT" | "PUBLISHED" | "ARCHIVED";
export type AccessMode = "PUBLIC" | "ASSIGNED";
export type RetakeScoringPolicy = "HIGHEST" | "LATEST";
export type ScoreVisibility = "IMMEDIATE" | "AFTER_EXAM_END" | "HIDDEN";
export type ReviewPolicy = "NEVER" | "AFTER_SUBMIT" | "AFTER_EXAM_END" | "AFTER_LAST_ATTEMPT";

/** Loại đề (D-33): GRADED = đề tính điểm; PRACTICE = đề luyện, làm lại không giới hạn, không tính giờ. */
export type ExamKind = "GRADED" | "PRACTICE";

export interface ExamListItem {
  kind?: ExamKind;
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
  /** Chuyên đề của đề (D-30). */
  categoryId?: string | null;
  categoryName?: string | null;
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
  reviewPolicy?: ReviewPolicy | null;
}

export interface ExamDetail {
  kind?: ExamKind;
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
  publishedReviewPolicy?: ReviewPolicy | null;
  /** Chuyên đề của đề (D-30): có thì mọi câu trong đề thuộc chuyên đề này. */
  categoryId?: string | null;
  categoryName?: string | null;
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

export type MatrixDrawMode = "STATIC" | "POOL";

export interface MatrixStatsQuery {
  categoryId?: string | null;
  tag?: string | null;
  questionType?: QuestionType | null;
}

export interface CategoryMatrixStats {
  categoryId: string | null;
  totalCandidates: number;
  level1Candidates: number;
  level2Candidates: number;
  level3Candidates: number;
  level4Candidates: number;
  level5Candidates?: number;
  level6Candidates?: number;
  level7Candidates?: number;
  level8Candidates?: number;
  level9Candidates?: number;
  level10Candidates?: number;
  unspecifiedCandidates: number;
}

export interface MatrixStats {
  totalCandidates: number;
  level1Candidates: number;
  level2Candidates: number;
  level3Candidates: number;
  level4Candidates: number;
  level5Candidates?: number;
  level6Candidates?: number;
  level7Candidates?: number;
  level8Candidates?: number;
  level9Candidates?: number;
  level10Candidates?: number;
  unspecifiedCandidates: number;
  byCategory?: CategoryMatrixStats[];
}

export interface MatrixTopicItem {
  categoryId: string | null;
  level1Count: number;
  level2Count: number;
  level3Count: number;
  level4Count: number;
  level5Count: number;
  level6Count?: number;
  level7Count?: number;
  level8Count?: number;
  level9Count?: number;
  level10Count?: number;
  scorePerQuestion?: number | null;
}

export interface MatrixDrawInput {
  categoryId?: string | null;
  tag?: string | null;
  questionType?: QuestionType | null;
  level1Count?: number;
  level2Count?: number;
  level3Count?: number;
  level4Count?: number;
  level5Count?: number;
  level6Count?: number;
  level7Count?: number;
  level8Count?: number;
  level9Count?: number;
  level10Count?: number;
  scorePerQuestion?: number | null;
  mode: MatrixDrawMode;
  topics?: MatrixTopicItem[];
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
  /** Ảnh trong nội dung: id → URL đã ký, chỉ gồm ảnh của các trường được trả về (D-27). */
  media?: MediaUrlMap;
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
  expectedLength?: number | null;
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
  /** Ảnh trong nội dung: id → URL đã ký, chỉ gồm ảnh của các trường được trả về (D-27). */
  media?: MediaUrlMap;
}

export interface Assignments {
  accessMode: AccessMode;
  groups: { id: string; code: string; name: string; memberCount: number }[];
  users: { id: string; userName: string; fullName: string }[];
  classrooms: { id: string; code: string; name: string; schoolYear: string | null; studentCount: number }[];
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

/** Điểm gộp theo chuyên đề của đề (categoryId null = đề chưa gắn chuyên đề). */
export interface StudentCategoryScore {
  categoryId: string | null;
  categoryName: string | null;
  parentName: string | null;
  examCount: number;
  averagePercentage: number;
  bestPercentage: number;
}

export interface StudentExamItem {
  kind?: ExamKind;
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
  latestAttemptId?: string | null;
  examVersionId?: string | null;
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
  pendingManualGrading: boolean;
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
  kind?: ExamKind;
  attemptId: string;
  examId: string;
  examName: string;
  attemptNumber: number;
  status: AttemptStatus;
  resumed: boolean;
  startedAt: string;
  expiredAt: string;
  serverTime: string;
  durationMinutes?: number;
  questions: AttemptQuestion[];
  /** Ảnh trong nội dung: id → URL đã ký, chỉ gồm ảnh của các trường được trả về (D-27). */
  media?: MediaUrlMap;
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
  /** Nhận xét của người chấm (câu tự luận). */
  manualComment: string | null;
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
  /** Còn câu tự luận chờ chấm tay: điểm chưa công bố. */
  pendingManualGrading: boolean;
  /** Ảnh trong nội dung: id → URL đã ký, chỉ gồm ảnh của các trường được trả về (D-27). */
  media?: MediaUrlMap;
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
  pendingManualGrading: boolean;
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

export interface AdminOptionDto {
  optionCode: string;
  content: string;
  isCorrect: boolean;
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
  options?: AdminOptionDto[];
  explanation?: string | null;
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
  /** Ảnh trong nội dung: id → URL đã ký, chỉ gồm ảnh của các trường được trả về (D-27). */
  media?: MediaUrlMap;
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
  /** Số câu tự luận chờ chấm tay; điểm đang là tạm tính khi khác 0. */
  pendingManualCount: number;
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

export interface UserImportRow {
  rowNumber: number;
  userName: string | null;
  fullName: string | null;
  roles: string[];
  classes: string[];
  /** Chỉ có sau khi import thật, với dòng để trống mật khẩu (hiện một lần). */
  temporaryPassword: string | null;
  issues: QuestionImportIssue[];
}

export interface VideoImportRow {
  rowNumber: number;
  title: string | null;
  videoUrl: string | null;
  categoryName: string | null;
  classes: string[];
  practiceExamName: string | null;
  issues: QuestionImportIssue[];
}

/** Kết quả thêm video từ Excel (D-44); imported = false thì không video nào được thêm. */
export interface VideoImportResult {
  dryRun: boolean;
  imported: boolean;
  totalRows: number;
  validRows: number;
  importedCount: number;
  rows: VideoImportRow[];
}

/** Kết quả import tài khoản từ Excel; imported = false thì không tài khoản nào được tạo. */
export interface UserImportResult {
  dryRun: boolean;
  imported: boolean;
  totalRows: number;
  validRows: number;
  importedCount: number;
  rows: UserImportRow[];
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

/** Một câu tự luận cần / đã chấm tay. */
export interface ManualGradingItem {
  attemptId: string;
  attemptQuestionId: string;
  userId: string;
  userName: string;
  fullName: string;
  attemptNumber: number;
  submittedAt: string | null;
  questionOrder: number;
  examQuestionId: string;
  content: string;
  contentFormat: ContentFormat;
  explanation: string | null;
  maxScore: number;
  answerText: string | null;
  manualScore: number | null;
  manualComment: string | null;
  manualGradedAt: string | null;
  /** Ảnh trong nội dung: id → URL đã ký, chỉ gồm ảnh của các trường được trả về (D-27). */
  media?: MediaUrlMap;
}

export interface ManualGradeResult {
  attemptId: string;
  totalScore: number;
  maxScore: number;
  percentage: number;
  passed: boolean | null;
  pendingManualCount: number;
}

/** id ảnh (GUID chữ thường) → URL đã ký, xem MarkdownView. */
export type MediaUrlMap = Record<string, string>;

export interface MediaUpload {
  id: string;
  contentType: string;
  sizeBytes: number;
  url: string;
  /** Chuỗi chèn vào nội dung: ![](media:<id>) */
  markdown: string;
}

// ----- Video bài giảng -----

export interface VideoLesson {
  id: string;
  title: string;
  videoUrl: string;
  /** Tài liệu PDF trên Google Drive, xem song song với video. */
  documentUrl?: string | null;
  youtubeVideoId?: string | null;
  thumbnailUrl?: string | null;
  description?: string | null;
  categoryId?: string | null;
  categoryName?: string | null;
  /** Lớp được xem; rỗng = bài giảng chung (mọi học viên). */
  classrooms: VideoClassroom[];
  durationMinutes?: number | null;
  displayOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
  rowVersion: string;
  /** Đề luyện đi kèm (D-38): học viên bấm "Luyện đề" để làm. */
  practiceExamId?: string | null;
  practiceExamCode?: string | null;
  practiceExamName?: string | null;
  /** Bài tập về nhà (D-39): link PDF Google Drive / Docs. */
  homeworkUrl?: string | null;
}

/** Kết quả bấm "Luyện đề" ở video. Ready = false và AlreadyAttempted = true: hỏi học viên có làm lại không. */
export interface VideoPractice {
  examId: string;
  examCode: string;
  examName: string;
  alreadyAttempted: boolean;
  attemptCount: number;
  ready: boolean;
}

export interface VideoClassroom {
  id: string;
  code: string;
  name: string;
}

export interface YoutubeVideoInfo {
  videoId: string;
  title: string;
  authorName: string | null;
  thumbnailUrl: string;
}

export interface CreateVideoInput {
  title: string;
  videoUrl: string;
  documentUrl?: string | null;
  description?: string | null;
  categoryId?: string | null;
  classroomIds?: string[];
  durationMinutes?: number | null;
  displayOrder?: number;
  practiceExamId?: string | null;
  homeworkUrl?: string | null;
}

export interface UpdateVideoInput {
  title: string;
  videoUrl: string;
  documentUrl?: string | null;
  description?: string | null;
  categoryId?: string | null;
  classroomIds?: string[];
  durationMinutes?: number | null;
  displayOrder: number;
  isActive: boolean;
  rowVersion: string;
  /** Luôn gửi: null = bỏ đề luyện. */
  practiceExamId: string | null;
  /** Luôn gửi: null = bỏ bài tập về nhà. */
  homeworkUrl: string | null;
}

export interface VideoListQuery extends PageQuery {
  categoryId?: string;
  classroomId?: string;
  keyword?: string;
  isActive?: boolean;
}

// ----- Nhập đề từ file Markdown "elearning-exam/1" (D-29) -----

export interface MarkdownImportIssue {
  line: number | null;
  field: string | null;
  code: string;
  message: string;
}

/** Câu hỏi đọc được (cùng cấu trúc body tạo câu hỏi); chỉ admin thấy, có đáp án để xem trước. */
export interface MarkdownImportQuestionInput {
  code: string | null;
  categoryId: string | null;
  content: string;
  contentFormat: ContentFormat;
  questionType: QuestionType;
  answerDataType: AnswerDataType | null;
  defaultScore: number;
  explanation: string | null;
  options: { optionCode: string | null; content: string; isCorrect: boolean }[] | null;
  acceptedAnswers: string[] | null;
  correctAnswerNumber: number | null;
  numericTolerance: number | null;
  caseSensitive: boolean;
  ignoreAccent: boolean;
  difficulty: QuestionDifficulty | null;
  partialScoring: boolean;
  tags: string[] | null;
}

export interface MarkdownImportQuestion {
  index: number;
  line: number;
  label: string;
  part: string | null;
  questionId: string | null;
  code: string | null;
  question: MarkdownImportQuestionInput | null;
  issues: MarkdownImportIssue[];
}

export interface DocxImage {
  name: string;
  contentType: string;
  dataBase64: string;
  part: number;
  question: number | null;
  bytes: number;
}

export interface DocxSkippedImage {
  name: string | null;
  part: number;
  question: number | null;
  reason: string;
}

export interface DocxImageResult {
  images: DocxImage[];
  skipped: DocxSkippedImage[];
  /** Số công thức MathType / Equation 3.0 (đối tượng OLE) trong file Word — AI không đọc được trong .docx. */
  mathTypeFormulas: number;
  /** Văn bản cho AI (như docx-for-ai), chỉ có khi gọi với withText; null khi không đọc được. */
  aiText?: string | null;
}

/** Đề Word đã chuyển thành văn bản cho AI đọc: công thức (cả MathType) thành LaTeX, hình đặt tên sẵn. */
export interface DocxAiTextResult {
  text: string;
  formulas: number;
  unreadFormulas: number;
  pictures: number;
}

export interface MarkdownImportResult {
  dryRun: boolean;
  imported: boolean;
  exam: {
    title: string | null;
    code: string | null;
    durationMinutes: number | null;
    categoryCode: string | null;
    description: string | null;
    instructions: string | null;
  };
  categoryId: string | null;
  totalQuestions: number;
  validQuestions: number;
  totalScore: number;
  questions: MarkdownImportQuestion[];
  fileIssues: MarkdownImportIssue[];
  missingImages: string[];
}

// ----- Điểm danh (D-31) -----

/** ONLINE: học trực tuyến — tính là có tham gia buổi học. */
export type AttendanceStatus = "PRESENT" | "LATE" | "ONLINE" | "EXCUSED" | "UNEXCUSED";

export type HomeworkStatus = "EXCELLENT" | "DONE" | "PARTIAL" | "MISSING";

export interface AttendanceClass {
  id: string;
  code: string;
  name: string;
  schoolYear: string | null;
  isActive: boolean;
  studentCount: number;
  homeroomTeacherName: string | null;
}

export interface AttendanceEntry {
  status: AttendanceStatus | null;
  homeworkStatus: HomeworkStatus | null;
  homeworkScore: number | null;
  note: string | null;
  /** Phân loại 1..5 trong buổi (D-47); null = không phân loại. */
  level?: number | null;
}

export interface AttendanceStudent extends AttendanceEntry {
  studentId: string;
  userName: string;
  fullName: string;
  parentName: string | null;
  parentPhone: string | null;
  studentPhone?: string | null;
  /** false: học sinh đã rời lớp nhưng còn dữ liệu ở buổi này. */
  inClass: boolean;
}

export interface AttendanceSession {
  classroomId: string;
  classroomCode: string;
  classroomName: string;
  sessionDate: string;
  saved: boolean;
  topic: string | null;
  nextHomework: string | null;
  updatedAt: string | null;
  updatedByName: string | null;
  students: AttendanceStudent[];
  /** Nhận xét chung theo phân loại, phần tử 0 là loại 1 (D-47). */
  levelComments?: (string | null)[];
}

export interface SaveAttendanceInput {
  topic: string | null;
  nextHomework: string | null;
  records: (AttendanceEntry & { studentId: string })[];
  /** 5 nhận xét chung theo phân loại (D-47); bỏ qua = giữ nguyên. */
  levelComments?: (string | null)[];
}

export interface SaveAttendanceResult {
  updatedAt: string;
  savedRecords: number;
}

export interface StudentContact {
  studentId: string;
  parentName: string | null;
  parentPhone: string | null;
  studentPhone?: string | null;
}

export interface AttendanceHistoryItem {
  sessionDate: string;
  topic: string | null;
  status: AttendanceStatus | null;
  homeworkStatus: HomeworkStatus | null;
  homeworkScore: number | null;
  note: string | null;
}

export interface AttendanceStudentHistory {
  studentId: string;
  userName: string;
  fullName: string;
  parentName: string | null;
  parentPhone: string | null;
  classroomName: string;
  year: number;
  month: number;
  averageScore: number | null;
  classRank: number | null;
  rankedCount: number;
  items: AttendanceHistoryItem[];
}

// ---------------------------------------------------------------- Gửi tin Zalo cho phụ huynh (D-45)

export type ZaloMessageKind = "DAILY_REPORT" | "ANNOUNCEMENT";
export type ZaloMessageStatus = "PENDING" | "SENDING" | "SENT" | "FAILED" | "CANCELLED";
/** Lý do bỏ qua một phụ huynh khi lập danh sách gửi. */
export type ZaloSkipReason = "NO_PHONE" | "INVALID_PHONE" | "NOT_MARKED" | "DUPLICATE_PHONE" | "NOT_SELECTED";

export interface ZaloStatus {
  enabled: boolean;
  provider: string;
  sandboxMode: boolean;
  missingTemplates: string[];
}

export interface ZaloBroadcastInput {
  kind: ZaloMessageKind;
  sessionDate?: string | null;
  content?: string | null;
  deadline?: string | null;
  attention?: string | null;
  studentIds?: string[] | null;
  force?: boolean;
}

export interface ZaloRecipient {
  studentId: string;
  studentName: string;
  parentName: string | null;
  parentPhone: string | null;
  skipReason: ZaloSkipReason | null;
  data: Record<string, string> | null;
}

export interface ZaloPreview {
  kind: ZaloMessageKind;
  sendableCount: number;
  alreadySent: boolean;
  recipients: ZaloRecipient[];
}

export interface ZaloBroadcast {
  id: string;
  kind: ZaloMessageKind;
  sessionDate: string | null;
  content: string | null;
  createdAt: string;
  createdByName: string | null;
  total: number;
  pending: number;
  sent: number;
  failed: number;
  cancelled: number;
}

export interface ZaloBroadcastCreated {
  broadcast: ZaloBroadcast;
  skipped: ZaloRecipient[];
}

export interface ZaloMessage {
  id: string;
  studentId: string;
  studentName: string;
  recipientName: string | null;
  phone: string;
  status: ZaloMessageStatus;
  attempts: number;
  sentAt: string | null;
  errorCode: string | null;
  errorMessage: string | null;
}

export interface ZaloBroadcastDetail {
  broadcast: ZaloBroadcast;
  messages: ZaloMessage[];
}

/* ---------- Báo câu hỏi lỗi (D-46) ---------- */

export type QuestionReportReason = "WRONG_ANSWER" | "WRONG_CONTENT" | "TYPO" | "MISSING_MEDIA" | "OTHER";
export type QuestionReportStatus = "OPEN" | "RESOLVED" | "REJECTED";

export interface MyQuestionReport {
  id: string;
  questionOrder: number;
  reason: QuestionReportReason;
  note: string | null;
  status: QuestionReportStatus;
  createdAt: string;
  resolutionNote: string | null;
}

export interface ReportedQuestion {
  examQuestionId: string;
  examId: string;
  examVersionId: string;
  examCode: string;
  examName: string;
  versionNumber: number;
  questionOrder: number;
  contentPreview: string;
  questionType: QuestionType;
  sourceQuestionId: string | null;
  isVoided: boolean;
  reportCount: number;
  openCount: number;
  lastReportedAt: string;
  reasons: QuestionReportReason[];
}

export interface QuestionReportItem {
  id: string;
  attemptId: string;
  userId: string;
  userName: string;
  fullName: string;
  questionOrder: number;
  reason: QuestionReportReason;
  note: string | null;
  status: QuestionReportStatus;
  createdAt: string;
  resolvedAt: string | null;
  resolvedByName: string | null;
  resolutionNote: string | null;
}

export interface ReportedQuestionDetail {
  summary: ReportedQuestion;
  content: string;
  contentFormat: ContentFormat;
  answerDataType: string | null;
  score: number;
  options: { code: string; content: string; isCorrect: boolean }[];
  acceptedAnswers: string[];
  correctAnswerNumber: number | null;
  explanation: string | null;
  reports: QuestionReportItem[];
  media: Record<string, string>;
}
