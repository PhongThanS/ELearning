#!/usr/bin/env node
// Tạo bộ dữ liệu mẫu để demo: danh mục, câu hỏi (đủ 5 loại), học viên, nhóm, lớp học (nhiều-nhiều),
// đề thi ở nhiều trạng thái, lượt thi đã nộp và câu tự luận chờ chấm.
//
// Mọi thao tác đi qua API (không ghi thẳng database) nên dữ liệu luôn đúng quy tắc nghiệp vụ.
// Chạy lại được: thứ gì đã có (theo mã / tên đăng nhập) thì dùng lại; lượt thi chỉ tạo cho đề mới tạo.
//
//   ADMIN_PASSWORD='...' node deploy/scripts/demo-data.mjs
//
// Biến môi trường:
//   BASE_URL               mặc định http://localhost:8080
//   ADMIN_USER             mặc định admin
//   ADMIN_PASSWORD         bắt buộc
//   DEMO_STUDENT_PASSWORD  mật khẩu chung của các học viên demo (mặc định DEMO_STUDENT_PASSWORD_DEFAULT bên dưới)
//
// Chỉ dùng cho máy dev / demo. Không chạy trên Production.

const BASE_URL = (process.env.BASE_URL ?? "http://localhost:8080").replace(/\/$/, "");
const ADMIN_USER = process.env.ADMIN_USER ?? "admin";
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD;
const DEMO_STUDENT_PASSWORD_DEFAULT = "Demo@2026";
const STUDENT_PASSWORD = process.env.DEMO_STUDENT_PASSWORD ?? DEMO_STUDENT_PASSWORD_DEFAULT;

if (!ADMIN_PASSWORD) {
  console.error("Thiếu ADMIN_PASSWORD. Ví dụ: ADMIN_PASSWORD='...' node deploy/scripts/demo-data.mjs");
  process.exit(1);
}

