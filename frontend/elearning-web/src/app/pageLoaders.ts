/**
 * Module của từng nhóm trang (mỗi module là một chunk JS riêng). Dùng chung cho route (tải khi mở trang) và cho việc
 * tải ngầm sẵn sau khi đăng nhập (prefetchPages): mở trang khác không phải chờ tải chunk qua mạng.
 */
export const pageLoaders = {
  identity: () => import("../features/admin/IdentityPages"),
  classes: () => import("../features/admin/ClassPages"),
  questions: () => import("../features/questions/QuestionPages"),
  markdownImport: () => import("../features/questions/MarkdownImportPage"),
  exams: () => import("../features/exams/ExamPages"),
  examPreview: () => import("../features/exams/ExamPreviewPage"),
  builder: () => import("../features/exams/VersionBuilderPage"),
  results: () => import("../features/exams/ResultPages"),
  dashboard: () => import("../features/admin/DashboardPage"),
  student: () => import("../features/attempts/StudentPages"),
  player: () => import("../features/attempts/ExamPlayerPage"),
  videos: () => import("../features/videos/AdminVideoPages"),
  studentVideos: () => import("../features/videos/StudentVideoPages"),
  studentAttendance: () => import("../features/attendance/StudentAttendancePage"),
  videoWatch: () => import("../features/videos/VideoWatchPage"),
  attendance: () => import("../features/attendance/AttendancePage"),
} satisfies Record<string, () => Promise<unknown>>;

type PageKey = keyof typeof pageLoaders;

/** Thứ tự tải ngầm: trang hay dùng trước. Học viên: phòng thi trước để vào thi không phải chờ. */
const prefetchOrder: Record<"admin" | "student", PageKey[]> = {
  admin: ["questions", "exams", "classes", "attendance", "videos", "dashboard", "markdownImport", "builder", "results", "examPreview", "identity", "videoWatch"],
  student: ["player", "student", "studentAttendance", "studentVideos", "videoWatch"],
};

const prefetched = new Set<string>();

type NetworkInformation = { saveData?: boolean; effectiveType?: string };

/** Mạng tiết kiệm dữ liệu / 2G: không tải ngầm, chỉ tải trang khi mở. */
function constrainedNetwork(): boolean {
  const connection = (navigator as Navigator & { connection?: NetworkInformation }).connection;
  return connection?.saveData === true || /(^|-)2g$/.test(connection?.effectiveType ?? "");
}

function whenIdle(callback: () => void): void {
  if (typeof window.requestIdleCallback === "function") {
    window.requestIdleCallback(callback, { timeout: 3000 });
  } else {
    window.setTimeout(callback, 1500);
  }
}

/**
 * Tải ngầm, lần lượt từng trang khi trình duyệt rảnh, sau khi vào khu quản trị / học viên. Mỗi trang chỉ tải một
 * lần; lỗi (mạng, bản cũ sau deploy) bỏ qua — trang vẫn tải bình thường khi mở.
 */
export function prefetchPages(area: "admin" | "student"): void {
  // Unit test render layout: không nạp toàn bộ trang
  if (import.meta.env.MODE === "test" || constrainedNetwork()) {
    return;
  }
  const queue = prefetchOrder[area].filter((key) => !prefetched.has(key));
  queue.forEach((key) => prefetched.add(key));
  const next = () => {
    const key = queue.shift();
    if (!key) {
      return;
    }
    void pageLoaders[key]()
      .catch(() => undefined)
      .finally(() => whenIdle(next));
  };
  whenIdle(next);
}
