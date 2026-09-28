import type { Attempt, SaveAnswerItem, SaveAnswersResponse } from "../../types/api";

/**
 * State của exam player (docs/06-frontend.md mục 4.3). Thuần, không phụ thuộc React, để test.
 * - clientSeq tăng mỗi lần sửa; syncedSeq là seq server đã xác nhận. Câu "chưa lưu" khi clientSeq > syncedSeq.
 * - Không lưu "thời gian còn lại"; luôn suy ra từ expiredAt và độ lệch đồng hồ với server.
 */

export interface AnswerDraft {
  selectedOptions: string[];
  answerText: string | null;
  isMarkedForReview: boolean;
  clientSeq: number;
  syncedSeq: number;
}

export interface PlayerState {
  attemptId: string;
  currentIndex: number;
  questionIds: string[];
  answers: Record<string, AnswerDraft>;
  clockOffsetMs: number;
  expiredAt: string;
  online: boolean;
}

export type PlayerAction =
  | { type: "goto"; index: number }
  | { type: "select"; questionId: string; options: string[] }
  | { type: "text"; questionId: string; text: string }
  | { type: "mark"; questionId: string; marked: boolean }
  | { type: "synced"; response: SaveAnswersResponse; receivedAt: number }
  | { type: "online"; online: boolean }
  | { type: "clock"; serverTime: string; expiredAt: string; receivedAt: number };

let lastSeq = 0;

/**
 * Sinh clientSeq tăng dần: max(lastSeq + 1, Date.now()) (D-18). Nhờ dựa trên Date.now(), seq vẫn tăng
 * qua các lần tải lại trang và giữa các tab trên cùng máy. Chỉ dùng để sắp thứ tự ghi, không mang nghĩa thời gian.
 */
export function nextClientSeq(now: number = Date.now()): number {
  lastSeq = Math.max(lastSeq + 1, now);
  return lastSeq;
}

export function initPlayerState(attempt: Attempt, receivedAt: number = Date.now(), backup?: BackupData | null): PlayerState {
  const answers: Record<string, AnswerDraft> = {};
  for (const q of [...attempt.questions].sort((a, b) => a.order - b.order)) {
    const server: AnswerDraft = {
      selectedOptions: [...q.answer.selectedOptions],
      answerText: q.answer.answerText,
      isMarkedForReview: q.answer.isMarkedForReview,
      clientSeq: q.answer.clientSeq,
      syncedSeq: q.answer.clientSeq,
    };
    // Backup trên máy mới hơn server (mất mạng trước khi lưu kịp) → dùng backup và gửi lại
    const local = backup?.answers[q.id];
    answers[q.id] =
      local && local.clientSeq > server.clientSeq
        ? { ...local, selectedOptions: [...local.selectedOptions], syncedSeq: server.clientSeq }
        : server;
    lastSeq = Math.max(lastSeq, answers[q.id]!.clientSeq);
  }

  return {
    attemptId: attempt.attemptId,
    currentIndex: 0,
    questionIds: [...attempt.questions].sort((a, b) => a.order - b.order).map((q) => q.id),
    answers,
    clockOffsetMs: new Date(attempt.serverTime).getTime() - receivedAt,
    expiredAt: attempt.expiredAt,
    online: true,
  };
}

export function playerReducer(state: PlayerState, action: PlayerAction): PlayerState {
  switch (action.type) {
    case "goto":
      return { ...state, currentIndex: Math.max(0, Math.min(action.index, state.questionIds.length - 1)) };
    case "select":
      return updateAnswer(state, action.questionId, { selectedOptions: [...action.options].sort() });
    case "text":
      return updateAnswer(state, action.questionId, { answerText: action.text });
    case "mark":
      return updateAnswer(state, action.questionId, { isMarkedForReview: action.marked });
    case "synced": {
      const answers = { ...state.answers };
      for (const item of action.response.answers) {
        const current = answers[item.questionId];
        if (current) {
          // Server trả seq đang lưu (kể cả khi bỏ qua bản cũ) → mọi seq ≤ giá trị đó coi như đã đồng bộ
          answers[item.questionId] = { ...current, syncedSeq: Math.max(current.syncedSeq, item.clientSeq) };
        }
      }
      return {
        ...state,
        answers,
        expiredAt: action.response.expiredAt,
        clockOffsetMs: new Date(action.response.serverTime).getTime() - action.receivedAt,
        online: true,
      };
    }
    case "online":
      return { ...state, online: action.online };
    case "clock":
      return {
        ...state,
        expiredAt: action.expiredAt,
        clockOffsetMs: new Date(action.serverTime).getTime() - action.receivedAt,
      };
  }
}

