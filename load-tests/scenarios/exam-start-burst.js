// exam-start-burst: VUS học viên đăng nhập rồi bắt đầu thi trong vòng START_WINDOW_SECONDS giây.
// Ngưỡng: p95 start < 1 s; không có lỗi 5xx; mỗi học viên đúng 1 lượt thi (docs/08-kiem-thu.md mục 7).
//
// Mỗi VU còn gọi start lần hai (như F5 / tab thứ hai) và phải nhận lại đúng lượt cũ (D-07).
// DOUBLE_START=1: lần đầu gửi hai start song song để thử cả trường hợp tranh chấp khi tạo lượt.
import { check, sleep } from 'k6';
import http from 'k6/http';
import { batchItem, data, adminSession, Session, trackAll } from '../lib/api.js';
import { ADMIN_PASSWORD, ADMIN_USER, int, STUDENT_PASSWORD, thresholds, userName, VUS } from '../lib/config.js';
import { countAttempts, createPublishedExam, ensureCohort } from '../lib/seed.js';
import { integrityErrors, start } from '../lib/student.js';

const WINDOW = int('START_WINDOW_SECONDS', 60);
const DOUBLE_START = __ENV.DOUBLE_START === '1';

export const options = {
  setupTimeout: '20m',
  teardownTimeout: '5m',
  scenarios: {
    start_burst: { executor: 'per-vu-iterations', vus: VUS, iterations: 1, maxDuration: `${WINDOW + 120}s` },
  },
  thresholds: thresholds(
    { 'http_req_duration{name:start}': ['p(95)<1000'] },
    { server_errors: ['count==0'], integrity_errors: ['count==0'], checks: ['rate>0.999'] },
  ),
};

export function setup() {
  const admin = adminSession(ADMIN_USER, ADMIN_PASSWORD);
  const groupId = ensureCohort(admin, VUS);
  return { examId: createPublishedExam(admin, groupId, 'START', { durationMinutes: 60 }) };
}

function doubleStart(session, examId) {
  const url = `/api/student/exams/${examId}/start`;
  session.ensureFresh();
  const responses = trackAll(http.batch([
    batchItem('POST', url, undefined, session.token, 'start'),
    batchItem('POST', url, undefined, session.token, 'start'),
  ]));
  const ids = responses.map((r) => (r.status === 200 || r.status === 201 ? data(r).attemptId : null));
  const created = responses.filter((r) => r.status === 201).length;
  const ok = check(ids, {
    'start song song: cùng một lượt': (x) => x[0] !== null && x[0] === x[1],
    'start song song: đúng một lượt mới': () => created === 1,
  });
  if (!ok) {
    integrityErrors.add(1);
  }
  return ids[0];
}

export default function (ctx) {
  sleep(Math.random() * WINDOW);
  const session = new Session(userName(__VU), STUDENT_PASSWORD);
  if (!session.login()) {
    return;
  }

  let attemptId;
  if (DOUBLE_START) {
    attemptId = doubleStart(session, ctx.examId);
  } else {
    const first = start(session, ctx.examId);
    attemptId = first && first.attemptId;
  }
  if (!attemptId) {
    return;
  }

  sleep(1 + Math.random() * 2);
  const again = start(session, ctx.examId);
  if (!check(again, { 'start lần hai: trả lại lượt cũ': (a) => a !== null && a.attemptId === attemptId })) {
    integrityErrors.add(1);
  }
}

export function teardown(ctx) {
  const admin = adminSession(ADMIN_USER, ADMIN_PASSWORD);
  const attempts = admin.all(`/api/admin/exams/${ctx.examId}/attempts`, VUS * 2);
  const users = new Set(attempts.map((a) => a.userId));
  const ok = check(attempts, {
    'mỗi học viên đúng 1 lượt': (a) => a.length === users.size,
    'đủ lượt cho mọi học viên': (a) => a.length === VUS,
  });
  if (!ok) {
    integrityErrors.add(1);
  }
  console.log(`Đề ${ctx.examId}: ${attempts.length} lượt / ${users.size} học viên / ${VUS} VU; `
    + `đang làm ${countAttempts(admin, ctx.examId, 'IN_PROGRESS')}.`);
}
