/**
 * Bản sao phía client của ELearning.Domain.Grading.AnswerNormalizer (docs/02-nghiep-vu.md mục 3.1),
 * chỉ dùng cho ô "Thử đáp án" trong trình soạn câu hỏi. Điểm chính thức luôn do server chấm.
 */
export function normalizeAnswer(input: string | null | undefined, caseSensitive: boolean, ignoreAccent: boolean): string {
  if (!input) {
    return "";
  }
  let text = input.normalize("NFC").replace(/\s+/gu, " ").trim();
  if (!caseSensitive) {
    text = text.toLowerCase();
  }
  if (ignoreAccent) {
    text = text
      .normalize("NFD")
      .replace(/\p{Mn}/gu, "")
      .replace(/đ/g, "d")
      .replace(/Đ/g, "D")
      .normalize("NFC");
  }
  return text;
}

export function matchesAny(input: string, accepted: string[], caseSensitive: boolean, ignoreAccent: boolean): boolean {
  const value = normalizeAnswer(input, caseSensitive, ignoreAccent);
  return value.length > 0 && accepted.some((a) => normalizeAnswer(a, caseSensitive, ignoreAccent) === value);
}