// ---------------------------------------------------------------- HTTP

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function call(token, method, path, body) {
  for (let attempt = 0; ; attempt++) {
    const res = await fetch(`${BASE_URL}/api${path}`, {
      method,
      headers: {
        "Content-Type": "application/json",
        "X-Requested-With": "XMLHttpRequest",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (res.status === 429 && attempt < 10) {
      const wait = Number(res.headers.get("retry-after") ?? 0) * 1000 || 5000 * (attempt + 1);
      process.stdout.write(`  (bị giới hạn tần suất, chờ ${Math.round(wait / 1000)}s)\n`);
      await sleep(wait);
      continue;
    }
    const text = await res.text();
    const json = text ? JSON.parse(text) : { success: res.ok, data: null, errors: [] };
    return { status: res.status, ...json };
  }
}

async function must(token, method, path, body) {
  const r = await call(token, method, path, body);
  if (r.status >= 400) {
    const errs = (r.errors ?? []).map((e) => `${e.code}${e.field ? `(${e.field})` : ""}: ${e.message}`).join("; ");
    throw new Error(`${method} ${path} → ${r.status} ${errs}`);
  }
  return r.data;
}

async function login(userName, password) {
  const data = await must(null, "POST", "/auth/login", { userName, password });
  return data.accessToken;
}

// ---------------------------------------------------------------- Ngẫu nhiên cố định (chạy lại cho cùng kết quả)

let seed = 20261008;
function rand() {
  seed = (seed * 1664525 + 1013904223) >>> 0;
  return seed / 2 ** 32;
}
const pick = (arr) => arr[Math.floor(rand() * arr.length)];

// ---------------------------------------------------------------- Dữ liệu

const CATEGORIES = [
  ["TOAN", "Toán học"],
  ["TIN", "Tin học"],
  ["ANH", "Tiếng Anh"],
  ["LY", "Vật lý"],
  ["SU", "Lịch sử"],
];

// type: S = chọn một, M = chọn nhiều, TF = đúng/sai, T = điền chữ, N = điền số, E = tự luận
// opts: [nội dung, đúng?]
const QUESTIONS = [
  // ----- Toán -----
  { code: "DEMO-TOAN-01", cat: "TOAN", d: "EASY", type: "S", tags: ["dai-so"], content: "Nghiệm của phương trình $2x + 6 = 0$ là:", opts: [["x = 3", false], ["x = -3", true], ["x = 6", false], ["x = -6", false]], exp: "2x = -6 nên x = -3." },
  { code: "DEMO-TOAN-02", cat: "TOAN", d: "EASY", type: "N", tags: ["so-hoc"], content: "Tính giá trị: 15% của 240 bằng bao nhiêu?", num: 36 },
  { code: "DEMO-TOAN-03", cat: "TOAN", d: "MEDIUM", type: "M", tags: ["so-hoc"], content: "Những số nào sau đây là **số nguyên tố**?", opts: [["2", true], ["9", false], ["11", true], ["15", false], ["17", true]], partial: true },
  { code: "DEMO-TOAN-04", cat: "TOAN", d: "MEDIUM", type: "TF", tags: ["hinh-hoc"], content: "Tổng ba góc trong một tam giác bằng 180°.", tf: true },
  { code: "DEMO-TOAN-05", cat: "TOAN", d: "MEDIUM", type: "S", tags: ["ham-so"], content: "Đồ thị hàm số $y = x^2 - 4x + 3$ có đỉnh là:", opts: [["(2; -1)", true], ["(-2; 15)", false], ["(1; 0)", false], ["(4; 3)", false]] },
  { code: "DEMO-TOAN-06", cat: "TOAN", d: "HARD", type: "N", tags: ["dai-so"], content: "Tổng các nghiệm của phương trình $x^2 - 5x + 6 = 0$ bằng bao nhiêu?", num: 5 },
  { code: "DEMO-TOAN-07", cat: "TOAN", d: "EASY", type: "TF", tags: ["so-hoc"], content: "Số 0 là số tự nhiên chẵn.", tf: true },
  { code: "DEMO-TOAN-08", cat: "TOAN", d: "HARD", type: "S", tags: ["hinh-hoc"], content: "Diện tích hình tròn bán kính 3 cm (lấy π ≈ 3,14) là:", opts: [["18,84 cm²", false], ["28,26 cm²", true], ["9,42 cm²", false], ["113,04 cm²", false]] },
  { code: "DEMO-TOAN-09", cat: "TOAN", d: "MEDIUM", type: "N", tags: ["hinh-hoc"], content: "Cạnh huyền của tam giác vuông có hai cạnh góc vuông 6 và 8 dài bao nhiêu?", num: 10 },
  { code: "DEMO-TOAN-10", cat: "TOAN", d: "MEDIUM", type: "M", tags: ["dai-so"], content: "Những biểu thức nào bằng $(a + b)^2$?", opts: [["a² + 2ab + b²", true], ["a² + b²", false], ["(a + b)(a + b)", true], ["a² - 2ab + b²", false]], partial: true },
  { code: "DEMO-TOAN-11", cat: "TOAN", d: "HARD", type: "E", tags: ["chung-minh"], content: "Chứng minh rằng tổng của hai số lẻ bất kỳ luôn là một số chẵn. Trình bày ngắn gọn.", score: 3, exp: "Gọi hai số lẻ là 2m+1 và 2n+1; tổng = 2(m+n+1) chia hết cho 2." },

  // ----- Tin học -----
  { code: "DEMO-TIN-01", cat: "TIN", d: "EASY", type: "S", tags: ["co-ban"], content: "Đơn vị nhỏ nhất để biểu diễn thông tin trong máy tính là:", opts: [["Byte", false], ["Bit", true], ["KB", false], ["Word", false]] },
  { code: "DEMO-TIN-02", cat: "TIN", d: "MEDIUM", type: "S", tags: ["lap-trinh", "python"], md: true, content: "Đoạn chương trình Python sau in ra gì?\n\n```python\ns = 0\nfor i in range(1, 5):\n    s += i\nprint(s)\n```", opts: [["10", true], ["15", false], ["4", false], ["0", false]], exp: "range(1, 5) gồm 1, 2, 3, 4; tổng = 10." },
  { code: "DEMO-TIN-03", cat: "TIN", d: "EASY", type: "TF", tags: ["mang"], content: "HTTPS mã hóa dữ liệu truyền giữa trình duyệt và máy chủ.", tf: true },
  { code: "DEMO-TIN-04", cat: "TIN", d: "MEDIUM", type: "M", tags: ["co-ban"], content: "Những thiết bị nào là **thiết bị vào** (input)?", opts: [["Bàn phím", true], ["Màn hình", false], ["Chuột", true], ["Máy in", false], ["Máy quét", true]], partial: true },
  { code: "DEMO-TIN-05", cat: "TIN", d: "MEDIUM", type: "T", tags: ["co-so-du-lieu"], content: "Câu lệnh SQL dùng để lấy dữ liệu từ bảng bắt đầu bằng từ khóa nào?", accepted: ["SELECT"] },
  { code: "DEMO-TIN-06", cat: "TIN", d: "HARD", type: "N", tags: ["he-dem"], content: "Số nhị phân 101101 bằng bao nhiêu trong hệ thập phân?", num: 45 },
  { code: "DEMO-TIN-07", cat: "TIN", d: "EASY", type: "S", tags: ["mang"], content: "Viết tắt của địa chỉ trang web là:", opts: [["URL", true], ["USB", false], ["CPU", false], ["RAM", false]] },
  { code: "DEMO-TIN-08", cat: "TIN", d: "MEDIUM", type: "TF", tags: ["lap-trinh"], content: "Trong Python, danh sách (list) có chỉ số bắt đầu từ 1.", tf: false },
  { code: "DEMO-TIN-09", cat: "TIN", d: "HARD", type: "S", tags: ["thuat-toan"], content: "Độ phức tạp thời gian của tìm kiếm nhị phân trên mảng đã sắp xếp n phần tử là:", opts: [["O(n)", false], ["O(log n)", true], ["O(n²)", false], ["O(1)", false]] },
  { code: "DEMO-TIN-10", cat: "TIN", d: "MEDIUM", type: "T", tags: ["co-ban"], content: "Hệ điều hành mã nguồn mở nổi tiếng có biểu tượng chú chim cánh cụt tên là gì?", accepted: ["Linux"] },

  // ----- Tiếng Anh -----
  { code: "DEMO-ANH-01", cat: "ANH", d: "EASY", type: "S", tags: ["grammar"], content: "She ___ to school every day.", opts: [["go", false], ["goes", true], ["going", false], ["gone", false]] },
  { code: "DEMO-ANH-02", cat: "ANH", d: "MEDIUM", type: "S", tags: ["grammar"], content: "If I ___ you, I would study harder.", opts: [["am", false], ["was", false], ["were", true], ["be", false]] },
  { code: "DEMO-ANH-03", cat: "ANH", d: "EASY", type: "T", tags: ["vocabulary"], content: "Từ tiếng Anh của \"thư viện\" là gì?", accepted: ["library"] },
  { code: "DEMO-ANH-04", cat: "ANH", d: "MEDIUM", type: "M", tags: ["vocabulary"], content: "Chọn các từ **đồng nghĩa** với \"happy\":", opts: [["glad", true], ["sad", false], ["joyful", true], ["angry", false], ["cheerful", true]], partial: true },
  { code: "DEMO-ANH-05", cat: "ANH", d: "EASY", type: "TF", tags: ["grammar"], content: "\"Children\" là dạng số nhiều của \"child\".", tf: true },
  { code: "DEMO-ANH-06", cat: "ANH", d: "HARD", type: "S", tags: ["grammar"], content: "By the time we arrived, the film ___.", opts: [["already started", false], ["has already started", false], ["had already started", true], ["was already start", false]] },
  { code: "DEMO-ANH-07", cat: "ANH", d: "MEDIUM", type: "E", tags: ["writing"], content: "Write 3–5 sentences about your favourite hobby.", score: 4, exp: "Chấm theo: đúng chủ đề, ngữ pháp, từ vựng, mạch lạc." },
  { code: "DEMO-ANH-08", cat: "ANH", d: "MEDIUM", type: "T", tags: ["grammar"], content: "Điền dạng quá khứ của động từ \"write\":", accepted: ["wrote"] },

  // ----- Vật lý -----
  { code: "DEMO-LY-01", cat: "LY", d: "EASY", type: "S", tags: ["co-hoc"], content: "Đơn vị đo lực trong hệ SI là:", opts: [["Joule", false], ["Newton", true], ["Watt", false], ["Pascal", false]] },
  { code: "DEMO-LY-02", cat: "LY", d: "MEDIUM", type: "N", tags: ["co-hoc"], content: "Một xe đi 120 km trong 2 giờ. Vận tốc trung bình là bao nhiêu km/h?", num: 60 },
  { code: "DEMO-LY-03", cat: "LY", d: "EASY", type: "TF", tags: ["quang-hoc"], content: "Ánh sáng truyền trong chân không với tốc độ khoảng 300 000 km/s.", tf: true },
  { code: "DEMO-LY-04", cat: "LY", d: "MEDIUM", type: "M", tags: ["dien-hoc"], content: "Những vật liệu nào **dẫn điện** tốt?", opts: [["Đồng", true], ["Nhựa", false], ["Nhôm", true], ["Gỗ khô", false], ["Bạc", true]], partial: true },
  { code: "DEMO-LY-05", cat: "LY", d: "HARD", type: "N", tags: ["dien-hoc"], content: "Điện trở 10 Ω mắc vào hiệu điện thế 220 V. Cường độ dòng điện là bao nhiêu ampe?", num: 22 },
  { code: "DEMO-LY-06", cat: "LY", d: "MEDIUM", type: "S", tags: ["nhiet-hoc"], content: "Nước sôi ở bao nhiêu độ C (áp suất tiêu chuẩn)?", opts: [["90 °C", false], ["100 °C", true], ["110 °C", false], ["120 °C", false]] },

  // ----- Lịch sử -----
  { code: "DEMO-SU-01", cat: "SU", d: "EASY", type: "N", tags: ["viet-nam"], content: "Chủ tịch Hồ Chí Minh đọc Tuyên ngôn Độc lập vào năm nào?", num: 1945 },
  { code: "DEMO-SU-02", cat: "SU", d: "MEDIUM", type: "S", tags: ["viet-nam"], content: "Chiến thắng Điện Biên Phủ diễn ra vào năm:", opts: [["1945", false], ["1954", true], ["1968", false], ["1975", false]] },
  { code: "DEMO-SU-03", cat: "SU", d: "EASY", type: "TF", tags: ["the-gioi"], content: "Chiến tranh thế giới thứ hai kết thúc năm 1945.", tf: true },
  { code: "DEMO-SU-04", cat: "SU", d: "MEDIUM", type: "T", tags: ["viet-nam"], content: "Vị vua nào dời đô từ Hoa Lư về Thăng Long năm 1010?", accepted: ["Lý Thái Tổ", "Lý Công Uẩn"], ignoreAccent: true },
  { code: "DEMO-SU-05", cat: "SU", d: "HARD", type: "M", tags: ["viet-nam"], content: "Những trận đánh nào do Trần Hưng Đạo chỉ huy?", opts: [["Bạch Đằng 1288", true], ["Chi Lăng", false], ["Vạn Kiếp", true], ["Ngọc Hồi – Đống Đa", false]], partial: true },
  { code: "DEMO-SU-06", cat: "SU", d: "MEDIUM", type: "S", tags: ["the-gioi"], content: "Cách mạng tháng Mười Nga diễn ra năm:", opts: [["1905", false], ["1917", true], ["1921", false], ["1939", false]] },
];

const STUDENT_NAMES = [
  "Nguyễn Minh Anh", "Trần Gia Bảo", "Lê Khánh Chi", "Phạm Đức Duy", "Hoàng Thu Hà", "Vũ Quang Huy",
  "Đặng Ngọc Lan", "Bùi Tuấn Kiệt", "Đỗ Phương Linh", "Hồ Nhật Minh", "Ngô Bảo Ngọc", "Dương Thành Nam",
  "Lý Hải Yến", "Phan Anh Khoa", "Trịnh Mai Phương", "Mai Đức Thắng", "Đinh Thảo Vy", "Tô Gia Hưng",
  "Lương Khánh Linh", "Châu Minh Quân", "Kiều Thanh Trúc", "Tạ Quốc Việt", "Âu Diệu Linh", "Quách Hoàng Long",
];
const studentUserName = (i) => `demo.hs${String(i).padStart(2, "0")}`;
// Năng lực mỗi học viên (xác suất trả lời đúng) để điểm phân tán tự nhiên
const SKILL = STUDENT_NAMES.map((_, i) => 0.35 + ((i * 37) % 60) / 100);

const CLASSES = [
  { code: "10A1", name: "Lớp 10A1", year: "2026-2027", start: "2026-09-05", end: "2027-05-31", students: range(1, 10), desc: "Lớp chuyên Toán – GVCN cô Lan." },
  { code: "10A2", name: "Lớp 10A2", year: "2026-2027", start: "2026-09-05", end: "2027-05-31", students: range(11, 20), desc: "Lớp cơ bản khối A." },
  { code: "11B1", name: "Lớp 11B1", year: "2026-2027", start: "2026-09-05", end: "2027-05-31", students: range(15, 24), desc: "Học viên 15–20 đồng thời học 10A2 (ví dụ quan hệ nhiều-nhiều)." },
  { code: "ANH-NC", name: "Tiếng Anh nâng cao", year: "2026-2027", start: "2026-10-01", end: "2026-12-31", students: [1, 4, 7, 12, 16, 19, 22], desc: "Lớp bồi dưỡng buổi chiều, học viên từ nhiều lớp chính khóa." },
  { code: "ONTHI-HK1", name: "Ôn thi học kỳ 1 (đã kết thúc)", year: "2025-2026", start: "2025-11-01", end: "2025-12-20", students: [2, 3, 5, 8], desc: "Lớp cũ, đã tắt.", inactive: true },
];

const GROUPS = [
  { code: "DEMO-CLB-TIN", name: "CLB Tin học", desc: "Câu lạc bộ lập trình", students: [3, 6, 9, 13, 17, 21] },
];

function range(a, b) {
  return Array.from({ length: b - a + 1 }, (_, i) => a + i);
}

const days = (n) => new Date(Date.now() + n * 86400000).toISOString();

const EXAMS = [
  {
    code: "DEMO-TOAN-GK1", name: "Kiểm tra giữa kỳ I – Toán 10", questions: ["DEMO-TOAN-01", "DEMO-TOAN-02", "DEMO-TOAN-03", "DEMO-TOAN-04", "DEMO-TOAN-05", "DEMO-TOAN-06", "DEMO-TOAN-07", "DEMO-TOAN-08", "DEMO-TOAN-09", "DEMO-TOAN-10"],
    durationMinutes: 45, maxAttempts: 1, passPercentage: 50, scoreVisibility: "IMMEDIATE", reviewPolicy: "AFTER_SUBMIT",
    classes: ["10A1", "10A2"], takers: [...range(1, 8), ...range(11, 17)], inProgress: [9],
    instructions: "- Thời gian **45 phút**, không dùng tài liệu.\n- Bài được lưu tự động; hết giờ hệ thống tự nộp.",
  },
  {
    code: "DEMO-TIN-15P", name: "Tin học – kiểm tra 15 phút", questions: ["DEMO-TIN-01", "DEMO-TIN-02", "DEMO-TIN-03", "DEMO-TIN-04", "DEMO-TIN-05", "DEMO-TIN-06", "DEMO-TIN-07", "DEMO-TIN-08"],
    durationMinutes: 15, maxAttempts: 2, passPercentage: 60, scoreVisibility: "IMMEDIATE", reviewPolicy: "AFTER_LAST_ATTEMPT",
    shuffleQuestions: true, shuffleOptions: true, classes: ["11B1"], groups: ["DEMO-CLB-TIN"], takers: [15, 16, 17, 18, 19, 3, 6, 9], retakers: [16, 3],
  },
  {
    code: "DEMO-ANH-U3", name: "Tiếng Anh – Unit 3 (có tự luận)", questions: ["DEMO-ANH-01", "DEMO-ANH-02", "DEMO-ANH-03", "DEMO-ANH-04", "DEMO-ANH-05", "DEMO-ANH-06", "DEMO-ANH-07", "DEMO-ANH-08"],
    durationMinutes: 30, maxAttempts: 1, passPercentage: 50, scoreVisibility: "IMMEDIATE", reviewPolicy: "NEVER",
    classes: ["ANH-NC"], takers: [1, 4, 7, 12, 16, 19], gradeEssays: 3,
    instructions: "Câu viết (tự luận) sẽ được giáo viên chấm sau; điểm tổng cập nhật khi chấm xong.",
  },
  {
    code: "DEMO-SU-ONTAP", name: "Ôn tập Lịch sử (mở cho mọi học viên)", questions: ["DEMO-SU-01", "DEMO-SU-02", "DEMO-SU-03", "DEMO-SU-04", "DEMO-SU-05", "DEMO-SU-06"],
    durationMinutes: 20, maxAttempts: 3, passPercentage: 50, scoreVisibility: "IMMEDIATE", reviewPolicy: "AFTER_LAST_ATTEMPT",
    accessMode: "PUBLIC", takers: [2, 5, 10, 14, 20, 23, 24], retakers: [5, 14],
  },
  {
    code: "DEMO-TOAN-KT1", name: "Kiểm tra 1 tiết – Toán (đã đóng)", questions: ["DEMO-TOAN-01", "DEMO-TOAN-04", "DEMO-TOAN-06", "DEMO-TOAN-07", "DEMO-TOAN-09", "DEMO-TOAN-11"],
    durationMinutes: 45, maxAttempts: 1, passPercentage: 50, scoreVisibility: "IMMEDIATE", reviewPolicy: "AFTER_SUBMIT",
    classes: ["10A1"], takers: range(1, 10), gradeEssays: 10, close: true,
  },
  {
    code: "DEMO-LY-CK1", name: "Thi cuối kỳ I – Vật lý (sắp diễn ra)", questions: ["DEMO-LY-01", "DEMO-LY-02", "DEMO-LY-03", "DEMO-LY-04", "DEMO-LY-05", "DEMO-LY-06"],
    durationMinutes: 60, maxAttempts: 1, passPercentage: 50, scoreVisibility: "AFTER_EXAM_END", reviewPolicy: "AFTER_EXAM_END",
    startAt: days(5), endAt: days(6), classes: ["10A1", "10A2", "11B1"], takers: [],
  },
  {
    code: "DEMO-TONGHOP", name: "Đề tổng hợp (bản nháp)", questions: ["DEMO-TIN-09", "DEMO-TIN-10", "DEMO-LY-06", "DEMO-SU-06", "DEMO-ANH-06"],
    durationMinutes: 30, maxAttempts: 1, scoreVisibility: "IMMEDIATE", reviewPolicy: "NEVER", draft: true, takers: [],
  },
];

// ---------------------------------------------------------------- Tạo

const TYPE = { S: "SINGLE_CHOICE", M: "MULTIPLE_CHOICE", TF: "TRUE_FALSE", T: "FILL_IN", N: "FILL_IN", E: "ESSAY" };
const letters = "ABCDEFGHIJ";

function questionBody(q, categoryId) {
  const body = {
    code: q.code,
    categoryId,
    content: q.content,
    contentFormat: q.md || q.content.includes("**") || q.content.includes("$") ? "MARKDOWN" : "PLAIN",
    questionType: TYPE[q.type],
    defaultScore: q.score ?? 1,
    difficulty: q.d,
    tags: q.tags,
    explanation: q.exp ?? null,
  };
  if (q.type === "S" || q.type === "M") {
    body.options = q.opts.map(([content, isCorrect], i) => ({ optionCode: letters[i], content, isCorrect }));
    body.partialScoring = !!q.partial;
  } else if (q.type === "TF") {
    body.options = [
      { optionCode: "TRUE", content: "Đúng", isCorrect: q.tf },
      { optionCode: "FALSE", content: "Sai", isCorrect: !q.tf },
    ];
  } else if (q.type === "T") {
    body.answerDataType = "TEXT";
    body.acceptedAnswers = q.accepted;
    body.ignoreAccent = !!q.ignoreAccent;
  } else if (q.type === "N") {
    body.answerDataType = "NUMBER";
    body.correctAnswerNumber = q.num;
  }
  return body;
}

/** Câu trả lời của học viên có năng lực `skill` (đúng với xác suất skill). */
function answerFor(q, skill) {
  const right = rand() < skill;
  switch (q.type) {
    case "S": {
      const correct = q.opts.findIndex(([, c]) => c);
      const wrong = q.opts.map((_, i) => i).filter((i) => i !== correct);
      return { selectedOptions: [letters[right ? correct : pick(wrong)]] };
    }
    case "M": {
      const correct = q.opts.map(([, c], i) => (c ? letters[i] : null)).filter(Boolean);
      if (right) return { selectedOptions: correct };
      // Sai một phần: bỏ một đáp án đúng hoặc thêm một đáp án sai
      const wrong = q.opts.map(([, c], i) => (c ? null : letters[i])).filter(Boolean);
      return { selectedOptions: rand() < 0.5 ? correct.slice(1) : [...correct, pick(wrong)] };
    }
    case "TF":
      return { selectedOptions: [right === q.tf ? "TRUE" : "FALSE"] };
    case "T":
      return { answerText: right ? q.accepted[0] : pick(["không biết", "abc", "?"]) };
    case "N":
      return { answerText: String(right ? q.num : q.num + pick([1, -1, 2, 10])) };
    case "E":
      return {
        answerText: pick([
          "My favourite hobby is reading books. I read every evening before bed. Books help me learn new words and relax.",
          "I like playing football with my friends at weekends. It keeps me healthy and I enjoy working in a team.",
          "My hobby is drawing. I usually draw animals and landscapes. It makes me feel calm and creative.",
          "Gọi hai số lẻ là 2m+1 và 2n+1. Tổng bằng 2m+2n+2 = 2(m+n+1) nên luôn chia hết cho 2, là số chẵn.",
        ]),
      };
  }
  return {};
}

async function findByCode(token, path, code, field = "code") {
  const page = await must(token, "GET", `${path}${path.includes("?") ? "&" : "?"}keyword=${encodeURIComponent(code)}&pageSize=100`);
  return page.items.find((x) => String(x[field]).toLowerCase() === code.toLowerCase()) ?? null;
}

async function main() {
  console.log(`Máy chủ: ${BASE_URL}`);
  const admin = await login(ADMIN_USER, ADMIN_PASSWORD);

  // Danh mục
  const categoryIds = {};
  for (const [code, name] of CATEGORIES) {
    const found = await findByCode(admin, "/question-categories", code);
    categoryIds[code] = (found ?? (await must(admin, "POST", "/question-categories", { code, name }))).id;
  }
  console.log(`✔ ${CATEGORIES.length} danh mục`);

  // Câu hỏi
  const questionIds = {};
  let newQuestions = 0;
  for (const q of QUESTIONS) {
    const found = await findByCode(admin, "/questions", q.code);
    if (found) {
      questionIds[q.code] = found.id;
      continue;
    }
    questionIds[q.code] = (await must(admin, "POST", "/questions", questionBody(q, categoryIds[q.cat]))).id;
    newQuestions++;
  }
  console.log(`✔ ${QUESTIONS.length} câu hỏi (mới ${newQuestions})`);

  // Học viên
  const userIds = {};
  let newUsers = 0;
  for (let i = 1; i <= STUDENT_NAMES.length; i++) {
    const userName = studentUserName(i);
    const found = await findByCode(admin, "/users", userName, "userName");
    if (found) {
      userIds[i] = found.id;
      continue;
    }
    const created = await must(admin, "POST", "/users", {
      userName,
      email: `${userName}@demo.local`,
      fullName: STUDENT_NAMES[i - 1],
      password: STUDENT_PASSWORD,
    });
    userIds[i] = created.user.id;
    newUsers++;
  }
  console.log(`✔ ${STUDENT_NAMES.length} học viên demo.hs01…demo.hs${STUDENT_NAMES.length} (mới ${newUsers})`);

  // Nhóm
  const groupIds = {};
  for (const g of GROUPS) {
    const found = await findByCode(admin, "/groups", g.code);
    const group = found ?? (await must(admin, "POST", "/groups", { code: g.code, name: g.name, description: g.desc }));
    groupIds[g.code] = group.id;
    await must(admin, "POST", `/groups/${group.id}/members`, { userIds: g.students.map((i) => userIds[i]) });
  }
  console.log(`✔ ${GROUPS.length} nhóm`);

  // Lớp học (học viên ↔ lớp nhiều-nhiều)
  const classIds = {};
  for (const c of CLASSES) {
    const found = await findByCode(admin, "/classes", c.code);
    const cls = found ?? (await must(admin, "POST", "/classes", {
      code: c.code, name: c.name, schoolYear: c.year, startDate: c.start, endDate: c.end, description: c.desc,
    }));
    classIds[c.code] = cls.id;
    await must(admin, "POST", `/classes/${cls.id}/students`, { userIds: c.students.map((i) => userIds[i]) });
    if (c.inactive) {
      await must(admin, "PATCH", `/classes/${cls.id}/status`, { isActive: false });
    }
  }
  console.log(`✔ ${CLASSES.length} lớp học`);

  // Đề thi + lượt thi
  const byCode = Object.fromEntries(QUESTIONS.map((q) => [q.code, q]));
  const byContent = new Map(QUESTIONS.map((q) => [q.content.trim(), q]));
  const tokens = {};
  const studentToken = async (i) => (tokens[i] ??= await login(studentUserName(i), STUDENT_PASSWORD));

  for (const e of EXAMS) {
    if (await findByCode(admin, "/exams", e.code)) {
      console.log(`• Đề ${e.code} đã có, bỏ qua`);
      continue;
    }
    const exam = await must(admin, "POST", "/exams", {
      code: e.code,
      name: e.name,
      description: `Đề mẫu để demo (${e.questions.length} câu).`,
      instructions: e.instructions ?? null,
      startAt: e.startAt ?? null,
      endAt: e.endAt ?? null,
      maxAttempts: e.maxAttempts,
      accessMode: e.accessMode ?? "ASSIGNED",
      durationMinutes: e.durationMinutes,
      passPercentage: e.passPercentage ?? null,
      scoreVisibility: e.scoreVisibility,
      reviewPolicy: e.reviewPolicy,
      shuffleQuestions: !!e.shuffleQuestions,
      shuffleOptions: !!e.shuffleOptions,
    });
    const versionUrl = `/exams/${exam.id}/versions/${exam.draftVersionId}`;
    await must(admin, "POST", `${versionUrl}/questions`, { questionIds: e.questions.map((c) => questionIds[c]) });
    if ((e.accessMode ?? "ASSIGNED") === "ASSIGNED" && (e.classes || e.groups)) {
      await must(admin, "PUT", `/exams/${exam.id}/assignments`, {
        groupIds: (e.groups ?? []).map((c) => groupIds[c]),
        userIds: [],
        classroomIds: (e.classes ?? []).map((c) => classIds[c]),
      });
    }
    if (e.draft) {
      console.log(`✔ Đề ${e.code}: bản nháp`);
      continue;
    }
    await must(admin, "POST", `${versionUrl}/publish`);

    // Học viên làm bài
    let submitted = 0;
    const takeOnce = async (i, submit = true) => {
      const token = await studentToken(i);
      const attempt = await must(token, "POST", `/student/exams/${exam.id}/start`);
      const answers = attempt.questions.map((aq, n) => {
        const q = byContent.get(aq.content.trim());
        const a = q ? answerFor(q, SKILL[i - 1]) : {};
        return {
          questionId: aq.id,
          clientSeq: n + 1,
          selectedOptions: a.selectedOptions ?? null,
          answerText: a.answerText ?? null,
          isMarkedForReview: false,
        };
      });
      await must(token, "PUT", `/student/attempts/${attempt.attemptId}/answers`, { answers });
      if (submit) {
        await must(token, "POST", `/student/attempts/${attempt.attemptId}/submit`);
        submitted++;
      }
    };
    for (const i of e.takers) await takeOnce(i);
    for (const i of e.retakers ?? []) await takeOnce(i);
    for (const i of e.inProgress ?? []) await takeOnce(i, false);

    // Chấm tay một phần câu tự luận
    let graded = 0;
    if (e.gradeEssays) {
      const queue = await must(admin, "GET", `/admin/exams/${exam.id}/manual-grading?status=PENDING&pageSize=100`);
      for (const item of queue.items.slice(0, e.gradeEssays)) {
        const max = byCode[e.questions.find((c) => byCode[c].type === "E")]?.score ?? 1;
        const score = Math.max(0, Math.min(max, Math.round(max * (0.4 + rand() * 0.6) * 2) / 2));
        await must(admin, "POST", `/admin/attempts/${item.attemptId}/questions/${item.attemptQuestionId}/manual-grade`, {
          score,
          comment: pick(["Bài làm tốt.", "Cần trình bày rõ hơn.", "Ý đúng nhưng còn thiếu chi tiết.", "Good job!"]),
        });
        graded++;
      }
    }
    if (e.close) {
      await must(admin, "POST", `/exams/${exam.id}/close`, { forceSubmitInProgress: true });
    }
    const extra = [
      `${submitted} lượt đã nộp`,
      e.inProgress ? `${e.inProgress.length} lượt đang làm` : null,
      e.gradeEssays ? `chấm tay ${graded} bài tự luận` : null,
      e.close ? "đã đóng" : null,
      e.startAt ? "chưa đến giờ thi" : null,
    ].filter(Boolean);
    console.log(`✔ Đề ${e.code}: ${extra.join(", ")}`);
  }

  console.log("\nXong. Học viên demo: demo.hs01 … demo.hs24 (mật khẩu: biến DEMO_STUDENT_PASSWORD hoặc giá trị mặc định trong script).");
}

main().catch((err) => {
  console.error(`\n✘ ${err.message}`);
  process.exit(1);
});
