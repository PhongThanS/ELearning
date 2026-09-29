// exam-submit-wave: VUS học viên cùng nộp bài trong WAVE_SECONDS giây (mặc định 2 phút).
// Ngưỡng: p95 submit < 2 s (docs/08-kiem-thu.md mục 7).
//
// Pha chuẩn bị (PREP_SECONDS): đăng nhập, bắt đầu, lưu cả bài trong một lô. Sau đó mọi VU chờ tới cùng một mốc
// rồi nộp rải trong WAVE_SECONDS. Mỗi VU nộp lại lần hai: phải nhận đúng kết quả cũ, không chấm lại.
import { check, sleep } from 'k6';
import { adminSession, Session, sleepUntil } from '../lib/api.js';
import { ADMIN_PASSWORD, ADMIN_USER, int, STUDENT_PASSWORD, thresholds, userName, VUS } from '../lib/config.js';
import { countAttempts, createPublishedExam, ensureCohort } from '../lib/seed.js';
import { integrityErrors, save, start, submit } from '../lib/student.js';

const PREP = int('PREP_SECONDS', 120);
const WAVE = int('WAVE_SECONDS', 120);

export const options = {
  setupTimeout: '20m',
  teardownTimeout: '5m',
  scenarios: {
    submit_wave: { executor: 'per-vu-iterations', vus: VUS, iterations: 1, maxDuration: `${PREP + WAVE + 180}s` },
  },
  thresholds: thresholds(
    { 'http_req_duration{name:submit}': ['p(95)<2000'] },
    { server_errors: ['count==0'], integrity_errors: ['count==0'], checks: ['rate>0.999'] },
  ),
};

export function setup() {
  const admin = adminSession(ADMIN_USER, ADMIN_PASSWORD);
  const groupId = ensureCohort(admin, VUS);
  const examId = createPublishedExam(admin, groupId, 'SUBMIT', { durationMinutes: 60 });
  // Mốc tính sau khi setup xong (setup có thể mất vài phút khi phải tạo học viên).
  return { examId, waveStartsAt: Date.now() + PREP * 1000 };
}

export default function (ctx) {
  sleep(Math.random() * PREP * 0.5);
  const session = new Session(userName(__VU), STUDENT_PASSWORD);
  if (!session.login()) {
    return;
  }
  const attempt = start(session, ctx.examId);
  if (!attempt || !save(session, attempt, attempt.questions)) {
    return;
  }

  sleepUntil(ctx.waveStartsAt + Math.random() * WAVE * 1000);
  const result = submit(session, attempt);
  if (!result) {
    return;
  }

  const again = submit(session, attempt, 'submit_again');
  const same = check(again, {
    'nộp lại: cùng kết quả': (r) => r !== null && r.totalScore === result.totalScore && r.submittedAt === result.submittedAt,
  });
  if (!same) {
    integrityErrors.add(1);
  }
}

export function teardown(ctx) {
  const admin = adminSession(ADMIN_USER, ADMIN_PASSWORD);
  const submitted = countAttempts(admin, ctx.examId, 'SUBMITTED');
  const ok = check(submitted, { 'mọi lượt đã nộp': (n) => n === VUS });
  if (!ok) {
    integrityErrors.add(1);
  }
  console.log(`Đề ${ctx.examId}: ${submitted}/${VUS} lượt SUBMITTED, ${countAttempts(admin, ctx.examId, 'IN_PROGRESS')} còn đang làm.`);
}
