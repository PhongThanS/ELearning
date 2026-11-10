/**
 * Đối chiếu công thức trong file .md (AI viết) với công thức đọc thẳng từ file Word gốc (SClass đổi MathType / công thức
 * Word sang LaTeX, POST …/docx-for-ai). AI không đọc được MathType trong .docx và hay tự bịa công thức khớp đáp án: file
 * vẫn hợp lệ nhưng đề đã khác đề gốc. Công thức nào của file .md không có trong file Word là dấu hiệu bị bịa.
 */

/**
 * Đưa LaTeX về một dạng để so: bỏ khoảng trắng, \left/\right, ngoặc nhọn, dấu câu; gộp các cách viết tương đương mà AI
 * hay đổi (\vec ↔ \overrightarrow, \dfrac ↔ \frac, \Delta ↔ \triangle ↔ Δ, ^{\circ} ↔ °, \text{…}).
 */
export function normalizeLatex(latex: string): string {
  return latex
    // Hệ / bảng: \begin{array}{l} ↔ \begin{cases}, dấu & và xuống dòng \\ không đổi nội dung
    .replace(/\\begin\{[a-z*]*\}(?:\{[^{}]*\})?|\\end\{[a-z*]*\}|&|\\\\|\\[{}]/g, "")
    .replace(/\\text\{([^{}]*)\}|\\textrm\{([^{}]*)\}|\\mathrm\{([^{}]*)\}|\\operatorname\{([^{}]*)\}/g, (_, a, b, c, d) => a ?? b ?? c ?? d ?? "")
    .replace(/\\(?:overrightarrow|vec)\s*/g, "→")
    .replace(/\\(?:widehat|hat)\s*/g, "^")
    .replace(/\\[dt]frac/g, "\\frac")
    .replace(/\\(?:Delta|triangle|bigtriangleup)(?![A-Za-z])|Δ|△/g, "Δ")
    .replace(/\^\s*\{?\\circ\}?|°/g, "°")
    .replace(/\\(?:left|right|displaystyle|limits)(?![A-Za-z])|\\[,;:! ]|~/g, "")
    .replace(/\\(?:cdot|times)(?![A-Za-z])|·/g, "")
    .replace(/\\leq?(?![A-Za-z])|≤/g, "≤")
    .replace(/\\geq?(?![A-Za-z])|≥/g, "≥")
    .replace(/\\neq?(?![A-Za-z])|≠/g, "≠")
    .replace(/\\(?:vert|mid)(?![A-Za-z])|\\\||\|/g, "|")
    .replace(/\\(sin|cos|tan|cot|log|ln)(?![A-Za-z])/g, "$1")
    .replace(/[\s{}_$.,;:]/g, "");
}

/** Công thức trong một đoạn văn bản Markdown: `$…$` và `$$…$$`. */
export function formulasIn(text: string): string[] {
  return [...text.matchAll(/\$\$([\s\S]+?)\$\$|\$([^$\n]+?)\$/g)].map((m) => m[1] ?? m[2] ?? "");
}

/** `explanation`: công thức nằm trong lời giải (AI hay viết lại lời giải nên chỉ để tham khảo). */
export type FormulaMismatch = { line: number; formula: string; explanation: boolean };

export type FormulaCheck = {
  /** Số công thức trong đề (nội dung, phương án, ý) đủ "nội dung" để so — bỏ $A$, $ABC$, $k$. */
  checked: number;
  /** Số công thức trong lời giải đã so. */
  checkedExplanations: number;
  /** Công thức của file .md không tìm thấy trong file Word (kèm số dòng trong file .md). */
  missing: FormulaMismatch[];
};

/** Công thức có phép toán / lệnh / chữ số mới đáng so; $A$, $ABC$, $k$ khớp ở đề nào cũng được. */
function meaningful(normalized: string): boolean {
  return normalized.length >= 3 && /[\\=+\-<>≤≥≠→^/|°0-9Δ]/.test(normalized);
}

/**
 * So từng công thức của file .md (từ câu đầu tiên trở đi, bỏ dòng `@answer`) với toàn bộ văn bản file Word đã chuyển.
 * Công thức được coi là có trong Word khi dạng chuẩn hóa của nó nằm trong dạng chuẩn hóa của văn bản Word.
 */
export function checkFormulas(markdown: string, wordText: string): FormulaCheck {
  const word = normalizeLatex(wordText);
  const missing: FormulaMismatch[] = [];
  let checked = 0;
  let checkedExplanations = 0;
  let started = false;
  let explanation = false;
  markdown.split(/\r?\n/).forEach((line, index) => {
    const directive = /^\s*@([a-z]+)/i.exec(line)?.[1]?.toLowerCase();
    if (directive === "question") {
      started = true;
    }
    if (directive) {
      explanation = directive === "explanation";
    }
    if (!started || directive === "answer") {
      return;
    }
    for (const formula of formulasIn(line)) {
      const normalized = normalizeLatex(formula);
      if (!meaningful(normalized)) {
        continue;
      }
      if (explanation) {
        checkedExplanations++;
      } else {
        checked++;
      }
      if (!word.includes(normalized)) {
        missing.push({ line: index + 1, formula, explanation });
      }
    }
  });
  return { checked, checkedExplanations, missing };
}

/**
 * Cắt văn bản file Word thành từng câu theo dòng "Câu 3:" / "Bài 3." (cả lời giải ngay dưới câu), bỏ tiêu đề phần và
 * thay dòng hình bằng chữ "(hình)". Trả null khi số câu trong Word ít hơn số câu của file .md (không ghép được theo thứ
 * tự); nhiều hơn (ví dụ phần lời giải gom cuối đề đánh số lại) thì lấy các câu đầu.
 */
export function wordQuestionBlocks(wordText: string, count: number): string[] | null {
  const blocks: string[][] = [];
  for (const line of wordText.split(/\r?\n/)) {
    if (/^\s*(?:CÂU|BÀI)\s*\d{1,3}\s*[.:)]/i.test(line)) {
      blocks.push([]);
    }
    if (blocks.length === 0 || /^\s*PH[ẦẨA]N\s+(?:[IVX]+|\d+)\b/i.test(line) || /^\s*<!--/.test(line)) {
      continue;
    }
    blocks[blocks.length - 1]!.push(line.replace(/!\[[^\]]*\]\([^)]*\)/g, "*(hình)*"));
  }
  return blocks.length >= count && count > 0 ? blocks.slice(0, count).map((b) => b.join("\n").trim()) : null;
}

/** Công thức trong đề (không tính lời giải) khớp file Word từ 90% trở lên: coi như AI chép đúng. */
export function formulasMatch(check: FormulaCheck): boolean {
  return check.checked > 0 && check.missing.filter((m) => !m.explanation).length <= check.checked * 0.1;
}
