import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { MultiSearchableSelect } from "./MultiSearchableSelect";

const options = [
  { value: "a", label: "Lớp 10A1", code: "L10A1" },
  { value: "b", label: "Lớp 11B2", code: "L11B2" },
];

describe("MultiSearchableSelect", () => {
  it("hiện các mục đã chọn thành thẻ; bấm × bỏ đúng mục đó", () => {
    const onChange = vi.fn();
    render(<MultiSearchableSelect value={["a", "b"]} onChange={onChange} options={options} />);

    expect(screen.getByText("Lớp 10A1")).toBeInTheDocument();
    expect(screen.getByText("Lớp 11B2")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Bỏ Lớp 10A1" }));
    expect(onChange).toHaveBeenCalledWith(["b"]);

    fireEvent.click(screen.getByRole("button", { name: "Bỏ chọn hết" }));
    expect(onChange).toHaveBeenLastCalledWith([]);
  });

  it("chưa chọn gì thì hiện chú thích", () => {
    render(<MultiSearchableSelect value={[]} onChange={() => {}} options={options} emptyText="Chung cho tất cả" />);
    expect(screen.getByText("Chung cho tất cả")).toBeInTheDocument();
  });
});
