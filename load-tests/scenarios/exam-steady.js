// exam-steady: VUS học viên đang làm bài, mỗi người lưu đáp án mỗi 5–15 giây trong DURATION.
// Ngưỡng: p95 lưu đáp án < 300 ms; lỗi < 0.1% (docs/08-kiem-thu.md mục 7).
//
// Phần lớn là lưu một câu (autosave sau khi đổi đáp án); 10% là lô vài câu (gửi lại sau khi mất mạng, D-18);
// 5% kèm một sự kiện đổi tab. Đăng nhập + start rải trong RAMP_SECONDS đầu, không tính vào ngưỡng lưu đáp án.
import { check, sleep } from 'k6';
import { adminSession, Session } from '../lib/api.js';
import { ADMIN_PASSWORD, ADMIN_USER, int, STUDENT_PASSWORD, thresholds, userName, VUS } from '../lib/config.js';
import { countAttempts, createPublishedExam, ensureCohort } from '../lib/seed.js';
import { pick, save, start } from '../lib/student.js';

const DURATION = __ENV.DURATION || '30m';
const RAMP = int('RAMP_SECONDS', 60);
const MIN_THINK = int('MIN_THINK_SECONDS', 5);
const MAX_THINK = int('MAX_THINK_SECONDS', 15);

function durationMinutes(text) {
  const match = /^(\d+)(s|m|h)$/.exec(text);
  if (!match) {
    throw new Error(`DURATION phải có dạng 90s, 30m, 1h; nhận được "${text}"`);
  }
  const value = parseInt(match[1], 10);
  return Math.ceil({ s: value / 60, m: value, h: value * 60 }[match[2]]);
}

export const options = {
  setupTimeout: '20m',
  scenarios: {
    steady: { executor: 'constant-vus', vus: VUS, duration: DURATION, gracefulStop: '30s' },
  },
  thresholds: thresholds(
    { 'http_req_duration{name:save_answers}': ['p(95)<300'] },
    { http_req_failed: ['rate<0.001'], server_errors: ['count==0'], integrity_errors: ['count==0'] },
  ),
};

export function setup() {
  const admin = adminSession(ADMIN_USER, ADMIN_PASSWORD);
  const groupId = ensureCohort(admin, VUS);
  // Đủ dài để không lượt nào hết giờ trong lúc chạy.
  const minutes = durationMinutes(DURATION) + Math.ceil(RAMP / 60) + 15;
  return { examId: createPublishedExam(admin, groupId, 'STEADY', { durationMinutes: minutes }) };
}

// Trạng thái riêng của từng VU (mỗi VU có runtime JS riêng), giữ qua các vòng lặp.
let session = null;
let attempt = null;

function prepare(examId) {
  sleep(Math.random() * RAMP);
  session = new Session(userName(__VU), STUDENT_PASSWORD);
  if (session.login()) {
    attempt = start(session, examId);
  }
}

export default function (ctx) {
  if (!attempt) {
    if (session) {
      sleep(5); // login / start lỗi: không dồn request, thử lại chậm
    }
    prepare(ctx.examId);
    if (!attempt) {
      return;
    }
  }

  const roll = Math.random();
  if (roll < 0.1) {
    const shuffled = attempt.questions.slice().sort(() => Math.random() - 0.5);
    save(session, attempt, shuffled.slice(0, 2 + Math.floor(Math.random() * 4)));
  } else {
    save(session, attempt, [pick(attempt.questions)]);
  }

  if (roll > 0.95) {
    const res = session.call('POST', `/api/student/attempts/${attempt.attemptId}/events`, {
      events: [
        { type: 'VISIBILITY_HIDDEN', clientTime: new Date().toISOString(), detail: null },
        { type: 'VISIBILITY_VISIBLE', clientTime: new Date().toISOString(), detail: null },
      ],
    }, 'events');
    check(res, { 'events: 200': (r) => r.status === 200 });
  }

  sleep(MIN_THINK + Math.random() * (MAX_THINK - MIN_THINK));
}

export function teardown(ctx) {
  const admin = adminSession(ADMIN_USER, ADMIN_PASSWORD);
  console.log(`Đề ${ctx.examId}: ${countAttempts(admin, ctx.examId, 'IN_PROGRESS')} lượt đang làm. `
    + 'Các lượt này sẽ được job tự nộp khi hết giờ; muốn dọn sớm thì đóng đề với forceSubmit.');
}
