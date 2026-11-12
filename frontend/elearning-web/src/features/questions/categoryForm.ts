// Quy tắc form chuyên đề: gợi ý mã chuyên đề con và kiểm tra dữ liệu nhập.

export const CATEGORY_CODE_PATTERN = /^[A-Za-z0-9]+(?:[._-][A-Za-z0-9]+)*$/;

/** Mã chuyên đề con kế tiếp: MÃCHA-01, MÃCHA-02… bỏ qua mã đã dùng. */
export function nextChildCode(parentCode: string, existingCodes: Iterable<string>): string {
  const used = new Set(Array.from(existingCodes, (c) => c.toUpperCase()));
  const base = parentCode.replace(/[._-]+$/, "");
  for (let i = 1; ; i++) {
    const code = `${base}-${String(i).padStart(2, "0")}`;
    if (!used.has(code.toUpperCase())) return code;
  }
}

/** Lỗi nhập liệu của form chuyên đề, rỗng nghĩa là hợp lệ. */
export function validateCategory(form: { id?: string; code: string; name: string }): { code?: string; name?: string } {
  const errors: { code?: string; name?: string } = {};
  const name = form.name.trim();
  if (name.length < 2) errors.name = "Tên chuyên đề cần ít nhất 2 ký tự.";
  else if (!/\p{L}/u.test(name)) errors.name = "Tên chuyên đề cần có chữ, không chỉ gồm số hoặc ký hiệu.";
  if (!form.id) {
    const code = form.code.trim();
    if (!code) errors.code = "Nhập mã chuyên đề.";
    else if (!CATEGORY_CODE_PATTERN.test(code)) errors.code = "Mã chỉ gồm chữ không dấu, số và dấu - . _ ở giữa (vd. TOAN-12).";
  }
  return errors;
}
