/**
 * Báo cáo học tập ngày: dữ liệu + vẽ ảnh PNG bằng canvas (không cần thư viện ngoài).
 * Ảnh dùng để gửi phụ huynh qua Zalo: cả lớp, hoặc từng học sinh (kèm phần đầu báo cáo).
 */

export interface DailyReportInfo {
  /** Ngày học yyyy-MM-dd. */
  date: string;
  className: string;
  topic: string;
  homework: string;
  deadline: string;
  attention: string;
}

export interface DailyReportRow {
  key: string;
  /** Số thứ tự trong báo cáo (giữ nguyên khi chụp riêng một dòng). */
  order: number;
  lastName: string;
  firstName: string;
  score: number | null;
  /** Trạng thái điểm danh hiển thị (Có mặt, Đi muộn, Vắng có phép...). */
  attendance: string;
  /** Màu chữ của ô điểm danh. */
  attendanceColor?: string;
  /** Phân loại hiển thị ("Loại 2"), rỗng nếu không phân loại (D-47). */
  level?: string;
  comment: string;
}

/** Màu ô điểm danh theo trạng thái (vắng đỏ, muộn cam, online xanh dương, có mặt xanh lá). */
export const REPORT_ATTENDANCE_COLORS: Record<string, string> = {
  PRESENT: "#2e7d32",
  LATE: "#d97706",
  ONLINE: "#1d4ed8",
  EXCUSED: "#0e7490",
  UNEXCUSED: "#c62828",
};

/** "Nguyễn Thị Minh Hà" → ["Nguyễn Thị Minh", "Hà"]: tên là chữ cuối, còn lại là họ và tên đệm. */
export function splitVietnameseName(fullName: string): [string, string] {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length <= 1) return ["", parts[0] ?? ""];
  return [parts.slice(0, -1).join(" "), parts[parts.length - 1]!];
}

/** Điểm hiển thị 2 chữ số thập phân, dấu phẩy: 9.5 → "9,50"; chưa có điểm → "—". */
export function formatReportScore(score: number | null): string {
  return score === null ? "—" : score.toFixed(2).replace(".", ",");
}

/** dd/MM/yyyy từ yyyy-MM-dd. */
export function formatReportDate(date: string): string {
  const [y, m, d] = date.split("-");
  return y && m && d ? `${d}/${m}/${y}` : date;
}

/** Xếp điểm cao → thấp (chưa có điểm xuống cuối), cùng điểm thì theo tên rồi họ. */
export function sortReportRows<T extends { score: number | null; firstName: string; lastName: string }>(rows: T[]): T[] {
  const cmp = (a: string, b: string) => a.localeCompare(b, "vi");
  return [...rows].sort((a, b) => {
    if (a.score !== b.score) {
      if (a.score === null) return 1;
      if (b.score === null) return -1;
      return b.score - a.score;
    }
    return cmp(a.firstName, b.firstName) || cmp(a.lastName, b.lastName);
  });
}

// ---------------------------------------------------------------- vẽ ảnh

const COLORS = {
  title: "#e53935",
  label: "#c62828",
  labelDark: "#1f2937",
  labelGreen: "#2e7d32",
  value: "#111827",
  header: "#2e7d32",
  headerText: "#ffffff",
  rowAlt: "#fbf1e8",
  border: "#cfcfcf",
  muted: "#6b7280",
};

const WIDTH = 1100;
const MARGIN = 24;
const COLS = [
  { key: "order", title: "STT", width: 64, align: "center" as const },
  { key: "lastName", title: "HỌ VÀ TÊN ĐỆM", width: 230, align: "left" as const },
  { key: "firstName", title: "TÊN", width: 130, align: "center" as const },
  { key: "attendance", title: "ĐIỂM DANH", width: 150, align: "center" as const },
  { key: "score", title: "ĐIỂM BTVN", width: 120, align: "center" as const },
  { key: "level", title: "PHÂN LOẠI", width: 110, align: "center" as const },
  { key: "comment", title: "NHẬN XÉT", width: WIDTH - 2 * MARGIN - 64 - 230 - 130 - 150 - 120 - 110, align: "left" as const },
];
const LINE = 22;
const CELL_PAD_X = 10;
const CELL_PAD_Y = 9;

