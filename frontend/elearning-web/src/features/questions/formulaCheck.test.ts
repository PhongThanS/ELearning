import { checkFormulas, formulasMatch, normalizeLatex, wordQuestionBlocks } from "./formulaCheck";

// Văn bản SClass chuyển từ file Word (POST …/docx-for-ai): công thức MathType đã thành LaTeX
const word = [
  "<!-- SClass: đề chuyển từ file Word -->",
  "PHẦN I. Trắc nghiệm",
  "Câu 1: [MĐ1] Cho tam giác $ABC$ với trọng tâm $G$. Tìm $k$ thỏa mãn $\\overrightarrow{GA}=k.\\overrightarrow{GM}$.",
  "A. $\\frac{1}{2}$.\tB. $-\\frac{1}{2}$.\tC. -2.\tD. 2.",
  "Lời giải",
  "![hình](hinh-p1-c1.png)",
  "Vì $\\overrightarrow{GA}=-2\\overrightarrow{GM}$.",
  "PHẦN II. Đúng sai",
  "Câu 2: Cho $\\Delta ABC$ có $\\widehat{A}=30^{\\circ}$.",
].join("\n");

describe("checkFormulas — phát hiện công thức AI tự bịa khi không đọc được MathType", () => {
  it("cách viết khác nhưng cùng công thức vẫn khớp (\\vec, \\dfrac, cách, \\triangle, °)", () => {
    expect(normalizeLatex("\\vec{GA} = k \\cdot \\vec{GM}")).toBe(normalizeLatex("\\overrightarrow{GA}=k.\\overrightarrow{GM}"));
    expect(normalizeLatex("\\triangle ABC, \\hat{A} = 30°")).toBe(normalizeLatex("\\Delta ABC,\\widehat{A}=30^{\\circ}"));
    expect(normalizeLatex("-\\dfrac{1}{2}")).toBe(normalizeLatex("-\\frac{1}{2}"));
  });

  it("file chép đúng: khớp; file bịa: báo đúng dòng, tách công thức của đề với của lời giải", () => {
    const good = "@question: 1\n@content:\nTìm $k$ thỏa mãn $\\vec{GA} = k\\vec{GM}$.\n@option A: $\\dfrac{1}{2}$\n@option B: $-\\dfrac{1}{2}$\n@answer: C";
    const fake = "@question: 1\n@content:\nTìm $k$ thỏa mãn $\\vec{AG} = k\\vec{AM}$.\n@option A: $k = \\dfrac{2}{3}$\n@option B: $-\\dfrac{1}{2}$\n@answer: A\n@explanation:\nVì $AG = \\dfrac{2}{3}AM$.";
    const ok = checkFormulas(good, word);
    expect(ok.missing).toEqual([]);
    expect(formulasMatch(ok)).toBe(true);

    const bad = checkFormulas(fake, word);
    expect(bad.missing.map((m) => [m.line, m.formula, m.explanation])).toEqual([
      [3, "\\vec{AG} = k\\vec{AM}", false],
      [4, "k = \\dfrac{2}{3}", false],
      [8, "AG = \\dfrac{2}{3}AM", true],
    ]);
    expect(formulasMatch(bad)).toBe(false);
  });

  it("công thức chỉ là tên điểm / một chữ ($A$, $ABC$, $k$) không tính", () => {
    expect(checkFormulas("@question: 1\n@content:\nCho $ABC$ và $k$ và $XYZ$", word).checked).toBe(0);
  });

  it("cắt đề gốc trong Word theo từng câu, bỏ tiêu đề phần, hình thành chữ", () => {
    const blocks = wordQuestionBlocks(word, 2)!;
    expect(blocks).toHaveLength(2);
    expect(blocks[0]).toContain("Câu 1:");
    expect(blocks[0]).toContain("*(hình)*");
    expect(blocks[0]).not.toContain("PHẦN II");
    expect(blocks[1]).toContain("$\\widehat{A}=30^{\\circ}$");
    expect(wordQuestionBlocks(word, 3)).toBeNull();
  });
});
