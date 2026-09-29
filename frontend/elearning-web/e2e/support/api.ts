import { request, type APIRequestContext } from "@playwright/test";
import { ADMIN_PASSWORD, ADMIN_USER, BASE_URL, STUDENT_PASSWORD, uniqueId } from "./env";

interface Envelope<T> {
  success: boolean;
  data: T | null;
  message: string | null;
  errors: { field: string | null; code: string; message: string }[];
}

export interface ExamDetail {
  id: string;
  code: string;
  name: string;
  publishedVersionId: string | null;
  draftVersionId: string | null;
}

export interface AttemptQuestion {
  id: string;
  order: number;
  type: string;
}

export interface Attempt {
  attemptId: string;
  questions: AttemptQuestion[];
}

/** Câu trả lời theo thứ tự câu hỏi: selectedOptions cho câu chọn, answerText cho câu điền. */
export type AnswerInput = { selectedOptions: string[] } | { answerText: string };

/**
 * Gọi API qua cùng origin với frontend (proxy của vite preview / Nginx), có header chống CSRF như SPA.
 * Dùng để chuẩn bị dữ liệu nhanh; phần cần kiểm tra thì thao tác trên UI.
 */
export class ApiSession {
  private constructor(
    private readonly ctx: APIRequestContext,
    private readonly token: string,
  ) {}

  static async login(userName: string, password: string): Promise<ApiSession> {
    const ctx = await request.newContext({ baseURL: BASE_URL, extraHTTPHeaders: { "X-Requested-With": "XMLHttpRequest" } });
    const response = await ctx.post("/api/auth/login", { data: { userName, password } });
    const body = (await response.json()) as Envelope<{ accessToken: string }>;
    if (!response.ok() || !body.data) {
      throw new Error(`Đăng nhập ${userName} thất bại: ${response.status()} ${JSON.stringify(body.errors)}`);
    }
    return new ApiSession(ctx, body.data.accessToken);
  }

  static admin(): Promise<ApiSession> {
    return ApiSession.login(ADMIN_USER, ADMIN_PASSWORD);
  }

  async call<T>(method: "GET" | "POST" | "PUT", url: string, data?: unknown): Promise<T> {
    const response = await this.ctx.fetch(url, { method, data, headers: { Authorization: `Bearer ${this.token}` } });
    const body = (await response.json()) as Envelope<T>;
    if (!response.ok()) {
      throw new Error(`${method} ${url} → ${response.status()} ${JSON.stringify(body.errors)}`);
    }
    return body.data as T;
  }

  dispose(): Promise<void> {
    return this.ctx.dispose();
  }
}

/** Dữ liệu dựng sẵn cho một kịch bản: một nhóm, một học viên thuộc nhóm. */
export interface Cohort {
  groupId: string;
  groupCode: string;
  userName: string;
  fullName: string;
}

export async function createCohort(admin: ApiSession): Promise<Cohort> {
  const groupCode = uniqueId("G-E2E-");
  const group = await admin.call<{ id: string }>("POST", "/api/groups", { code: groupCode, name: `Nhóm E2E ${groupCode}` });
  const userName = uniqueId("hv.e2e.").toLowerCase();
  const fullName = `Học Viên ${userName.slice(-6)}`;
  await admin.call("POST", "/api/users", {
    userName,
    email: `${userName}@e2e.test`,
    fullName,
    password: STUDENT_PASSWORD,
    groupIds: [group.id],
  });
  return { groupId: group.id, groupCode, userName, fullName };
}

/**
 * Đề 5 câu đủ 4 loại, gán cho nhóm, đã publish. Đáp án đúng:
 * 1 chọn một → B · 2 chọn nhiều → A + C · 3 đúng/sai → Đúng · 4 điền chữ → "Hà Nội" (bỏ dấu) · 5 điền số → 3,5
 */
export async function createPublishedExam(
  admin: ApiSession,
  groupId: string,
  options: { durationMinutes?: number; reviewPolicy?: string; name?: string } = {},
): Promise<ExamDetail> {
  const tag = uniqueId("");
  const question = (body: object) => admin.call<{ id: string }>("POST", "/api/questions", body).then((q) => q.id);
  const questionIds = [
    await question({
      content: `[${tag}] Ngôn ngữ nào chạy trên .NET?`,
      questionType: "SINGLE_CHOICE",
      options: [
        { content: "Python thuần", isCorrect: false },
        { content: "C Sharp", isCorrect: true },
        { content: "Ruby thuần", isCorrect: false },
      ],
    }),
    await question({
      content: `[${tag}] Những số nào là số chẵn?`,
      questionType: "MULTIPLE_CHOICE",
      options: [
        { content: "Số hai", isCorrect: true },
        { content: "Số ba", isCorrect: false },
        { content: "Số bốn", isCorrect: true },
      ],
    }),
    await question({
      content: `[${tag}] SQL Server hỗ trợ transaction.`,
      questionType: "TRUE_FALSE",
      options: [
        { optionCode: "TRUE", content: "Đúng", isCorrect: true },
        { optionCode: "FALSE", content: "Sai", isCorrect: false },
      ],
    }),
    await question({
      content: `[${tag}] Thủ đô của Việt Nam?`,
      questionType: "FILL_IN",
      answerDataType: "TEXT",
      acceptedAnswers: ["Hà Nội"],
      ignoreAccent: true,
    }),
    await question({
      content: `[${tag}] 7 chia 2 bằng bao nhiêu?`,
      questionType: "FILL_IN",
      answerDataType: "NUMBER",
      correctAnswerNumber: 3.5,
    }),
  ];

  const exam = await admin.call<ExamDetail>("POST", "/api/exams", {
    code: `E2E-${tag}`,
    name: options.name ?? `Đề E2E ${tag}`,
    maxAttempts: 1,
    accessMode: "ASSIGNED",
    durationMinutes: options.durationMinutes ?? 30,
    passPercentage: 50,
    scoreVisibility: "IMMEDIATE",
    reviewPolicy: options.reviewPolicy ?? "AFTER_SUBMIT",
  });
  const versionUrl = `/api/exams/${exam.id}/versions/${exam.draftVersionId}`;
  await admin.call("POST", `${versionUrl}/questions`, { questionIds });
  await admin.call("PUT", `/api/exams/${exam.id}/assignments`, { groupIds: [groupId], userIds: [] });
  await admin.call("POST", `${versionUrl}/publish`);
  return admin.call<ExamDetail>("GET", `/api/exams/${exam.id}`);
}

/** Học viên làm bài qua API (dùng khi kịch bản chỉ cần có bài nộp để kiểm tra phía admin). */
export async function takeExamViaApi(userName: string, examId: string, answers: AnswerInput[]): Promise<string> {
  const student = await ApiSession.login(userName, STUDENT_PASSWORD);
  try {
    const attempt = await student.call<Attempt>("POST", `/api/student/exams/${examId}/start`);
    const ordered = [...attempt.questions].sort((a, b) => a.order - b.order);
    await student.call("PUT", `/api/student/attempts/${attempt.attemptId}/answers`, {
      answers: ordered.map((q, i) => ({ questionId: q.id, clientSeq: 1, isMarkedForReview: false, ...answers[i] })),
    });
    await student.call("POST", `/api/student/attempts/${attempt.attemptId}/submit`);
    return attempt.attemptId;
  } finally {
    await student.dispose();
  }
}
