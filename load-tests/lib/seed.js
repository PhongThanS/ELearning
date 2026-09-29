// Dựng dữ liệu cho một lần chạy qua API admin (không ghi thẳng vào database).
import { GROUP_CODE, QUESTION_COUNT, STUDENT_PASSWORD, userName } from './config.js';

/**
 * Nhóm GROUP_CODE có đủ `count` học viên <USER_PREFIX>0001…; tạo phần còn thiếu.
 * Dùng lại được giữa các lần chạy: mỗi lần chạy tạo đề mới, nên số lượt không dồn qua các lần.
 */
export function ensureCohort(admin, count) {
  const found = admin.all(`/api/groups?keyword=${encodeURIComponent(GROUP_CODE)}`, 1000)
    .find((g) => g.code.toUpperCase() === GROUP_CODE.toUpperCase());
  const group = found || admin.must('POST', '/api/groups', {
    code: GROUP_CODE,
    name: 'Học viên load test',
    description: 'Tạo bởi load-tests/ (k6). Không dùng cho kỳ thi thật.',
  });

  const members = new Set(admin.all(`/api/users?groupId=${group.id}`, 100000).map((u) => u.userName.toLowerCase()));
  const toAdd = [];
  let created = 0;
  for (let i = 1; i <= count; i++) {
    const name = userName(i);
    if (members.has(name)) {
      continue;
    }
    const res = admin.send('POST', '/api/users', {
      userName: name,
      email: `${name}@loadtest.test`,
      fullName: `Học Viên Tải ${String(i).padStart(4, '0')}`,
      password: STUDENT_PASSWORD,
      groupIds: [group.id],
    });
    if (res.status >= 200 && res.status < 300) {
      created++;
      continue;
    }
    if (res.status === 409) {
      // Đã có từ lần chạy với nhóm khác: chỉ thêm vào nhóm, mật khẩu phải là STUDENT_PASSWORD.
      const existing = admin.must('GET', `/api/users?keyword=${encodeURIComponent(name)}&pageSize=100`).items
        .find((u) => u.userName.toLowerCase() === name);
      if (existing) {
        toAdd.push(existing.id);
        continue;
      }
    }
    throw new Error(`Tạo ${name} thất bại: ${res.status} ${res.body}`);
  }
  for (let i = 0; i < toAdd.length; i += 100) {
    admin.must('POST', `/api/groups/${group.id}/members`, { userIds: toAdd.slice(i, i + 100) });
  }
  console.log(`Nhóm ${GROUP_CODE}: ${count} học viên (tạo mới ${created}, thêm vào nhóm ${toAdd.length}).`);
  return group.id;
}

function questionBody(i, tag) {
  const content = `[LOADTEST ${tag}] Câu ${i + 1}`;
  switch (i % 5) {
    case 0:
      return {
        content: `${content}: ngôn ngữ nào chạy trên .NET?`,
        questionType: 'SINGLE_CHOICE',
        options: [
          { content: 'Python thuần', isCorrect: false },
          { content: 'C#', isCorrect: true },
          { content: 'Ruby thuần', isCorrect: false },
          { content: 'PHP thuần', isCorrect: false },
        ],
      };
    case 1:
      return {
        content: `${content}: những số nào là số chẵn?`,
        questionType: 'MULTIPLE_CHOICE',
        options: [
          { content: 'Hai', isCorrect: true },
          { content: 'Ba', isCorrect: false },
          { content: 'Bốn', isCorrect: true },
          { content: 'Năm', isCorrect: false },
        ],
      };
    case 2:
      return {
        content: `${content}: SQL Server hỗ trợ transaction.`,
        questionType: 'TRUE_FALSE',
        options: [
          { optionCode: 'TRUE', content: 'Đúng', isCorrect: true },
          { optionCode: 'FALSE', content: 'Sai', isCorrect: false },
        ],
      };
    case 3:
      return {
        content: `${content}: thủ đô của Việt Nam?`,
        questionType: 'FILL_IN',
        answerDataType: 'TEXT',
        acceptedAnswers: ['Hà Nội'],
        ignoreAccent: true,
      };
    default:
      return {
        content: `${content}: 7 chia 2 bằng bao nhiêu?`,
        questionType: 'FILL_IN',
        answerDataType: 'NUMBER',
        correctAnswerNumber: 3.5,
      };
  }
}

/**
 * Đề QUESTION_COUNT câu (đủ 4 loại), 1 lượt, gán cho nhóm, đã publish.
 * `endAt` (Date) giới hạn ExpiredAt của mọi lượt (D-05).
 */
export function createPublishedExam(admin, groupId, scenario, { durationMinutes, endAt }) {
  const tag = `${scenario}-${Date.now().toString(36)}`.toUpperCase();
  const questionIds = [];
  for (let i = 0; i < QUESTION_COUNT; i++) {
    questionIds.push(admin.must('POST', '/api/questions', questionBody(i, tag)).id);
  }
  const exam = admin.must('POST', '/api/exams', {
    code: `LT-${tag}`,
    name: `Load test ${tag}`,
    description: 'Tạo bởi load-tests/ (k6).',
    maxAttempts: 1,
    accessMode: 'ASSIGNED',
    endAt: endAt ? endAt.toISOString() : null,
    durationMinutes,
    passPercentage: 50,
    scoreVisibility: 'IMMEDIATE',
    reviewPolicy: 'NEVER',
  });
  const versionUrl = `/api/exams/${exam.id}/versions/${exam.draftVersionId}`;
  admin.must('POST', `${versionUrl}/questions`, { questionIds });
  admin.must('PUT', `/api/exams/${exam.id}/assignments`, { groupIds: [groupId], userIds: [] });
  admin.must('POST', `${versionUrl}/publish`);
  console.log(`Đề LT-${tag} (${exam.id}): ${QUESTION_COUNT} câu, ${durationMinutes} phút, endAt ${endAt ? endAt.toISOString() : '—'}.`);
  return exam.id;
}

/** Đếm lượt thi của đề theo trạng thái (null = mọi trạng thái), qua totalCount của trang đầu. */
export function countAttempts(admin, examId, status) {
  const filter = status ? `&status=${status}` : '';
  return admin.must('GET', `/api/admin/exams/${examId}/attempts?pageSize=1${filter}`).totalCount;
}