function updateAnswer(state: PlayerState, questionId: string, patch: Partial<AnswerDraft>): PlayerState {
  const current = state.answers[questionId];
  if (!current) {
    return state;
  }
  return {
    ...state,
    answers: { ...state.answers, [questionId]: { ...current, ...patch, clientSeq: nextClientSeq() } },
  };
}

export const isUnsaved = (draft: AnswerDraft) => draft.clientSeq > draft.syncedSeq;

export const isAnswered = (draft: AnswerDraft | undefined) =>
  !!draft && (draft.selectedOptions.length > 0 || (draft.answerText ?? "").trim().length > 0);

/** Các câu chưa lưu, dạng body gửi lên API (tối đa 50 mỗi lô). */
export function pendingItems(state: PlayerState, max = 50): SaveAnswerItem[] {
  return state.questionIds
    .map((id) => [id, state.answers[id]!] as const)
    .filter(([, draft]) => isUnsaved(draft))
    .slice(0, max)
    .map(([questionId, draft]) => ({
      questionId,
      clientSeq: draft.clientSeq,
      selectedOptions: draft.selectedOptions,
      answerText: draft.answerText ?? "",
      isMarkedForReview: draft.isMarkedForReview,
    }));
}

/** Thời gian còn lại (ms) theo đồng hồ server: expiredAt − (Date.now() + độ lệch). */
export function remainingMs(state: Pick<PlayerState, "expiredAt" | "clockOffsetMs">, now: number = Date.now()): number {
  return new Date(state.expiredAt).getTime() - (now + state.clockOffsetMs);
}

/** Cùng regex với server (docs/02-nghiep-vu.md mục 3.3); không dùng parseFloat vì parseFloat("3,5") = 3. */
export const NUMBER_PATTERN = /^[+-]?[0-9]{1,20}([.,][0-9]{1,10})?$/;

export function isValidNumberAnswer(text: string | null | undefined): boolean {
  const value = (text ?? "").trim();
  return value.length === 0 || NUMBER_PATTERN.test(value);
}

// ----- Backup sessionStorage (không dùng localStorage vì máy dùng chung — D-18) -----

export interface BackupData {
  answers: Record<string, Omit<AnswerDraft, "syncedSeq">>;
}

const backupKey = (attemptId: string) => `exam_attempt_${attemptId}`;

export function saveBackup(state: PlayerState) {
  try {
    const answers: BackupData["answers"] = {};
    for (const [id, draft] of Object.entries(state.answers)) {
      answers[id] = {
        selectedOptions: draft.selectedOptions,
        answerText: draft.answerText,
        isMarkedForReview: draft.isMarkedForReview,
        clientSeq: draft.clientSeq,
      };
    }
    sessionStorage.setItem(backupKey(state.attemptId), JSON.stringify({ answers } satisfies BackupData));
  } catch {
    // sessionStorage bị chặn hoặc đầy: vẫn làm bài bình thường, chỉ mất lớp dự phòng.
  }
}

export function loadBackup(attemptId: string): BackupData | null {
  try {
    const raw = sessionStorage.getItem(backupKey(attemptId));
    return raw ? (JSON.parse(raw) as BackupData) : null;
  } catch {
    return null;
  }
}

export function clearBackup(attemptId: string) {
  try {
    sessionStorage.removeItem(backupKey(attemptId));
  } catch {
    // bỏ qua
  }
}