function font(weight: number, size: number, italic = false) {
  return `${italic ? "italic " : ""}${weight} ${size}px "Be Vietnam Pro", "Segoe UI", Arial, sans-serif`;
}

/** Ngắt dòng theo độ rộng (giữ xuống dòng có sẵn; từ quá dài thì cắt theo ký tự). */
function wrap(ctx: CanvasRenderingContext2D, text: string, maxWidth: number): string[] {
  const lines: string[] = [];
  for (const paragraph of text.split(/\r?\n/)) {
    let line = "";
    for (const word of paragraph.split(/\s+/).filter(Boolean)) {
      const candidate = line ? `${line} ${word}` : word;
      if (ctx.measureText(candidate).width <= maxWidth) {
        line = candidate;
        continue;
      }
      if (line) lines.push(line);
      line = word;
      while (ctx.measureText(line).width > maxWidth && line.length > 1) {
        let cut = line.length - 1;
        while (cut > 1 && ctx.measureText(line.slice(0, cut)).width > maxWidth) cut--;
        lines.push(line.slice(0, cut));
        line = line.slice(cut);
      }
    }
    lines.push(line);
  }
  return lines.length ? lines : [""];
}

interface Layout {
  height: number;
  draw: (ctx: CanvasRenderingContext2D) => void;
}

