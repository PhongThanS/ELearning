/**
 * Sửa trực tiếp file đề "elearning-exam/1" lúc xem trước (docs/02-nghiep-vu.md mục 1.5): mỗi câu là một khối dòng từ
 * "@question" tới trước "@question" / "@part" / "@end" kế tiếp. Server báo số dòng của "@question" (đếm từ 1), giao
 * diện thay đúng khối đó rồi cho server kiểm tra lại cả file — vẫn chỉ một nguồn sự thật là file .md.
 */

export type Difficulty = "EASY" | "MEDIUM" | "HARD";

const BLOCK_START = /^@(question|part|end)\b/i;
const DIFFICULTY_LINE = /^@difficulty\s*:/i;

/** Đổi xuống dòng về "\n" (server cũng đếm dòng như vậy) và bỏ BOM. */
export function normalizeMarkdown(text: string): string {
  return text.replace(/^﻿/, "").replace(/\r\n?/g, "\n");
}

/** Khối của câu bắt đầu ở dòng `line` (đếm từ 1): [start, end) theo chỉ số dòng; null nếu dòng đó không phải "@question". */
export function questionBlock(lines: readonly string[], line: number): { start: number; end: number } | null {
  const start = line - 1;
  if (start < 0 || start >= lines.length || !/^@question\b/i.test(lines[start] ?? "")) {
    return null;
  }

  let end = start + 1;
  while (end < lines.length && !BLOCK_START.test(lines[end] ?? "")) {
    end++;
  }

  // Câu cuối của file chép nguyên khối ```markdown … ``` từ AI: rào đóng không thuộc câu
  if (end === lines.length) {
    const firstText = lines.findIndex((l) => l.trim().length > 0);
    let lastText = end - 1;
    while (lastText > start && (lines[lastText] ?? "").trim().length === 0) {
      lastText--;
    }
    if (firstText >= 0 && (lines[firstText] ?? "").trimStart().startsWith("```") && (lines[lastText] ?? "").trim() === "```") {
      end = lastText;
    }
  }

  // Dòng trống cuối khối là khoảng cách giữa hai câu, giữ ngoài phần sửa
  while (end - 1 > start && (lines[end - 1] ?? "").trim().length === 0) {
    end--;
  }
  return { start, end };
}

/** Nội dung gốc của câu ở dòng `line`, để sửa. */
export function questionText(markdown: string, line: number): string | null {
  const lines = markdown.split("\n");
  const block = questionBlock(lines, line);
  return block ? lines.slice(block.start, block.end).join("\n") : null;
}

/** Thay khối của câu ở dòng `line` bằng `text`; trả nguyên markdown nếu không tìm thấy câu. */
export function replaceQuestion(markdown: string, line: number, text: string): string {
  const lines = markdown.split("\n");
  const block = questionBlock(lines, line);
  if (!block) {
    return markdown;
  }
  const replacement = normalizeMarkdown(text).replace(/\n+$/, "").split("\n");
  return [...lines.slice(0, block.start), ...replacement, ...lines.slice(block.end)].join("\n");
}

/**
 * Đặt độ khó riêng cho câu: sửa dòng "@difficulty" của câu (thêm ngay sau "@question" nếu chưa có); null = bỏ dòng đó
 * (câu lại theo mặc định của "@part", nếu có).
 */
export function setQuestionDifficulty(markdown: string, line: number, difficulty: Difficulty | null): string {
  const lines = markdown.split("\n");
  const block = questionBlock(lines, line);
  if (!block) {
    return markdown;
  }
  const own = lines.slice(block.start, block.end).findIndex((l) => DIFFICULTY_LINE.test(l));
  const next = [...lines];
  if (own >= 0) {
    if (difficulty) {
      next[block.start + own] = `@difficulty: ${difficulty}`;
    } else {
      next.splice(block.start + own, 1);
    }
  } else if (difficulty) {
    next.splice(block.start + 1, 0, `@difficulty: ${difficulty}`);
  }
  return next.join("\n");
}
