import type { Attempt } from "../../types/api";
import {
  initPlayerState,
  isAnswered,
  isUnsaved,
  isValidNumberAnswer,
  nextClientSeq,
  pendingItems,
  playerReducer,
  remainingMs,
} from "./playerState";

const attempt = (overrides: Partial<Attempt> = {}): Attempt => ({
  attemptId: "a1",
  examId: "e1",
  examName: "Đề thử",
  attemptNumber: 1,
  status: "IN_PROGRESS",
  resumed: false,
  startedAt: "2026-09-29T02:00:00.000Z",
  expiredAt: "2026-09-29T03:00:00.000Z",
  serverTime: "2026-09-29T02:00:00.000Z",
  questions: [
    { id: "q2", order: 2, content: "Câu 2", contentFormat: "PLAIN", type: "FILL_IN", answerDataType: "NUMBER", score: 1, options: [], answer: { selectedOptions: [], answerText: null, isMarkedForReview: false, clientSeq: 0 } },
    { id: "q1", order: 1, content: "Câu 1", contentFormat: "PLAIN", type: "SINGLE_CHOICE", answerDataType: null, score: 1, options: [{ code: "A", content: "A" }, { code: "B", content: "B" }], answer: { selectedOptions: ["B"], answerText: null, isMarkedForReview: true, clientSeq: 5 } },
  ],
  ...overrides,
});

describe("playerState", () => {
  it("clientSeq luôn tăng, kể cả khi đồng hồ máy bị lùi", () => {
    const a = nextClientSeq(1_000);
    const b = nextClientSeq(500);
    const c = nextClientSeq(2_000);
    expect(b).toBeGreaterThan(a);
    expect(c).toBeGreaterThan(b);
  });

  it("khởi tạo theo thứ tự câu và lấy câu trả lời từ server", () => {
    const state = initPlayerState(attempt(), Date.parse("2026-09-29T02:00:00.000Z"));
    expect(state.questionIds).toEqual(["q1", "q2"]);
    expect(state.answers.q1).toMatchObject({ selectedOptions: ["B"], isMarkedForReview: true, clientSeq: 5, syncedSeq: 5 });
    expect(isUnsaved(state.answers.q1!)).toBe(false);
  });

  it("backup trên máy mới hơn server thì được dùng và đánh dấu chưa lưu", () => {
    const backup = { answers: { q1: { selectedOptions: ["A"], answerText: null, isMarkedForReview: false, clientSeq: 9 } } };
    const state = initPlayerState(attempt(), Date.now(), backup);
    expect(state.answers.q1!.selectedOptions).toEqual(["A"]);
    expect(isUnsaved(state.answers.q1!)).toBe(true);
    expect(pendingItems(state)).toEqual([{ questionId: "q1", clientSeq: 9, selectedOptions: ["A"], answerText: "", isMarkedForReview: false }]);
  });

  it("backup cũ hơn server bị bỏ qua", () => {
    const backup = { answers: { q1: { selectedOptions: ["A"], answerText: null, isMarkedForReview: false, clientSeq: 3 } } };
    const state = initPlayerState(attempt(), Date.now(), backup);
    expect(state.answers.q1!.selectedOptions).toEqual(["B"]);
  });

  it("sửa câu trả lời tăng clientSeq; server xác nhận thì hết chưa lưu", () => {
    let state = initPlayerState(attempt(), Date.now());
    state = playerReducer(state, { type: "text", questionId: "q2", text: "3,5" });
    const seq = state.answers.q2!.clientSeq;
    expect(isUnsaved(state.answers.q2!)).toBe(true);
    expect(isAnswered(state.answers.q2)).toBe(true);

    state = playerReducer(state, {
      type: "synced",
      receivedAt: Date.now(),
      response: { serverTime: new Date().toISOString(), expiredAt: "2026-09-29T03:10:00.000Z", answers: [{ questionId: "q2", applied: true, clientSeq: seq }] },
    });
    expect(isUnsaved(state.answers.q2!)).toBe(false);
    expect(state.expiredAt).toBe("2026-09-29T03:10:00.000Z");
  });

  it("server bỏ qua bản cũ (applied=false) vẫn coi như đã đồng bộ tới seq server đang giữ", () => {
    let state = initPlayerState(attempt(), Date.now());
    state = playerReducer(state, { type: "select", questionId: "q1", options: ["A"] });
    const seq = state.answers.q1!.clientSeq;
    state = playerReducer(state, {
      type: "synced",
      receivedAt: Date.now(),
      response: { serverTime: new Date().toISOString(), expiredAt: state.expiredAt, answers: [{ questionId: "q1", applied: false, clientSeq: seq + 10 }] },
    });
    expect(isUnsaved(state.answers.q1!)).toBe(false);
  });

  it("thời gian còn lại tính theo đồng hồ server, không theo đồng hồ máy", () => {
    // Máy chạy nhanh 10 phút so với server
    const clientNow = Date.parse("2026-09-29T02:10:00.000Z");
    const state = initPlayerState(attempt({ serverTime: "2026-09-29T02:00:00.000Z" }), clientNow);
    expect(remainingMs(state, clientNow)).toBe(60 * 60 * 1000);
    expect(remainingMs(state, clientNow + 1000)).toBe(60 * 60 * 1000 - 1000);
  });

  it("goto bị giới hạn trong phạm vi câu hỏi", () => {
    const state = initPlayerState(attempt(), Date.now());
    expect(playerReducer(state, { type: "goto", index: 99 }).currentIndex).toBe(1);
    expect(playerReducer(state, { type: "goto", index: -3 }).currentIndex).toBe(0);
  });

  it.each([
    ["3,5", true],
    ["3.5", true],
    ["-0,25", true],
    ["", true],
    ["1,000.5", false],
    ["1e3", false],
    ["abc", false],
  ])("kiểm tra định dạng số %s → %s", (input, valid) => {
    expect(isValidNumberAnswer(input)).toBe(valid);
  });
});
