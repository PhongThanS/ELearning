import { describe, expect, it } from "vitest";
import { normalizeMarkdown, questionText, replaceQuestion, setQuestionDifficulty } from "./markdownBlocks";

const FILE = [
  "@format: elearning-exam/1", // 1
  "@part: PHẦN I", // 2
  "@type: SINGLE_CHOICE", // 3
  "@score: 0.25", // 4
  "", // 5
  "@question: 1", // 6
  "@content: 1 + 1 = ?", // 7
  "@option A: 2", // 8
  "@option B: 3", // 9
  "@answer: A", // 10
  "", // 11
  "@question: 2", // 12
  "@difficulty: HARD", // 13
  "@content:", // 14
  "Viết @@ trong nội dung", // 15
  "@option A: x", // 16
  "@option B: y", // 17
  "@answer: B", // 18
  "", // 19
  "@part: PHẦN II", // 20
].join("\n");

describe("markdownBlocks", () => {
  it("lấy đúng khối câu, không gồm dòng trống và @part kế tiếp", () => {
    expect(questionText(FILE, 6)).toBe("@question: 1\n@content: 1 + 1 = ?\n@option A: 2\n@option B: 3\n@answer: A");
    expect(questionText(FILE, 12)?.split("\n")).toHaveLength(7);
    expect(questionText(FILE, 7)).toBeNull();
  });

  it("thay một câu mà giữ nguyên phần còn lại của file", () => {
    const next = replaceQuestion(FILE, 6, "@question: 1\r\n@content: 2 + 2 = ?\r\n@option A: 4\r\n@option B: 5\r\n@answer: A\r\n\r\n");

    expect(questionText(next, 6)).toContain("2 + 2");
    expect(next.split("\n")[10]).toBe("");
    expect(next.split("\n")[11]).toBe("@question: 2");
    expect(next.endsWith("@part: PHẦN II")).toBe(true);
  });

  it("đặt, đổi và bỏ độ khó riêng của câu", () => {
    const added = setQuestionDifficulty(FILE, 6, "EASY");
    expect(added.split("\n")[6]).toBe("@difficulty: EASY");

    const changed = setQuestionDifficulty(FILE, 12, "MEDIUM");
    expect(changed.split("\n")[12]).toBe("@difficulty: MEDIUM");
    expect(changed.split("\n")).toHaveLength(FILE.split("\n").length);

    const removed = setQuestionDifficulty(FILE, 12, null);
    expect(removed).not.toContain("@difficulty");
    expect(setQuestionDifficulty(FILE, 6, null)).toBe(FILE);
  });

  it("câu cuối của file bọc trong khối ```markdown không lấy rào đóng", () => {
    const fenced = normalizeMarkdown("﻿```markdown\r\n@format: elearning-exam/1\r\n@question: 1\r\n@type: ESSAY\r\n@content: Viết.\r\n```\r\n");

    expect(questionText(fenced, 3)).toBe("@question: 1\n@type: ESSAY\n@content: Viết.");
    expect(setQuestionDifficulty(fenced, 3, "HARD").split("\n").slice(-2)).toEqual(["```", ""]);
  });
});