function layoutReport(ctx: CanvasRenderingContext2D, info: DailyReportInfo, rows: DailyReportRow[]): Layout {
  const ops: ((c: CanvasRenderingContext2D) => void)[] = [];
  let y = MARGIN;

  // Tiêu đề
  const title = `BÁO CÁO HỌC TẬP NGÀY ${formatReportDate(info.date)}`;
  ops.push((c) => {
    c.font = font(700, 26);
    c.fillStyle = COLORS.title;
    c.textAlign = "center";
    c.textBaseline = "top";
    c.fillText(title, WIDTH / 2, MARGIN);
  });
  y += 36;
  if (info.className) {
    const yy = y;
    ops.push((c) => {
      c.font = font(500, 15);
      c.fillStyle = COLORS.muted;
      c.textAlign = "center";
      c.fillText(`Lớp ${info.className}`, WIDTH / 2, yy);
    });
    y += 26;
  }
  y += 8;

  // Các dòng thông tin: nhãn căn phải, giá trị in nghiêng đậm, tự xuống dòng
  const labelRight = MARGIN + 190;
  const valueX = labelRight + 24;
  const valueWidth = WIDTH - MARGIN - valueX;
  const infoRows: [string, string, string, string][] = [
    ["NỘI DUNG:", info.topic, COLORS.label, COLORS.title],
    ["BTVN:", info.homework, COLORS.labelDark, COLORS.value],
    ["HẠN NỘP:", info.deadline, COLORS.labelDark, COLORS.value],
    ["CHÚ Ý:", info.attention, COLORS.labelGreen, COLORS.value],
  ];
  for (const [label, value, labelColor, valueColor] of infoRows) {
    ctx.font = font(700, 16, true);
    const lines = wrap(ctx, value || "", valueWidth);
    const yy = y;
    ops.push((c) => {
      c.textAlign = "right";
      c.textBaseline = "top";
      c.font = font(700, 16, true);
      c.fillStyle = labelColor;
      c.fillText(label, labelRight, yy);
      c.textAlign = "left";
      c.fillStyle = valueColor;
      lines.forEach((l, i) => c.fillText(l, valueX, yy + i * LINE));
    });
    y += Math.max(1, lines.length) * LINE + 8;
  }
  y += 8;

  // Tiêu đề bảng
  const headerH = 44;
  const tableTop = y;
  ops.push((c) => {
    c.fillStyle = COLORS.header;
    c.fillRect(MARGIN, tableTop, WIDTH - 2 * MARGIN, headerH);
    c.font = font(700, 15);
    c.fillStyle = COLORS.headerText;
    c.textBaseline = "middle";
    let x = MARGIN;
    for (const col of COLS) {
      c.textAlign = "center";
      c.fillText(col.title, x + col.width / 2, tableTop + headerH / 2);
      x += col.width;
    }
  });
  y += headerH;

  // Các dòng học sinh
  rows.forEach((row, index) => {
    ctx.font = font(400, 15);
    const commentLines = wrap(ctx, row.comment || "", COLS[6]!.width - 2 * CELL_PAD_X);
    ctx.font = font(700, 15);
    const attendanceLines = wrap(ctx, row.attendance, COLS[3]!.width - 2 * CELL_PAD_X);
    ctx.font = font(700, 15);
    const lastLines = wrap(ctx, row.lastName, COLS[1]!.width - 2 * CELL_PAD_X);
    const lines = Math.max(commentLines.length, lastLines.length, attendanceLines.length, 1);
    const h = Math.max(40, lines * LINE + 2 * CELL_PAD_Y);
    const top = y;
    ops.push((c) => {
      c.fillStyle = index % 2 === 1 ? COLORS.rowAlt : "#ffffff";
      c.fillRect(MARGIN, top, WIDTH - 2 * MARGIN, h);
      c.strokeStyle = COLORS.border;
      c.lineWidth = 1;
      let x = MARGIN;
      const cells: { text: string[]; weight: number; color?: string }[] = [
        { text: [String(row.order)], weight: 700 },
        { text: lastLines, weight: 700 },
        { text: [row.firstName], weight: 700 },
        { text: attendanceLines, weight: 700, color: row.attendanceColor },
        { text: [formatReportScore(row.score)], weight: 700 },
        { text: [row.level ?? ""], weight: 700 },
        { text: commentLines, weight: 400 },
      ];
      COLS.forEach((col, i) => {
        c.strokeRect(x + 0.5, top + 0.5, col.width, h);
        const cell = cells[i]!;
        c.font = font(cell.weight, 15);
        c.fillStyle = cell.color ?? COLORS.value;
        c.textBaseline = "middle";
        const blockH = cell.text.length * LINE;
        const startY = top + (h - blockH) / 2 + LINE / 2;
        cell.text.forEach((t, li) => {
          if (col.align === "center") {
            c.textAlign = "center";
            c.fillText(t, x + col.width / 2, startY + li * LINE);
          } else {
            c.textAlign = "left";
            c.fillText(t, x + CELL_PAD_X, startY + li * LINE);
          }
        });
        x += col.width;
      });
    });
    y += h;
  });

  y += MARGIN;
  return {
    height: y,
    draw: (c) => {
      c.fillStyle = "#ffffff";
      c.fillRect(0, 0, WIDTH, y);
      ops.forEach((op) => op(c));
    },
  };
}

/** Vẽ báo cáo (cả lớp hoặc một số dòng) thành canvas độ phân giải gấp đôi cho ảnh nét. */
export async function renderDailyReport(info: DailyReportInfo, rows: DailyReportRow[], scale = 2): Promise<HTMLCanvasElement> {
  await document.fonts?.ready;
  const measure = document.createElement("canvas").getContext("2d");
  if (!measure) throw new Error("Trình duyệt không hỗ trợ vẽ ảnh.");
  const layout = layoutReport(measure, info, rows);
  const canvas = document.createElement("canvas");
  canvas.width = WIDTH * scale;
  canvas.height = Math.ceil(layout.height * scale);
  const ctx = canvas.getContext("2d")!;
  ctx.scale(scale, scale);
  layout.draw(ctx);
  return canvas;
}

export function canvasToBlob(canvas: HTMLCanvasElement): Promise<Blob> {
  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error("Không tạo được ảnh."))), "image/png"),
  );
}

/** Chép ảnh vào clipboard (dán thẳng vào Zalo); trả false nếu trình duyệt không cho. */
export async function copyImage(blob: Blob): Promise<boolean> {
  try {
    if (!navigator.clipboard || typeof ClipboardItem === "undefined") return false;
    await navigator.clipboard.write([new ClipboardItem({ "image/png": blob })]);
    return true;
  } catch {
    return false;
  }
}

export function downloadImage(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
