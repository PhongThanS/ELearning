import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QuestionCard } from "./ExamPlayerPage";

const base = {
  id: "q1",
  order: 1,
  content: "Chọn **B**",
  contentFormat: "MARKDOWN" as const,
  score: 1,
};

describe("QuestionCard", () => {
  it("câu chọn nhiều: bật / tắt từng lựa chọn", async () => {
    const onSelect = vi.fn();
    render(
      <QuestionCard
        question={{ ...base, type: "MULTIPLE_CHOICE", answerDataType: null, options: [{ code: "A", content: "A1" }, { code: "B", content: "B1" }] }}
        total={5}
        draft={{ selectedOptions: ["A"], answerText: null, isMarkedForReview: false }}
        onSelect={onSelect}
        onText={vi.fn()}
        onMark={vi.fn()}
      />,
    );

    await userEvent.click(screen.getByRole("checkbox", { name: /B1/ }));
    expect(onSelect).toHaveBeenLastCalledWith(["A", "B"]);
    await userEvent.click(screen.getByRole("checkbox", { name: /A1/ }));
    expect(onSelect).toHaveBeenLastCalledWith([]);
  });

  it("câu điền số báo sai định dạng ngay trên ô nhập", () => {
    render(
      <QuestionCard
        question={{ ...base, type: "FILL_IN", answerDataType: "NUMBER", options: [] }}
        total={5}
        draft={{ selectedOptions: [], answerText: "1,000.5", isMarkedForReview: false }}
        onSelect={vi.fn()}
        onText={vi.fn()}
        onMark={vi.fn()}
      />,
    );

    expect(screen.getByRole("textbox")).toHaveClass("is-invalid");
    expect(screen.getByText(/Số không hợp lệ/)).toBeInTheDocument();
  });

  it("render Markdown nhưng không render HTML thô", () => {
    render(
      <QuestionCard
        question={{ ...base, content: "<script>alert(1)</script>\n\nChữ <b>thô</b> và **đậm**", type: "FILL_IN", answerDataType: "TEXT", options: [] }}
        total={1}
        draft={{ selectedOptions: [], answerText: null, isMarkedForReview: false }}
        onSelect={vi.fn()}
        onText={vi.fn()}
        onMark={vi.fn()}
      />,
    );

    expect(document.querySelector("script")).toBeNull();
    expect(screen.getByText("đậm").tagName).toBe("STRONG");
    expect(document.querySelector(".md-content b")).toBeNull();
  });
});
