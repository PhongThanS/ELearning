// Luồng của một học viên ảo: đăng nhập, bắt đầu, lưu đáp án, nộp bài.
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import { data, errorCode } from './api.js';

/** Lượt thi sai: start lần hai trả lượt khác, lưu đáp án không được áp dụng, nộp lại cho kết quả khác… */
export const integrityErrors = new Counter('integrity_errors');

function pick(list) {
  return list[Math.floor(Math.random() * list.length)];
}

/** Câu trả lời ngẫu nhiên hợp lệ cho một câu hỏi của player (không biết đáp án đúng). */
export function randomAnswer(question) {
  const codes = question.options.map((o) => o.code);
  switch (question.type) {
    case 'SINGLE_CHOICE':
    case 'TRUE_FALSE':
      return { selectedOptions: [pick(codes)], answerText: null };
    case 'MULTIPLE_CHOICE': {
      const chosen = codes.filter(() => Math.random() < 0.5);
      return { selectedOptions: chosen.length > 0 ? chosen : [pick(codes)], answerText: null };
    }
    case 'FILL_IN':
      return {
        selectedOptions: null,
        answerText: question.answerDataType === 'NUMBER' ? pick(['3,5', '3.5', '4']) : pick(['Hà Nội', 'ha noi', 'Huế']),
      };
    default:
      return { selectedOptions: null, answerText: 'Trả lời tự luận do load test sinh ra.' };
  }
}

/** Trạng thái lượt thi phía client: clientSeq tăng dần theo từng câu (D-18). */
export class AttemptState {
  constructor(attempt) {
    this.attemptId = attempt.attemptId;
    this.expiredAt = attempt.expiredAt;
    this.questions = attempt.questions;
    this.seq = {};
    attempt.questions.forEach((q) => {
      this.seq[q.id] = q.answer ? q.answer.clientSeq : 0;
    });
  }

  item(question) {
    this.seq[question.id] += 1;
    return Object.assign({ questionId: question.id, clientSeq: this.seq[question.id], isMarkedForReview: Math.random() < 0.1 },
      randomAnswer(question));
  }
}

/** Bắt đầu thi; 201 = lượt mới, 200 + resumed = lượt đang làm (D-07). Trả về AttemptState hoặc null. */
export function start(session, examId) {
  const res = session.call('POST', `/api/student/exams/${examId}/start`, undefined, 'start');
  const ok = check(res, { 'start: 201 hoặc 200': (r) => r.status === 201 || r.status === 200 });
  if (!ok) {
    console.warn(`start ${session.userName}: ${res.status} ${errorCode(res)}`);
    return null;
  }
  return new AttemptState(data(res));
}

/** Lưu một lô câu trả lời; mọi câu phải được áp dụng vì clientSeq luôn tăng. */
export function save(session, state, questions) {
  const res = session.call('PUT', `/api/student/attempts/${state.attemptId}/answers`,
    { answers: questions.map((q) => state.item(q)) }, 'save_answers');
  const ok = check(res, { 'save: 200': (r) => r.status === 200 });
  if (ok) {
    const saved = data(res).answers;
    if (!check(saved, { 'save: mọi câu được áp dụng': (a) => a.length === questions.length && a.every((x) => x.applied) })) {
      integrityErrors.add(1);
    }
  }
  return ok;
}

/** Nộp bài; trả về kết quả (đã áp chính sách hiển thị) hoặc null. */
export function submit(session, state, name) {
  const res = session.call('POST', `/api/student/attempts/${state.attemptId}/submit`, undefined, name || 'submit');
  const ok = check(res, {
    'submit: 200': (r) => r.status === 200,
    'submit: đã nộp': (r) => r.status === 200 && data(r).status === 'SUBMITTED',
  });
  return ok ? data(res) : null;
}

export { pick };
