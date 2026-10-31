import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SplitView, clampSplit } from "./SplitView";

describe("SplitView", () => {
  it("giới hạn tỉ lệ trong 15–85%", () => {
    expect(clampSplit(3)).toBe(15);
    expect(clampSplit(99)).toBe(85);
    expect(clampSplit(42.345)).toBe(42.3);
  });

  it("co giãn bằng phím mũi tên, Home / End và nhấp đúp để chia đều", () => {
    render(<SplitView label="Thanh chia" first={<p>Video</p>} second={<p>Tài liệu</p>} />);
    const handle = screen.getByRole("separator", { name: "Thanh chia" });
    expect(handle).toHaveAttribute("aria-valuenow", "50");

    fireEvent.keyDown(handle, { key: "ArrowRight" });
    expect(handle).toHaveAttribute("aria-valuenow", "55");
    fireEvent.keyDown(handle, { key: "Home" });
    expect(handle).toHaveAttribute("aria-valuenow", "15");
    fireEvent.doubleClick(handle);
    expect(handle).toHaveAttribute("aria-valuenow", "50");
    expect(screen.getByText("Video")).toBeInTheDocument();
    expect(screen.getByText("Tài liệu")).toBeInTheDocument();
  });
});
