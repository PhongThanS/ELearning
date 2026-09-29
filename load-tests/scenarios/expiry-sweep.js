// expiry-sweep: VUS lượt thi hết giờ cùng lúc, học viên bỏ đi không nộp; AttemptExpirationWorker phải tự nộp hết.
// Ngưỡng: job xử lý hết trong < 5 phút (docs/08-kiem-thu.md mục 7).
//
// Đề có EndAt = sau pha bắt đầu + LEAD_SECONDS, thời lượng 60 phút, nên ExpiredAt = EndAt cho mọi lượt (D-05).
// Thời gian đo = max(SubmittedAt) − EndAt, lấy từ server; gồm cả 30 giây ân hạn và chu kỳ quét 60 giây.
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';
import { adminSession, Session, sleepUntil } from '../lib/api.js';
import { ADMIN_PASSWORD, ADMIN_USER, int, STUDENT_PASSWORD, thresholds, userName, VUS } from '../lib/config.js';
import { countAttempts, createPublishedExam, ensureCohort } from '../lib/seed.js';
import { integrityErrors, save, start } from '../lib/student.js';

const WINDOW = int('START_WINDOW_SECONDS', 60);
const LEAD = int('LEAD_SECONDS', 60);
const POLL = int('POLL_SECONDS', 5);
const SWEEP_TIMEOUT = int('SWEEP_TIMEOUT_SECONDS', 600);

const sweepSeconds = new Trend('expiry_sweep_seconds');

export const options = {
  setupTimeout: '20m',
  scenarios: {
    starters: {
      executor: 'per-vu-iterations', exec: 'starter', vus: VUS, iterations: 1, maxDuration: `${WINDOW + 120}s`,
    },
    observer: {
      executor: 'per-vu-iterations', exec: 'observe', vus: 1, iterations: 1,
      maxDuration: `${WINDOW + LEAD + SWEEP_TIMEOUT + 120}s`,
    },
  },
  thresholds: thresholds(
    { expiry_sweep_seconds: ['max<300'], 'http_req_duration{name:start}': ['p(95)<1000'] },
    { server_errors: ['count==0'], integrity_errors: ['count==0'], checks: ['rate>0.999'] },
  ),
};

export function setup() {
  const admin = adminSession(ADMIN_USER, ADMIN_PASSWORD);
  const groupId = ensureCohort(admin, VUS);
  // Tính mốc sau khi tạo học viên, để pha bắt đầu luôn xong trước EndAt.
  const endAt = new Date(Date.now() + (WINDOW + LEAD + 30) * 1000);
  endAt.setUTCMilliseconds(0);
  const examId = createPublishedExam(admin, groupId, 'EXPIRY', { durationMinutes: 60, endAt });
  return { examId, endAt: endAt.getTime() };
}

export function starter(ctx) {
  sleep(Math.random() * WINDOW);
  const session = new Session(userName(__VU), STUDENT_PASSWORD);
  if (!session.login()) {
    return;
  }
  const attempt = start(session, ctx.examId);
  if (!attempt) {
    return;
  }
  const capped = check(attempt, { 'start: ExpiredAt = EndAt': (a) => Math.abs(Date.parse(a.expiredAt) - ctx.endAt) < 1000 });
  if (!capped) {
    integrityErrors.add(1);
  }
  // Làm vài câu rồi bỏ đi: job phải chấm cả câu đã lưu.
  save(session, attempt, attempt.questions.slice(0, 3));
}

export function observe(ctx) {
  sleepUntil(ctx.endAt);
  const admin = adminSession(ADMIN_USER, ADMIN_PASSWORD);
  const deadline = ctx.endAt + SWEEP_TIMEOUT * 1000;
  let inProgress = countAttempts(admin, ctx.examId, 'IN_PROGRESS');
  while (inProgress > 0 && Date.now() < deadline) {
    sleep(POLL);
    inProgress = countAttempts(admin, ctx.examId, 'IN_PROGRESS');
  }

  const attempts = admin.all(`/api/admin/exams/${ctx.examId}/attempts`, VUS * 2);
  const lastSubmitted = attempts.reduce((max, a) => (a.submittedAt ? Math.max(max, Date.parse(a.submittedAt)) : max), ctx.endAt);
  const seconds = inProgress > 0 ? SWEEP_TIMEOUT : (lastSubmitted - ctx.endAt) / 1000;
  sweepSeconds.add(seconds);

  const ok = check(attempts, {
    'không còn lượt đang làm': () => inProgress === 0,
    'đủ lượt cho mọi học viên': (a) => a.length === VUS,
    'mọi lượt được tự nộp vì hết giờ': (a) => a.every((x) => x.status === 'AUTO_SUBMITTED' && x.submitReason === 'TIME_EXPIRED'),
  });
  if (!ok) {
    integrityErrors.add(1);
  }
  console.log(`Đề ${ctx.examId}: ${attempts.length} lượt, còn đang làm ${inProgress}; `
    + `lượt cuối được nộp sau EndAt ${seconds.toFixed(1)} giây.`);
}
